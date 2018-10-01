using System.Runtime.InteropServices;

namespace FPService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class FPService : IService
    {
        [DllImport(@"C:\htdocs\lib\sharplib\ZEngine\ZEngine.dll")]
        private static extern int IdentifyUser(string candidateTmp, string server, string user,
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
