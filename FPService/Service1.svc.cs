using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;

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

        #endregion
        public string Hello()
        {
            return "Hello";
        }

        #region ZEngine
        
        public int ZkIdentifier(string connectionString, string query, string printFieldName,
            string idFieldName, int rate, string print)
        {
            try
            {
                //Connect to db
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = new MySqlCommand(query, connection))
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        //Connect to device
                        ZKFPM_Init();
                        var mDbHandle = ZKFPM_CreateDBCache();
                        if (IntPtr.Zero == mDbHandle)
                            throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");
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

                        return userId;
                    }
                }
            }
            catch (FormatException fe)
            {
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (MySqlException me)
            {
                throw new FaultException<string>("Database error: " + me.Message);
            }
            catch (Exception e)
            {      
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }

        public int MatchPrints(string fp1, string fp2)
        {
            try
            {
                ZKFPM_Init();
                var mDbHandle = ZKFPM_CreateDBCache();
                if (IntPtr.Zero == mDbHandle)
                    throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");

                var tmp1 = Convert.FromBase64String(fp1);
                var tmp2 = Convert.FromBase64String(fp2);
                var res = ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(tmp1, 0),
                    (uint)tmp1.Length, Marshal.UnsafeAddrOfPinnedArrayElement(tmp2, 0), (uint)tmp2.Length);
                return res;
            }
            catch (FormatException fe)
            {
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (Exception e)
            {
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }

        public int ZkIdentifierWide(string print, string query, string connectionString, int rate)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                using (var cmd = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    cmd.ExecuteNonQuery();
                    using (var dataAdapter = new MySqlDataAdapter(cmd))
                    using (var dt = new DataTable())
                    {
                        dataAdapter.Fill(dt);

                        var myData = dt.Select();
                        int userId = 0;
                        var tmp1 = Convert.FromBase64String(print);
                        ZKFPM_Init();
                        var mDbHandle = ZKFPM_CreateDBCache();
                        if (IntPtr.Zero == mDbHandle)
                            throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");
                        using (var cts = new CancellationTokenSource())
                        {
                            var po = new ParallelOptions
                            {
                                CancellationToken = cts.Token,
                                MaxDegreeOfParallelism = Environment.ProcessorCount
                            };

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
                            catch (OperationCanceledException)
                            {
                                // normal when match found
                            }
                        }

                        return userId;
                    }
                }
            }
            catch (FormatException fe)
            {
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (MySqlException me)
            {
                throw new FaultException<string>("Database error: " + me.Message);
            }
            catch (Exception e)
            {
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }
        #endregion
    }
}
