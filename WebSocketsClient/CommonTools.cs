using System;
using System.Linq;
using System.Net;
using System.Text;
using NLog;
using System.Reflection;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Configuration;
using System.IO;
using System.Collections.Generic;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace WebSocketsClient
{
    public class CommonTools
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string myGuid;

        public static void AddLog(string type, int level, string message)
        {
            try
            {
                if (level <= Convert.ToInt32(ConfigurationManager.AppSettings["LogLevel"]))
                {
                    switch (type)
                    {
                        case Constants.LOG_INFO: logger.Info(message); break;
                        case Constants.LOG_ERROR: logger.Error(message); break;
                        case Constants.LOG_TRACE: logger.Trace(message); break;
                        case Constants.LOG_DEBUG: logger.Debug(message); break;
                        default: break;
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"AddLog ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(), GetCurrentLineNumber(ex), $"AddLog ERROR:{ex}");
            }
            
        }

        public static void AddLog(string type, string name, int line, string message)
        {

            try
            {
                string fullmessage = name + " | " + line + " | " + message;

                AddLog(type, Log_Level.Error.GetHashCode(), fullmessage);
            }
            catch (Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"AddLog ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(), GetCurrentLineNumber(ex), $"AddLog ERROR:{ex}");
            }
        }

        public static int GetCurrentLineNumber(Exception ex)
        {

            var lineNumber = 0;
            const string lineSearch = ":line ";
            var index = ex.StackTrace.LastIndexOf(lineSearch);
            if (index != -1)
            {
                var lineNumberText = ex.StackTrace.Substring(index + lineSearch.Length);
                if (int.TryParse(lineNumberText, out lineNumber))
                {
                }
            }
            return lineNumber;
            //need .PBO file
            //var lineNumber = new System.Diagnostics.StackTrace(ex, true).GetFrame(0).GetFileLineNumber();
            //return lineNumber;
        }

        public static string GetCurrentMethodInfo()
        {

            var stack = new System.Diagnostics.StackTrace();
            var frame = stack.GetFrame(1);
            var method = frame.GetMethod();

            return method.DeclaringType.FullName + "." + method.Name;

        }

        public static string GetCurrentProjectName()
        {
            return Assembly.GetCallingAssembly().GetName().Name;
        }

        public static string GetLocalIPAddress()
        {
            string ipaddress = "";
            try
            {
                NetworkInterface[] allNICs = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in allNICs)
                {
                    var ipProp = nic.GetIPProperties();
                    var gwAddresses = ipProp.GatewayAddresses;
                    if (gwAddresses.Count > 0 &&
                        gwAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork) != null)
                    {
                        var localIP = ipProp.UnicastAddresses.First(d => d.Address.AddressFamily == AddressFamily.InterNetwork).Address;
                        ipaddress = localIP.ToString();
                        break;
                    }
                }

                if (ipaddress.Length == 0)
                {
                    StringBuilder ipAddressList = new StringBuilder();
                    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        foreach (IPAddressInformation ipInfo in nic.GetIPProperties().UnicastAddresses)
                        {
                            if (IPAddress.IsLoopback(ipInfo.Address) == false
                                && ipInfo.Address.AddressFamily != AddressFamily.InterNetworkV6)
                            {
                                //取得IP Address
                                ipaddress = ipInfo.Address.ToString();
                                break;
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"GetLocalIPAddress ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(), GetCurrentLineNumber(ex), $"GetLocalIPAddress ERROR:{ex}");
            }
            
            return ipaddress;
        }

        public static bool IsGzipData(byte[] result)
        {
            byte ID1 = result[0];
            byte ID2 = result[1];
            byte CM = result[2];

            return (ID1 == 31 && ID2 == 139 && CM == 8) ? true : false;
        }

        /// <summary>
        /// 取得檔案內容binary
        /// </summary>
        /// <param name="fileName">檔名</param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string fileName)
        {
            byte[] buffer = null;
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)fs.Length);
            }
            return buffer;
        }
        /// <summary>
        /// 取得資料庫連線
        /// </summary>
        /// <param name="connection_str"></param>
        /// <returns></returns>

        public static string GetConnSettingWithName(string name)
        {
            if (ConfigurationManager.ConnectionStrings[name] != null)
            {
                return ConfigurationManager.ConnectionStrings[name].ToString();
            }
            else
            {
                return "";
            }
        }

        public static void PrepareDBConnectionString()
        {
            Crypto crypto = new Crypto();
            try
            {
                if (ConfigurationManager.ConnectionStrings["DBConnCom"] != null &&
                !string.IsNullOrEmpty(ConfigurationManager.ConnectionStrings["DBConnCom"].ToString()))
                {
                    Global.isGetDBConnResult = true;
                    string function = ConfigurationManager.ConnectionStrings["DBConnCom"].ToString();
                    if (!string.IsNullOrEmpty(function))
                    {
                        DBResult dbResult = GetDBConnResult(function);
                        string dbConnCode = dbResult.Code.ToString();
                        string dbConnString = dbResult.DBConnString.ToString();
                        if (dbConnCode.Equals(Constants.RC_SUCCESS))
                        {
                            Global.ConnString = dbConnString;
                            if (ConfigurationManager.ConnectionStrings["DBConnExt"] != null)
                            {
                                string dbconnext = ConfigurationManager.ConnectionStrings["DBConnExt"].ToString();
                                if (!string.IsNullOrEmpty(dbconnext))
                                {
                                    Global.ConnString += dbconnext;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Global.ConnString = crypto.AesDecryptBase64(ConfigurationManager.ConnectionStrings["MTKGateway"].ToString(), crypto.GetJWTKey());
                    Global.isGetDBConnResult = false;
                }
            }
            catch (Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"PrepareDBConnectionString ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(), GetCurrentLineNumber(ex), $"PrepareDBConnectionString ERROR:{ex}");
            }
        }


        public static DBResult GetDBConnResult(string func)
        {
            
            DBResult dbResult = new DBResult();
            try
            {
                string[] funcArray = func.Split("@");
                if (funcArray.Length < 3)
                {
                    dbResult.Code = Constants.RC_NEED_EXTERNAL_DBCONN;
                    return dbResult;
                }
                Dictionary<string, string> dbDict = new Dictionary<string, string>();
                dbDict.Add("SSL", GetConnSettingWithName("SSL"));
                dbDict.Add("LdapHost",GetConnSettingWithName("LdapHost"));
                dbDict.Add("SearchBase",GetConnSettingWithName("SearchBase"));
                dbDict.Add("SysCode",GetConnSettingWithName("SysCode"));
                dbDict.Add("CStr",GetConnSettingWithName("CStr"));

                //dll元件在windows環境，沒有指定路徑，則系統預設目錄在C:\Windows\System32路徑
                //專案設定成讀取程式啟動的路徑(AppDomain.CurrentDomain.BaseDirectory)
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, funcArray[0]);
                Assembly assembly = Assembly.Load(ReadAllBytes(path));
                //string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, funcArray[0]);
                //Assembly assembly = Assembly.LoadFrom(path);
                AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"GetDBConnResult DllPath={path}");
                Type type = assembly.GetType(funcArray[1]);        //lib class path，如：DBConnMTKLibrary.DBConn
                object[] param = new object[2];
                param[0] = Guid.NewGuid().ToString();
                param[1] = null;
                var instance = Activator.CreateInstance(type, param);
                // Expression 建立委託
                var thisObject = Expression.Constant(instance);
                var fooMethod = instance.GetType().GetMethod(funcArray[2]); //lib method，如：ProcessDBConn
                ParameterExpression[] parameter = new ParameterExpression[1];
                parameter[0] = Expression.Parameter(typeof(Dictionary<string, string>));
                var fooCall = Expression.Call(thisObject, fooMethod, parameter);
                var fooLambda = Expression.Lambda<Func<Dictionary<string, string>, Dictionary<string, object>>>(fooCall, parameter);
                var fooFunc = fooLambda.Compile();
                Dictionary<string, object> dict = fooFunc(dbDict);

                foreach (KeyValuePair<string, object> item in dict)
                {
                    if (item.Key.Equals("Code"))
                    {
                        dbResult.Code = item.Value.ToString();
                        AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"GetDBConnResult End Key:{item.Key},Value:{item.Value}");
                    }
                    else if (item.Key.Equals("DBConn"))
                    {
                        dbResult.DBConnString = item.Value.ToString();
                        //tools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{myGuid}> GetDBConnResult End Key:{item.Key},Value:{item.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"GetDBConnResult ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(),GetCurrentLineNumber(ex), $"GetDBConnResult ERROR:{ex}");
            }
            return dbResult;
        }

        /// <summary>
        /// 判斷是否要重取資料庫連線
        /// </summary>
        /// <param name="errno"></param>
        /// <returns></returns>
        //public bool IsReloadDBConnection(int errno)
        //{
        //    if (errno >= 18301 && errno <= 18499)
        //    {
        //        /*
        //         * 再執行DBConn.dll，取得dbconnectio string，比對記憶體是否一樣
        //         * Y：直接回錯
        //         * N：再打一次sqlconnection
        //         */
        //        //判斷是否走外部取得DB連線
        //        if (ConfigurationManager.ConnectionStrings["DBConnCom"] != null)
        //        {
        //            string function = ConfigurationManager.ConnectionStrings["DBConnCom"].ToString();
        //            if (!string.IsNullOrEmpty(function))
        //            {
        //                DBResult dbResult = CommonTools.GetDBConnResult(function);
        //                string dbConnCode = dbResult.Code.ToString();
        //                string dbConnString = dbResult.DBConnString.ToString();
        //                if (dbConnCode.Equals(Constants.RC_SUCCESS))
        //                {
        //                    if (ConfigurationManager.ConnectionStrings["DBConnExt"] != null)
        //                    {
        //                        string dbconnext = ConfigurationManager.ConnectionStrings["DBConnExt"].ToString();
        //                        if (!string.IsNullOrEmpty(dbconnext))
        //                        {
        //                            dbConnString += dbconnext;
        //                        }
        //                    }
        //                    if (!dbConnString.Equals(Global.ConnString))
        //                    {
        //                        return true;
        //                    }
        //                    else
        //                    {
        //                        return false;
        //                    }
        //                }
        //                else
        //                {
        //                    return false;
        //                }
        //            }
        //            else
        //            {
        //                return false;
        //            }
        //        }
        //    }
        //    return false;
        //}
        /// <summary>
        /// 檢查JSON格式
        /// </summary>
        /// <param name="strInput">json string</param>
        /// <returns></returns>
        public bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            try
            {
                var obj = JObject.Parse(strInput);
                return true;
            }
            catch (Exception) //some other exception
            {
                // 因為LOG_ERROR會再寫syslog，所以驗證json格式失敗，大部份可能是資料庫異常的，所以改成記LOG_DEBUG。
                //log.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{MyGuid}> IsValidJson Error");
                //log.AddErrLog(Constants.LOG_ERROR, utility.GetCurrentMethodInfo(), utility.GetCurrentLineNumber(ex), $"<{MyGuid}> IsValidJson Error:{ex}", ex.Message);
                return false;
            }
        }
        //public static void AddJobNameToDictionary(string jobname, CancellationTokenSource cts)
        //{
        //    string key = $"{jobname}_{Guid.NewGuid()}";
        //    Global.CTSDictionary.Add(key, cts);
        //    Global.InsertionOrder.AddLast(key); // 將鍵添加到插入顺序鏈表的末尾
        //}
        //public static void RemoveJobNameFromDictionary(string jobname)
        //{
        //    CancellationTokenSource cts = null;
        //    LinkedListNode<string> node = Global.InsertionOrder.First;
        //    while (node != null)
        //    {
        //        if (node.Value.StartsWith(jobname + "_"))
        //        {
        //            string key = node.Value;
        //            Global.CTSDictionary.Remove(key);
        //            Global.InsertionOrder.Remove(node); // 从插入顺序链表中移除
        //            cts.Cancel();
        //            break; // 一旦找到并移除了，就可以停止了
        //        }
        //        node = node.Next;
        //    }
        //}
        public static void RemoveJobNameFromDictionary(string myguid, string jobname)
        {
            try
            {
                CancellationTokenSource cts;
                if (Global.CTSDictionary.TryGetValue(jobname, out cts))
                {
                    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Info.GetHashCode(), $"<{myguid}> RemoveJobNameFromDictionary Cancel Job " + jobname);                    
                    Global.CTSDictionary.Remove(jobname);
                    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Info.GetHashCode(), $"<{myguid}> RemoveJobNameFromDictionary Cancel Job CTSDictionary.Remove");
                    cts.Cancel();
                }
            }
            catch (Exception ex)
            {
                AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"RemoveJobNameFromDictionary ERROR:{ex.Message}");
                AddLog(Constants.LOG_ERROR, GetCurrentMethodInfo(), GetCurrentLineNumber(ex), $"RemoveJobNameFromDictionary ERROR:{ex}");
            }
        }
    }
}
