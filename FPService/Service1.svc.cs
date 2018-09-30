using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace FPService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        [DllImport(@"C:\Users\sulton\source\repos\ZKEngine\x64\Debug\ZEngine.dll")]
        private extern static int IdentifyUser(string candidateTmp, string server, string user,
         string password, string db, string query, string idFieldName,
         string printFieldName, int threadsCount);

        public string Hello()
        {
            return "Hello";
        }

        public int Identify(string fp1, string server, string user, string password, string db,
             string query, string idFieldName, string printFieldName, int threadsCount)
        {
            server = string.IsNullOrWhiteSpace(server) ? "localhost" : server;
            user = string.IsNullOrWhiteSpace(server) ? "root" : user;
            idFieldName = string.IsNullOrWhiteSpace(idFieldName) ? "id" : idFieldName;
            threadsCount = 0 < threadsCount ? threadsCount : 2;

            return IdentifyUser(fp1, server, user, password, db, query, idFieldName,
                                      printFieldName, threadsCount);
        }
    }
}
