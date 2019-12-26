using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cliente360.Integracion.Alignet
{
    class Program
    {
        static void Main(string[] args)
        {
            var cadenaConexion = System.Configuration.ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;

            new IntegracionAlignetManager(cadenaConexion).Start();
            //Console.Read();
        }
    }
}
