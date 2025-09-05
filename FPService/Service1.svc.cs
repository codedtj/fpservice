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
            var cid = Guid.NewGuid().ToString();
            Logger.Info($"[{cid}] Hello: start");
            try
            {
                var message = "Hello";
                Logger.Info($"[{cid}] Hello: success -> '{message}'");
                return message;
            }
            catch (Exception ex)
            {
                Logger.Error($"[{cid}] Hello: unexpected error", ex);
                throw new FaultException<string>("Unexpected server error: " + ex.Message);
            }
        }

        #region ZEngine
        
        public int ZkIdentifier(string connectionString, string query, string printFieldName,
            string idFieldName, int rate, string print)
        {
            var cid = Guid.NewGuid().ToString();
            Logger.Info($"[{cid}] ZkIdentifier: start");
            Logger.LogConnectionString(connectionString);
            Logger.Debug($"[{cid}] query='{query}', printFieldName='{printFieldName}', idFieldName='{idFieldName}', rate={rate}");
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    Logger.Debug($"[{cid}] opening DB connection");
                    connection.Open();
                    using (var cmd = new MySqlCommand(query, connection))
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        Logger.Debug($"[{cid}] DB reader opened; initializing fingerprint engine");
                        ZKFPM_Init();
                        var mDbHandle = ZKFPM_CreateDBCache();
                        if (IntPtr.Zero == mDbHandle)
                            throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");
                        Logger.Debug($"[{cid}] fingerprint cache created");
                        var candidatePrint = Convert.FromBase64String(print);
                        int userId = 0;
                        int rows = 0;

                        while (dataReader.Read())
                        {
                            rows++;
                            var dbPrint = Convert.FromBase64String(dataReader[printFieldName].ToString());
                            var score = ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(candidatePrint, 0),
                                (uint)candidatePrint.Length, Marshal.UnsafeAddrOfPinnedArrayElement(dbPrint, 0), (uint)dbPrint.Length);
                            if (score >= rate)
                            {
                                userId = (int)dataReader[idFieldName];
                                Logger.Info($"[{cid}] match found: userId={userId}, score={score}, rowsChecked={rows}");
                                break;
                            }
                        }
                        Logger.Info($"[{cid}] finished scanning rowsChecked={rows}, userId={userId}");
                        return userId;
                    }
                }
            }
            catch (FormatException fe)
            {
                Logger.Error($"[{cid}] invalid Base64 while identifying", fe);
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (MySqlException me)
            {
                Logger.Error($"[{cid}] database error", me);
                throw new FaultException<string>("Database error: " + me.Message);
            }
            catch (Exception e)
            {
                Logger.Error($"[{cid}] unexpected error", e);
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }

        public int MatchPrints(string fp1, string fp2)
        {
            var cid = Guid.NewGuid().ToString();
            Logger.Info($"[{cid}] MatchPrints: start");
            try
            {
                Logger.Debug($"[{cid}] initializing fingerprint engine");
                ZKFPM_Init();
                var mDbHandle = ZKFPM_CreateDBCache();
                if (IntPtr.Zero == mDbHandle)
                    throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");

                Logger.Debug($"[{cid}] decoding Base64 inputs");
                var tmp1 = Convert.FromBase64String(fp1);
                var tmp2 = Convert.FromBase64String(fp2);
                var res = ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(tmp1, 0),
                    (uint)tmp1.Length, Marshal.UnsafeAddrOfPinnedArrayElement(tmp2, 0), (uint)tmp2.Length);
                Logger.Info($"[{cid}] match score={res}");
                return res;
            }
            catch (FormatException fe)
            {
                Logger.Error($"[{cid}] invalid Base64 in MatchPrints", fe);
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (Exception e)
            {
                Logger.Error($"[{cid}] unexpected error in MatchPrints", e);
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }

        public int ZkIdentifierWide(string print, string query, string connectionString, int rate)
        {
            var cid = Guid.NewGuid().ToString();
            Logger.Info($"[{cid}] ZkIdentifierWide: start");
            Logger.LogConnectionString(connectionString);
            Logger.Debug($"[{cid}] query='{query}', rate={rate}");
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                using (var cmd = new MySqlCommand(query, connection))
                {
                    Logger.Debug($"[{cid}] opening DB connection");
                    connection.Open();
                    Logger.Debug($"[{cid}] executing query to populate DataTable");
                    cmd.ExecuteNonQuery();
                    using (var dataAdapter = new MySqlDataAdapter(cmd))
                    using (var dt = new DataTable())
                    {
                        dataAdapter.Fill(dt);
                        Logger.Info($"[{cid}] data loaded: rows={dt.Rows.Count}");

                        var myData = dt.Select();
                        int userId = 0;
                        Logger.Debug($"[{cid}] decoding candidate print (Base64)");
                        var tmp1 = Convert.FromBase64String(print);
                        Logger.Debug($"[{cid}] initializing fingerprint engine");
                        ZKFPM_Init();
                        var mDbHandle = ZKFPM_CreateDBCache();
                        if (IntPtr.Zero == mDbHandle)
                            throw new FaultException<string>("Fingerprint engine initialization failed: ZKFPM_CreateDBCache returned null handle.");
                        Logger.Debug($"[{cid}] fingerprint cache created; starting parallel match");
                        using (var cts = new CancellationTokenSource())
                        {
                            var po = new ParallelOptions
                            {
                                CancellationToken = cts.Token,
                                MaxDegreeOfParallelism = Environment.ProcessorCount
                            };

                            int checkedRows = 0;
                            try
                            {
                                Parallel.For(0, myData.Length, po, i =>
                                {
                                    var tmp2 = Convert.FromBase64String(myData[i].ItemArray[2].ToString());
                                    var score = ZKFPM_MatchFinger(mDbHandle, Marshal.UnsafeAddrOfPinnedArrayElement(tmp1, 0),
                                        (uint)tmp1.Length, Marshal.UnsafeAddrOfPinnedArrayElement(tmp2, 0), (uint)tmp2.Length);
                                    if (score >= rate)
                                    {
                                        userId = (int)myData[i].ItemArray[1];
                                        Logger.Info($"[{cid}] match found: userId={userId}, score={score}, rowIndex={i}");
                                        cts.Cancel();
                                    }
                                    Interlocked.Increment(ref checkedRows);
                                    po.CancellationToken.ThrowIfCancellationRequested();
                                });
                            }
                            catch (OperationCanceledException)
                            {
                                Logger.Debug($"[{cid}] parallel search cancelled due to match");
                            }
                            Logger.Info($"[{cid}] finished parallel matching; checkedRows={checkedRows}, userId={userId}");
                        }

                        return userId;
                    }
                }
            }
            catch (FormatException fe)
            {
                Logger.Error($"[{cid}] invalid Base64 in ZkIdentifierWide", fe);
                throw new FaultException<string>("Invalid fingerprint template format (Base64 expected): " + fe.Message);
            }
            catch (MySqlException me)
            {
                Logger.Error($"[{cid}] database error in ZkIdentifierWide", me);
                throw new FaultException<string>("Database error: " + me.Message);
            }
            catch (Exception e)
            {
                Logger.Error($"[{cid}] unexpected error in ZkIdentifierWide", e);
                throw new FaultException<string>("Unexpected server error: " + e.Message);
            }
        }
        #endregion
    }
}
