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
            server = string.IsNullOrWhiteSpace(server) ? "localhost" : server;
            user = string.IsNullOrWhiteSpace(server) ? "root" : user;
            idFieldName = string.IsNullOrWhiteSpace(idFieldName) ? "id" : idFieldName;
            threadsCount = 0 < threadsCount ? threadsCount : 2;

            return IdentifyUser(fp1, server, user, password, db, query, idFieldName,
                                      printFieldName, threadsCount);
        }

        public int ZkIdentifier(string connectionString, string query, string printFieldName,
            int rate, string print)
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
                if (IntPtr.Zero != mDbHandle)
                {
                    return -1;
                }

                var candidatePrint = Convert.FromBase64String(print);
                int userId = 0;

                while (dataReader.Read())
                {
                    var dbPrint = Convert.FromBase64String(dataReader[printFieldName].ToString());
                    if (ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(candidatePrint, 0),
                    (uint)candidatePrint.Length, Marshal.UnsafeAddrOfPinnedArrayElement(dbPrint, 0),
                    (uint)dbPrint.Length) >= rate)
                    {
                        userId = (int)dataReader["userid"];
                        break;
                    }
                }

                dataReader.Close();
                dataReader.Dispose();
                connection.Close();
                connection.Dispose();

                return userId;
            }
            catch
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
        #endregion

        #region AEngine
        private int _id;
        public int Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (value > 0)
                {
                    _id = value;
                }
            }
        }

        #region standart
        public int AIdentifier(string print, string[] queries, string connectionString,
            string printFieldName, string idFieldName, int rate)
        {
            var mres = new ManualResetEvent[queries.Length];

            for (var i = 0; i < mres.Length; i++)
            {
                mres[i] = new ManualResetEvent(false);
            }

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            for (var i = 0; i < queries.Length; i++)
            {
                var idx = i;
                Task.Factory.StartNew(state =>
                {
                    FpIdentifyer(queries[idx], connectionString, print, printFieldName,
                        idFieldName, token, source, rate);
                    mres[idx].Set();
                }, $"Task{idx}", token);
            }

            WaitHandle.WaitAll(mres.Select(x => x).ToArray());

            return Id;
        }

        private void FpIdentifyer(string query, string connectionString, string print,
            string printFieldName, string idFieldName, CancellationToken token,
            CancellationTokenSource source, int rate)
        {
            try
            {
                //Connect to db
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();
                MySqlCommand cmd = new MySqlCommand(query, connection);
                MySqlDataReader dataReader = cmd.ExecuteReader();

                var matcher = new AEngine.Matcher();

                while (dataReader.Read())
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    var dbPrint = dataReader[printFieldName].ToString();
                    if (matcher.Match(print, dbPrint) >= rate)
                    {
                        var userId = (int)dataReader[idFieldName];
                        if (userId > 0)
                        {
                            Id = userId;
                            source.Cancel();
                        }
                    }
                }

                dataReader.Close();
                dataReader.Dispose();
                connection.Close();
                connection.Dispose();
            }
            catch (Exception e)
            {
                Id = -1;
            }
        }
        #endregion

        #region wide identifier
        public int AIdentifierWide(string print, string query, string connectionString, int rate)
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

            CancellationTokenSource cts = new CancellationTokenSource();
            ParallelOptions po = new ParallelOptions();
            po.CancellationToken = cts.Token;
            po.MaxDegreeOfParallelism = Environment.ProcessorCount;

            //Parallel
            try
            {
                Parallel.For(0, myData.Length, po, i =>
                {
                    var matcher = new AEngine.Matcher();
                    var dbPrint = myData[i].ItemArray[2].ToString();
                    if (matcher.Match(print, dbPrint) >= rate)
                    {
                        userId = (int)myData[i].ItemArray[1];
                    }
                    po.CancellationToken.ThrowIfCancellationRequested();
                });
            }
            catch (Exception e)
            {
                cts.Dispose();
            }

            connection.Close();
            connection.Dispose();
            return userId;
        }
        #endregion
        #endregion

    }
}
