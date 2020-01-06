using BO.Integracion.Siebel;
using Cliente360.Integracion.Alignet.Entities;
using Release.Helper.Data.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Configuration;
//using Cliente360.Integracion.Notificacion;

namespace Cliente360.Integracion.Alignet
{
    public class IntegracionAlignetManager
    {
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static string csAlignet;

        private readonly string APICONSULTA_ALIGNET = ConfigurationManager.AppSettings["APICONSULTA_ALIGNET"];

        private readonly string APIREVERSE_ALIGNET = ConfigurationManager.AppSettings["APIREVERSE_ALIGNET"];

        private readonly string IDACQUIRER = ConfigurationManager.AppSettings["IDACQUIRER"];
        private readonly string IDCOMMERCE = ConfigurationManager.AppSettings["IDCOMMERCE"];
        //private string OPERATIONNUMBER = "62170007";
        private readonly string AUTHORIZATION_CONSULTA = ConfigurationManager.AppSettings["AUTHORIZATION_CONSULTA"];
        private readonly string AUTHORIZATION_EXTORNO = ConfigurationManager.AppSettings["AUTHORIZATION_EXTORNO"];

        //System.Configuration.ConfigurationManager
        private static readonly string CODEST_AUTORIZADO =  ConfigurationManager.AppSettings["CODEST_AUTORIZADO"]; //0
        private static readonly string CODERROR_EXCEPCION =  ConfigurationManager.AppSettings["CODERROR_EXCEPCION"]; //21
        private static readonly string CODERROR_ALIGNET =  ConfigurationManager.AppSettings["CODERROR_ALIGNET"]; //22
        private static readonly string CODEST_OKEXTORNADO =  ConfigurationManager.AppSettings["CODEST_OKEXTORNADO"]; //2
        private static readonly string CODEST_OKLIQUIDADO = ConfigurationManager.AppSettings["CODEST_OKLIQUIDADO"]; //3


        private static readonly string DIAS_PROCESO_EXTORNO = ConfigurationManager.AppSettings["DIAS_PROCESO_EXTORNO"]; 

        private static readonly string cadenaConexion = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
        //private static readonly string cadenaConexion = ConfigurationManager.ConnectionStrings["connectionStringDesa"].ConnectionString;

        private static readonly string COD_RESULT_LIQUIDADO = ConfigurationManager.AppSettings["COD_RESULT_LIQUIDADO"]; //"7";
        private static readonly string COD_RESULT_EXTORNADO = ConfigurationManager.AppSettings["COD_RESULT_EXTORNADO"]; //"8";
        private static readonly string ESTADO_EXTORNADO = ConfigurationManager.AppSettings["ESTADO_EXTORNADO"]; //"Extornado";
        private static readonly string ESTADO_LIQUIDADO = ConfigurationManager.AppSettings["ESTADO_LIQUIDADO"]; //"Liquidado";
        //private static readonly string VALIDAR_EXTORNO_REALIZADO = ConfigurationManager.AppSettings["VALIDAR_EXTORNO_REALIZADO"];  


        public IntegracionAlignetManager(string csalignet)
        {
            csAlignet = csalignet;
        }
        public void Start()
        {

            Newtonsoft.Json.Linq.JObject objreversa;
            string[] estados = { CODEST_AUTORIZADO, CODERROR_EXCEPCION, CODERROR_ALIGNET };
            //var notification = new NotificationManager(cadenaConexion);
            try
            {
                _logger.Info("Inicio del procesamiento #2 Extorno.");
                var transacciones_alignet = ObtenerTransaccionesParaExtornar(estados);

                foreach (base_transacciones el in transacciones_alignet)
                {
                    string _result=null;

                    _result = REVERSE_ALIGNET(el.NUMERO_PEDIDO);
                    //Console.WriteLine(" _result :" + _result);
                    if (_result.Length > 0)
                    {
                        objreversa = Newtonsoft.Json.Linq.JObject.Parse(_result);
                        objreversa.TryGetValue("success", out Newtonsoft.Json.Linq.JToken value);
                        //Console.WriteLine(value.ToString());
                        if (value.ToString() == "true")
                        {
                            Console.WriteLine(" el.NUMERO_PEDIDO :" + el.NUMERO_PEDIDO + " Extornado");
                            ActualizarTransaccionesAlignet(el.ID, CODEST_OKEXTORNADO, ESTADO_EXTORNADO);
                        }
                        else
                        {
                            string _estado = GET_RESULT_CONSULTA_ALIGNET(el.NUMERO_PEDIDO);

                            if (_estado == COD_RESULT_EXTORNADO || _estado == COD_RESULT_LIQUIDADO)
                            {
                                Console.WriteLine(" el.NUMERO_PEDIDO :" + el.NUMERO_PEDIDO + _estado +" por Consulta");
                                if (_estado == COD_RESULT_EXTORNADO) ActualizarTransaccionesAlignet(el.ID, CODEST_OKEXTORNADO, ESTADO_EXTORNADO);
                                if (_estado == COD_RESULT_LIQUIDADO) ActualizarTransaccionesAlignet(el.ID, CODEST_OKLIQUIDADO, ESTADO_LIQUIDADO);
                            }
                            else
                            {
                                ActualizarTransaccionesAlignet(el.ID, CODERROR_ALIGNET, el.ESTADO_TRANSACCION);
                            }

                        }
                    }
                    else
                    {
                        ActualizarTransaccionesAlignet(el.ID, CODERROR_ALIGNET, el.ESTADO_TRANSACCION);
                    }
               }

                _logger.Info("Fin del procesamiento #2 Extorno.");
            }
            catch (Exception ex )
            {
                _logger.Error(ex);
            }
        }

