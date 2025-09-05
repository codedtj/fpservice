using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FPService
{
    public class Service1 : IService1
    {
        #region Static extern methods
        [DllImport("libzkfp.dll")]
        private static extern int ZKFPM_Init();

        [DllImport("libzkfp.dll")]
        private static extern int ZKFPM_MatchFinger(IntPtr hDbCache, IntPtr fpTemplate1, uint cbTemplate1,
            IntPtr fpTemplate2, uint cbTemplate2);

        [DllImport("libzkfp.dll")]
        private static extern IntPtr ZKFPM_CreateDBCache();

        [DllImport(@"C:\htdocs\lib\sharplib\ZEngine\ZEngine.dll")]
        private static extern int IdentifyUser(string candidateTmp, string server, string user,
         string password, string db, string query, string idFieldName,
         string printFieldName, int threadsCount);
        #endregion
        public string Hello()
        {
            return "Hello";
        }

        #region ZEngine
        public int ZkExternIdentifier(string fp1, string server, string user, string password, string db,
             string query, string idFieldName, string printFieldName, int threadsCount)
        {
            try
            {
                server = string.IsNullOrWhiteSpace(server) ? "localhost" : server;
                user = string.IsNullOrWhiteSpace(server) ? "root" : user;
                idFieldName = string.IsNullOrWhiteSpace(idFieldName) ? "id" : idFieldName;
                threadsCount = 0 < threadsCount ? threadsCount : 2;

                return IdentifyUser(fp1, server, user, password, db, query, idFieldName,
                                          printFieldName, threadsCount);
            }
            catch(Exception e)
            {
                return -10;
            }
        }
        
        public int ZkIdentifier(string connectionString, string query, string printFieldName,
            string idFieldName, int rate, string print)
        {
            try
            {
                //Connect to db
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();
                MySqlCommand cmd = new MySqlCommand(query, connection);
                MySqlDataReader dataReader = cmd.ExecuteReader();

                //Connect to device
                ZKFPM_Init();
                var mDbHandle = ZKFPM_CreateDBCache();
                if (IntPtr.Zero == mDbHandle)
                    return -1;
                var candidatePrint = Convert.FromBase64String(print);
                int userId = 0;

                while (dataReader.Read())
                {
                    var dbPrint = Convert.FromBase64String(dataReader[printFieldName].ToString());  
                    if (ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(candidatePrint, 0),
                    (uint)candidatePrint.Length, Marshal.UnsafeAddrOfPinnedArrayElement(dbPrint, 0), 
                    (uint)dbPrint.Length) >= rate)
                    {
                       userId = (int)dataReader[idFieldName];
                       break;
                    }
                }

                dataReader.Close();
                dataReader.Dispose();
                connection.Close();
                connection.Dispose();

                return userId;
            }
            catch (Exception e)
            {      
                return -10;
            }
        }

        public int MatchPrints(string fp1, string fp2)
        {
            ZKFPM_Init();
            var mDbHandle = ZKFPM_CreateDBCache();
            if (IntPtr.Zero != mDbHandle)
            {
                var tmp1 = Convert.FromBase64String(fp1);
                var tmp2 = Convert.FromBase64String(fp2);
                var res = ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(tmp1, 0),
                    (uint)tmp1.Length, Marshal.UnsafeAddrOfPinnedArrayElement(tmp2, 0), (uint)tmp2.Length);
                return res;
            }
            return -1;
        }

        public int ZkIdentifierWide(string print, string query, string connectionString, int rate)
        {
            //Connect to db
            MySqlConnection connection = new MySqlConnection(connectionString);
            MySqlCommand cmd = new MySqlCommand(query, connection);
            connection.Open();
            cmd.ExecuteNonQuery();
            MySqlDataAdapter dataAdapter = new MySqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            dataAdapter.Fill(dt);

            var myData = dt.Select();
            int userId = 0;
            var tmp1 = Convert.FromBase64String(print);
            ZKFPM_Init();
            var mDbHandle = ZKFPM_CreateDBCache();
            if (IntPtr.Zero == mDbHandle)
                return -1;
            CancellationTokenSource cts = new CancellationTokenSource();
            ParallelOptions po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            //Parallel
            try
            {
                Parallel.For(0, myData.Length, po, i =>
                {                   
                    var tmp2 = Convert.FromBase64String(myData[i].ItemArray[2].ToString());
                    if (ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(tmp1, 0),
                    (uint)tmp1.Length, Marshal.UnsafeAddrOfPinnedArrayElement(tmp2, 0), (uint)tmp2.Length) >= rate)
                    {
                        userId = (int)myData[i].ItemArray[1];
                        cts.Cancel();
                    }
                    po.CancellationToken.ThrowIfCancellationRequested();
                });
            }
            catch (Exception e)
            {
                // ignored
            }

            cts.Dispose();
            connection.Close();
            connection.Dispose();
            return userId;
        }
        #endregion
    }
}