        private List<base_transacciones> ObtenerTransaccionesParaExtornar(string[] estado)
        {
            _logger.Info("Inicio de la carga de transacciones alignet...");
            string sql = "Select * from base_transacciones_alignet where ESTADO_OPERACION in ('" + String.Join("','", estado) + "') AND  convert(datetime, dateadd(d, -" + DIAS_PROCESO_EXTORNO + ", GETDATE()), 103) <= convert(datetime, FECHA_PEDIDO, 103) ";
            var transacciones = GetData(sql, CommandType.Text);
            _logger.Info(string.Format("Se obtuvieron {0} transacciones...", transacciones.Count));
            return transacciones;
        }
        public void ActualizarTransaccionesAlignet(long id,string estado_operacion,string estado_transaccion)
        {
            _logger.Info("Actualiza carga de transacciones alignet...");
            string sql = "UPDATE base_transacciones_alignet SET ESTADO_OPERACION = '"+ estado_operacion + "',ESTADO_TRANSACCION = '" + estado_transaccion + "', FECHA_OPERACION = GetDate() WHERE ID = '" + id.ToString() + "'";  
            UpdateData(sql, CommandType.Text);
        }
        public List<base_transacciones> GetData(string sqlText, CommandType commandType = CommandType.StoredProcedure)
        {
            var dbcon = new SqlConnection(csAlignet);
            using (var dSqlServer = new BO.Integracion.Siebel.DataSqlServer<base_transacciones>(new Db(dbcon)))
            {
                var dt = dSqlServer.Get(sqlText, commandType);
                return dt.ToList();
            }
        }
        public void UpdateData(string sqlText, CommandType commandType = CommandType.StoredProcedure)
        {
            var dbcon = new SqlConnection(csAlignet);
            using (var dSqlServer = new BO.Integracion.Siebel.DataSqlServer<base_transacciones>(new Db(dbcon)))
            {
                dSqlServer.ExecuteNonQuery(sqlText, commandType);
            }
        }
        private string GET_RESULT_CONSULTA_ALIGNET(string operationNumber) 
        {
            string TEXT = IDACQUIRER + IDCOMMERCE + operationNumber + AUTHORIZATION_CONSULTA;
            string PURCHASEVERIFICATION = SHA512(TEXT);
            string DATA = "{\"idAcquirer\":\"" + IDACQUIRER + "\",\"idCommerce\":\"" + IDCOMMERCE + "\",\"operationNumber\":\"" + operationNumber + "\",\"purchaseVerification\":\"" + PURCHASEVERIFICATION + "\"}";
            string responseData = "";
            Newtonsoft.Json.Linq.JObject objreversa;
            //Console.WriteLine(DATA);
            try
            {
                System.Net.WebRequest wrequest = System.Net.WebRequest.Create(APICONSULTA_ALIGNET);
                wrequest.ContentType = "application/json";
                wrequest.Method = "POST";
                using (var streamWriter = new System.IO.StreamWriter(wrequest.GetRequestStream()))
                {
                     streamWriter.Write(DATA);
                }              
                System.Net.WebResponse wresponse = wrequest.GetResponse();
                System.IO.StreamReader responseStream = new  System.IO.StreamReader(wresponse.GetResponseStream());
                responseData = responseStream.ReadToEnd();
                objreversa = Newtonsoft.Json.Linq.JObject.Parse(responseData);
                objreversa.TryGetValue("result", out Newtonsoft.Json.Linq.JToken result);
                responseData = result.ToString().Trim();
            }
            catch (Exception ex) {
                responseData = "ERROR:" + ex.Message;
            }

            return responseData;
        }
        private string REVERSE_ALIGNET(string operationNumber)
        {
            string DATA = ""; // "{\"idAcquirer\":\"" + IDACQUIRER + "\",\"idCommerce\":\"" + IDCOMMERCE + "\",\"operationNumber\":\"" + operationNumber + "\",\"purchaseVerification\":\"" + PURCHASEVERIFICATION + "\"}";
            string responseData = "";
            //Console.WriteLine(DATA);
            try
            {
                System.Net.WebRequest wrequest = System.Net.WebRequest.Create(APIREVERSE_ALIGNET + "/"+operationNumber);
                //wrequest.ContentType = "application/json";
                //wrequest.Headers.Add("Authorization", AUTHORIZATION);
                wrequest.Headers["Authorization"]= "Bearer "+AUTHORIZATION_EXTORNO;
                wrequest.Method = "DELETE"; //DELETE
                wrequest.Timeout = 100000;
                //Console.WriteLine(APIREVERSE_ALIGNET + "/" + operationNumber);

                using (var streamWriter = new System.IO.StreamWriter(wrequest.GetRequestStream()))
                {
                    streamWriter.Write(DATA);
                } 
                
                System.Net.WebResponse wresponse = wrequest.GetResponse();
                System.IO.StreamReader responseStream = new System.IO.StreamReader(wresponse.GetResponseStream());
                responseData = responseStream.ReadToEnd();
                //Console.WriteLine(responseData);
            }
            catch (Exception ex)
            {
                responseData = "{\"success\":\"false\",\"Exception\":\"" + ex.Message+"\"}";
            }

            return responseData;
        }
        public static string SHA512(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            using (var hash = System.Security.Cryptography.SHA512.Create())
            {
                var hashedInputBytes = hash.ComputeHash(bytes);
                var hashedInputStringBuilder = new System.Text.StringBuilder(128);
                foreach (var b in hashedInputBytes)
                    hashedInputStringBuilder.Append(b.ToString("X2"));
                return hashedInputStringBuilder.ToString().ToLower();
            }
        }

    }
}
