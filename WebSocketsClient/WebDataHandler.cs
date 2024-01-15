using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using Microsoft.AspNetCore.Http;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using MTKSecurityLibrary;

namespace WebSocketsClient
{
    public class WebDataHandler
    {
        public Crypto crypto = new Crypto();
        public string myGuid;
        public static MTKDBServer dbserver = new MTKDBServer();
        public static Timer timer;
        public WebDataHandler(string myguid = null)
        {
            myGuid = myguid ?? Guid.NewGuid().ToString();
            
        }
        #region ProcessingJob
        /// <summary>
        /// 當server_upd.action_type=JOB_Run，呼叫sql執行相關sp job程式
        /// </summary>
        /// <param name="apiname"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static void ProcessingJob(string apiname, int timeout, string guid)
        {
            string returnCode;
            string jobResult;
            JObject objResult;
            try
            {
                WebDataHandler webDataHandler = new WebDataHandler(guid);
                //CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> ProcessingJob Start action = {apiname}");
                while (true)
                {
                    var jobAsync  = webDataHandler.DoPostNonCompressAsync(apiname, null, null, "", Global.ConnString, "", HttpMethods.Post, timeout);
                    jobResult = jobAsync.Result;
                    /*
                     * 判斷returnCode
                     * 0000：成功，需要重覆執行(停10毫秒)
                     * 0230/0410/0900/其他：讀取結束
                     */
                    objResult = JObject.Parse(jobResult);
                    returnCode = objResult.SelectToken("returnCode").ToString();
                    if (returnCode == Constants.RC_SUCCESS)
                    {
                        //CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> ProcessingJob action = {apiname},returnCode = {returnCode}");
                        Thread.Sleep(10);
                    }
                    else if (returnCode == Constants.RC_MULTI_REQUEST)
                    {
                        break;
                    }
                    else if (returnCode == Constants.RC_NODATA)
                    {
                        break;
                    }
                    else if (returnCode == Constants.RC_DATABASE_EXCEPTION)
                    {
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> ProcessingJob action = {apiname},returnCode = {returnCode}");
            }
            catch (SqlException ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{guid}> ProcessingJob DB Error:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{guid}> ProcessingJob action = {apiname}, DB Error:{ex}");
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{guid}> ProcessingJob Error:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{guid}> ProcessingJob action = {apiname}, Error:{ex}");
            }
            return;
        }
        #endregion
        public async Task<string> DoPostNonCompressAsync(string action, string postdata, Dictionary<string, string> dictparam, string access_token, string dbconn, string apipath, string method, int timeout = 0)
        {
            string result = "{\"returnCode\":\"0900\"}";
            CancellationTokenSource cts = new CancellationTokenSource();

            CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync API={action}");
            SqlConnection connection = null;
            try
            {
                Global.CTSDictionary[action] = cts;
                SqlCommand command = null;
                string[] actionArray = action.Split(".");
                string apiname = "";
                string dbname = "";
                if (actionArray.Length == 3)      // 格式：[db].[schema].[t-sql]
                {
                    apiname = actionArray[actionArray.Length - 1];
                    dbname = actionArray[0];
                }
                else if (actionArray.Length == 2) // 格式：[schema].[t-sql]
                {
                    apiname = actionArray[actionArray.Length - 1];
                    dbname = "";
                }
                else
                {
                    apiname = actionArray[0];
                    dbname = "";
                }
                SP_ParameterDetail paramDetail = DoSystemStoredProcedureOutParams(apipath, apiname, dbconn, dbname);
                if (!paramDetail.return_code.Equals(Constants.RC_SUCCESS))
                {
                    return await Task.Run(() => paramDetail.return_code);
                }
                dbserver.dbconn = dbconn;
                connection = dbserver.Connection();
                command = new SqlCommand(action, connection);

                command.Parameters.Clear();
                command.CommandType = CommandType.StoredProcedure;
                if (action.Equals("SpPushSystemBrokerAgentInfo") ||
                    action.Equals("SpPushSystemBrokerToDo") ||
                    action.Equals("SpPushSystemJobToCancel"))
                {
                    command.CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommand_Timeout"] ?? "30");
                }
                else
                {
                    command.CommandTimeout = (timeout > 0) ? timeout : Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommand_JobRun_Timeout"] ?? "1800");
                }
                if (dictparam != null)
                {
                    foreach (var item in dictparam)
                    {
                        string pname = item.Key;
                        string pvalue = item.Value;
                        command.Parameters.Add(pname, SqlDbType.VarChar).Value = pvalue;
                    }
                }
                if (!string.IsNullOrEmpty(postdata) && !postdata.Equals("{}"))//如： SpPushBackendInitial
                {
                    command.Parameters.Add("@postdata", SqlDbType.NVarChar).Value = postdata;
                    //commonTools.addLog(Constants.LOG_DEBUG, $"<{myGuid}> Add Post : PostData={postdata}");
                }
                if (!access_token.Equals(""))
                {
                    command.Parameters.Add("@access_token", SqlDbType.VarChar).Value = access_token;
                }
                //設定輸出參數
                if (paramDetail != null)
                {
                    foreach (SP_Parameter detail in paramDetail.rows)
                    {
                        SqlParameter sqlParameter = new SqlParameter();
                        sqlParameter.ParameterName = detail.pname;
                        sqlParameter.SqlDbType = (SqlDbType)Enum.Parse(typeof(SqlDbType), detail.datatype, true);
                        sqlParameter.Size = detail.size;
                        sqlParameter.Direction = ParameterDirection.Output;
                        command.Parameters.Add(sqlParameter);
                    }
                }
                connection.Open();
                /*
                    * response內容，兩類型態：
                    * 1.BLOB：包含有壓縮、無壓縮 => ExecuteReader 取得
                    * 2.@result => getSystemStoredProcedureOutParamsValue 取得
                */
                if (action.Equals("SpPushSystemBrokerAgentInfo") ||
                    action.Equals("SpPushSystemBrokerToDo") ||
                    action.Equals("SpPushSystemJobToCancel"))
                {
                    var asyncResult = await command.ExecuteScalarAsync(cts.Token);
                    byte[] byteResult = (byte[])asyncResult;
                    if (byteResult != null)
                    {
                        byteResult = CommonTools.IsGzipData(byteResult) ? crypto.Decompress(byteResult) : byteResult;
                        char[] chars = new char[byteResult.Length / sizeof(char)];
                        Buffer.BlockCopy(byteResult, 0, chars, 0, byteResult.Length);
                        result = new string(chars);
                    }
                    else
                    {
                        result = GetSystemStoredProcedureOutParamsValue(command, paramDetail, action);
                    }
                }
                else
                {
                    await command.ExecuteNonQueryAsync(cts.Token);
                    //針對JOB_RUN的sp，只取@result結果即可
                    result = GetSystemStoredProcedureOutParamsValue(command, paramDetail, action);
                }
                dbserver.DisConnection(connection);
            }
            catch (OperationCanceledException ex)
            {
                dbserver.DisConnection(connection);
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync OperationCanceledException Error:{ex.Message}");
            }
            catch (SqlException ex)
            {
                dbserver.DisConnection(connection);
                SqlErrorCollection err = ex.Errors;
                int errno = Convert.ToInt32(err[0].Number);
                //無法連接到資料庫的錯誤
                if (errno == 2)
                {
                    Global.BrokerState = false;
                }

                if (ex.Message.Contains("Operation cancelled by user"))
                {
                    // 這是正常的取消操作
                    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync \nRequest :serviceName={action} Cancel Error: {ex.Message}");
                }
                else
                {
                    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync \nRequest :serviceName={action} Database Error: {ex.Message}");
                    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync \nRequest={postdata}\nToken={access_token}\nResponse={result}");
                    CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoPostNonCompressAsync Request :serviceName={action} Database Error: {ex}");
                }
            }
            catch (Exception ex)
            {
                dbserver.DisConnection(connection);
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync Request :serviceName={action} Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompressAsync \nRequest={postdata}\nToken={access_token}\nResponse={result}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoPostNonCompressAsync Request :serviceName={action} Error: {ex}");
            }
            //return result;
            //return Task.Run(() => result);
            return await Task.Run(() => result);
        }
        #region Database
        /// <summary>
        /// 執行共用API電文
        /// </summary>
        /// <param name="action">API名稱</param>
        /// <param name="postdata">Post Body Json內容</param>
        /// <param name="dictparam">Get Parameter內容</param>
        /// <param name="access_token">安控token</param>
        /// <param name="dbconn">資料庫連線設定</param>
        /// <param name="dbname">資料庫代碼</param>
        /// <returns>回傳執行Json結果</returns>
        public string DoPostNonCompress(string action, string postdata, Dictionary<string, string> dictparam, string access_token, string dbconn, string apipath, string method, int timeout = 0)
        {
            string result = "{\"returnCode\":\"0900\"}";
            CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{myGuid}> DoPostNonCompress API={action}");
            SqlConnection connection = null;
            try
            {
                SqlCommand command = null;
                string[] actionArray = action.Split(".");
                string apiname = "";
                string dbname = "";
                if (actionArray.Length == 3)      // 格式：[db].[schema].[t-sql]
                {
                    apiname = actionArray[actionArray.Length - 1];
                    dbname = actionArray[0];
                }
                else if (actionArray.Length == 2) // 格式：[schema].[t-sql]
                {
                    apiname = actionArray[actionArray.Length - 1];
                    dbname = "";
                }
                else
                {
                    apiname = actionArray[0];
                    dbname = "";
                }
                SP_ParameterDetail paramDetail = DoSystemStoredProcedureOutParams(apipath, apiname, dbconn, dbname);
                if (!paramDetail.return_code.Equals(Constants.RC_SUCCESS))
                {
                    return paramDetail.return_code;
                }
                dbserver.dbconn = dbconn;
                connection = dbserver.Connection();
                command = new SqlCommand(action, connection);

                command.Parameters.Clear();
                command.CommandType = CommandType.StoredProcedure;
                if (action.Equals("SpPushSystemBrokerAgentInfo") ||
                    action.Equals("SpPushSystemBrokerToDo") ||
                    action.Equals("SpPushSystemJobToCancel"))
                {
                    command.CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommand_Timeout"] ?? "30");
                }
                else
                {
                    command.CommandTimeout = (timeout > 0) ? timeout : Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommand_JobRun_Timeout"] ?? "1800");
                }
                if (dictparam != null)
                {
                    foreach (var item in dictparam)
                    {
                        string pname = item.Key;
                        string pvalue = item.Value;
                        command.Parameters.Add(pname, SqlDbType.VarChar).Value = pvalue;
                    }
                }
                if (!string.IsNullOrEmpty(postdata) && !postdata.Equals("{}"))//如： SpPushBackendInitial
                {
                    command.Parameters.Add("@postdata", SqlDbType.NVarChar).Value = postdata;
                    //commonTools.addLog(Constants.LOG_DEBUG, $"<{myGuid}> Add Post : PostData={postdata}");
                }
                if (!access_token.Equals(""))
                {
                    command.Parameters.Add("@access_token", SqlDbType.VarChar).Value = access_token;
                }
                //設定輸出參數
                if (paramDetail != null)
                {
                    foreach (SP_Parameter detail in paramDetail.rows)
                    {
                        SqlParameter sqlParameter = new SqlParameter();
                        sqlParameter.ParameterName = detail.pname;
                        sqlParameter.SqlDbType = (SqlDbType)Enum.Parse(typeof(SqlDbType), detail.datatype, true);
                        sqlParameter.Size = detail.size;
                        sqlParameter.Direction = ParameterDirection.Output;
                        command.Parameters.Add(sqlParameter);
                    }
                }
                connection.Open();
                /*
                    * response內容，兩類型態：
                    * 1.BLOB：包含有壓縮、無壓縮 => ExecuteReader 取得
                    * 2.@result => getSystemStoredProcedureOutParamsValue 取得
                */
                if (action.Equals("SpPushSystemBrokerAgentInfo") ||
                    action.Equals("SpPushSystemBrokerToDo") ||
                    action.Equals("SpPushSystemJobToCancel"))
                {
                    byte[] byteResult = (byte[])command.ExecuteScalar();
                    if (byteResult != null)
                    {
                        byteResult = CommonTools.IsGzipData(byteResult) ? crypto.Decompress(byteResult) : byteResult;
                        char[] chars = new char[byteResult.Length / sizeof(char)];
                        Buffer.BlockCopy(byteResult, 0, chars, 0, byteResult.Length);
                        result = new string(chars);
                    }
                    else
                    {
                        result = GetSystemStoredProcedureOutParamsValue(command, paramDetail, action);
                    }
                }
                else
                {
                    var byteResult = (byte[])command.ExecuteScalar();
                    //針對JOB_RUN的sp，只取@result結果即可
                    result = GetSystemStoredProcedureOutParamsValue(command, paramDetail, action);
                }
                dbserver.DisConnection(connection);
            }
            catch (SqlException ex)
            {
                dbserver.DisConnection(connection);
                SqlErrorCollection err = ex.Errors;
                int errno = Convert.ToInt32(err[0].Number);
                //無法連接到資料庫的錯誤
                if (errno == 2)
                {
                    Global.BrokerState = false;
                }
                //result = Constants.RC_DATABASE_EXCEPTION;
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompress \nRequest :serviceName={action} Database Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompress \nRequest={postdata}\nToken={access_token}\nResponse={result}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoPostNonCompress Request :serviceName={action} Database Error: {ex}");
            }
            catch (Exception ex)
            {
                dbserver.DisConnection(connection);
                //result = Constants.RC_DATABASE_EXCEPTION;
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompress Request :serviceName={action} Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoPostNonCompress \nRequest={postdata}\nToken={access_token}\nResponse={result}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoPostNonCompress Request :serviceName={action} Error: {ex}");
            }
            return result;
        }
        /// <summary>
        /// 取得sp設定參數為output的欄位資料型態    
        /// </summary>
        /// <param name="db_api_path"></param>
        /// <param name="action"></param>
        /// <param name="dbconn"></param>
        /// <param name="dbname"></param>
        /// <returns></returns>
        private SP_ParameterDetail DoSystemStoredProcedureOutParams(string db_api_path, string action, string dbconn, string dbname)
        {
            string return_code = Constants.RC_SUCCESS;
            SqlConnection connection = null;
            //先取記憶體再取資料庫
            foreach (SP_ParameterDetail detail in Global.SpParameterList.rows)
            {
                if (detail.api_path_name.Equals(db_api_path + action))
                {
                    return detail;
                }
            }

            SP_ParameterDetail parameter = new SP_ParameterDetail();
            string sqlstatment = "";
            if (string.IsNullOrEmpty(dbname))
            {
                sqlstatment = string.Format(
                    "SELECT obj.name AS procedure_name, obj.type, " +
                    "       p.name AS parameter_name, p.is_output, " +
                    "       t.name AS type_name, p.max_length AS size " +
                    "FROM sys.objects obj " +
                    "JOIN sys.parameters p " +
                    "ON p.object_id = obj.object_id " +
                    "INNER JOIN sys.types t " +
                    "ON p.user_type_id = t.user_type_id " +
                    "WHERE obj.name = '{0}' AND p.is_output = 1;",
                    action);
            }
            else
            {
                sqlstatment = string.Format(
                    "USE {0};" +
                    "SELECT obj.name AS procedure_name, obj.type, " +
                    "       p.name AS parameter_name, p.is_output, " +
                    "       t.name AS type_name, p.max_length AS size " +
                    "FROM sys.objects obj " +
                    "JOIN sys.parameters p " +
                    "ON p.object_id = obj.object_id " +
                    "INNER JOIN sys.types t " +
                    "ON p.user_type_id = t.user_type_id " +
                    "WHERE obj.name = '{1}' AND p.is_output = 1;",
                    dbname,
                    action);
            }
           
            try
            {
                dbserver.dbconn = dbconn;
                connection = dbserver.Connection();

                SqlCommand command;
                using (command = new SqlCommand(sqlstatment, connection))
                {
                    command.CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommand_Timeout"]);

                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SP_Parameter detail = new SP_Parameter();
                            detail.pname = reader.GetValue(2).ToString();
                            detail.datatype = reader.GetValue(4).ToString();
                            detail.size = Int32.Parse(reader.GetValue(5).ToString());
                            parameter.rows.Add(detail);
                        }
                        parameter.api_path_name = db_api_path + action;

                        Global.SpParameterList.rows.Add(parameter);
                    }
                }
                dbserver.DisConnection(connection);
            }
            catch (SqlException ex)
            {
                dbserver.DisConnection(connection);
                SqlErrorCollection err = ex.Errors;
                int errno = Convert.ToInt32(err[0].Number);
                if (errno == 2)
                {
                    Global.BrokerState = false;
                }

                return_code = Constants.RC_DATABASE_EXCEPTION;
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoSystemStoredProcedureOutParams \nResponse={return_code}");
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoSystemStoredProcedureOutParams Request :serviceName={action} Database Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoSystemStoredProcedureOutParams Request :serviceName={action} Database Error: {ex}");
            }
            catch (Exception ex)
            {
                dbserver.DisConnection(connection);
                return_code = Constants.RC_DATABASE_EXCEPTION;
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoSystemStoredProcedureOutParams \nResponse={return_code}");
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> DoSystemStoredProcedureOutParams Request :serviceName={action} Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> DoSystemStoredProcedureOutParams Request :serviceName={action} Error: {ex}");                
            }
            parameter.return_code = return_code;
            return parameter;
        }
        /// <summary>
        /// 取得sp設定參數為output的回傳資料
        /// </summary>
        /// <param name="command"></param>
        /// <param name="detail"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public string GetSystemStoredProcedureOutParamsValue(SqlCommand command, SP_ParameterDetail detail, string action)
        {
            string result = "";
            try
            {
                SP_ParameterDetailValue detailvalue = new SP_ParameterDetailValue();
                foreach (SP_Parameter item in detail.rows)
                {

                    SP_ParameterValue sp_ParameterValue = new SP_ParameterValue();
                    sp_ParameterValue.pname = item.pname;
                    sp_ParameterValue.pvalue = command.Parameters[item.pname].Value.ToString();
                    detailvalue.rows.Add(sp_ParameterValue);

                    if (sp_ParameterValue.pname.ToUpper().Equals("@CONTENT_TYPE"))
                    {
                        Global.ContentType = sp_ParameterValue.pvalue;
                    }
                    if (sp_ParameterValue.pname.ToUpper().Equals("@RESULT") && sp_ParameterValue.pvalue.Length > 0)
                    {
                        result = sp_ParameterValue.pvalue;
                    }
                }
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> GetSystemStoredProcedureOutParamsValue Error:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> GetSystemStoredProcedureOutParamsValue Error:{ex}");
            }
            return result;
        }
        /// <summary>
        /// 清空Broker Service內的仍存在的Converstation（重新啟動Broker時會執行）
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="NotificationQueue"></param>
        public void ClearNotificationQueue(string connectionString, string NotificationQueue)
        {
            CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{myGuid}> ClearNotificationQueue Start");
            try
            {
                //using (var connection = new SqlConnection(connectionString))
                //using (var command = new SqlCommand())
                //{
                //    string sql =
                //        @"DECLARE @Conversation AS uniqueidentifier; " +
                //        @"DECLARE ConversationCursor CURSOR LOCAL FAST_FORWARD  " +
                //        @"    FOR " +
                //        @"        SELECT conversation_handle  " +
                //        @"        FROM sys.conversation_endpoints " +
                //        @"        WHERE (far_service = '" + NotificationQueue + "' AND state <> 'SO')" +
                //        @"              OR (state = 'CO' AND security_timestamp < DATEADD(HH,-2,GETUTCDATE()))" +
                //        @"     " +
                //        @"OPEN ConversationCursor; " +
                //        @"FETCH NEXT FROM ConversationCursor INTO @Conversation; " +
                //        @"WHILE @@FETCH_STATUS = 0  " +
                //        @"BEGIN " +
                //        @"    END CONVERSATION @Conversation WITH CLEANUP; " +
                //        @" " +
                //        @"    FETCH NEXT FROM ConversationCursor INTO @Conversation; " +
                //        @"END " +
                //        @"";

                //    command.Connection = connection;
                //    command.CommandType = CommandType.Text;
                //    command.CommandText = sql;

                //    connection.Open();

                //    command.ExecuteNonQuery();
                //}
            }
            catch (SqlException ex)
            {
               
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> ClearNotificationQueue Database Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> ClearNotificationQueue Database Error: {ex}");
               
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{myGuid}> ClearNotificationQueue Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{myGuid}> ClearNotificationQueue Error: {ex}");
                
            }
        }
        #endregion
        #region API
        /// <summary>
        /// 發送電文SpPushSystemJobToCancel，刪除暫存的Job Table
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="jobname"></param>
        static public void CallAPIJobToCancel(string guid, string jobname)
        {
            try
            {
                string apiname = "SpPushSystemJobToCancel";
                var param = new Dictionary<string, string>
                        {
                            { "udp_guid" , guid},
                            { "job_name" , jobname}
                        };
                WebDataHandler webDataHandler = new WebDataHandler();
                var result = webDataHandler.DoPostNonCompress(apiname, "", param, "", Global.ConnString, "", HttpMethods.Get);
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{guid}> CallAPIJobToCancel Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{guid}> CallAPIJobToCancel Error: {ex}");
            }
        }
        /// <summary>
        /// 發送電文SpPushSystemBrokerAgentInfo，取得有註冊Broker的清單
        /// </summary>
        /// <param name="state"></param>
        static public void SpPushSystemBrokerAgentInfo(string guid, string state)
        {
            //try
            //{
            //    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> SpPushSystemBrokerAgentInfo Starting State={state}");
            //    string action = "SpPushSystemBrokerAgentInfo";
            //    var ipAddress = CommonTools.GetLocalIPAddress();

            //    // Body
            //    var api_Rq = new api_SpPushSystemBrokerAgentInfo.Request()
            //    {
            //        source_ip = ipAddress,
            //        source_port = ConfigurationManager.AppSettings["UDP_Port"],
            //        broker_guid = Global.BrokerGuid,
            //        state = state,
            //        far_service = Global.Broker_Service_Name
            //    };
            //    var postBody = JsonConvert.SerializeObject(api_Rq);

            //    WebDataHandler webDataHandler = new WebDataHandler(guid);
            //    string result = webDataHandler.DoPostNonCompress(action, postBody, null, "", Global.ConnString, "", HttpMethods.Post);

            //    api_SpPushSystemBrokerAgentInfo.Response rs = JsonConvert.DeserializeObject<api_SpPushSystemBrokerAgentInfo.Response>(result);
            //    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> SpPushSystemBrokerAgentInfo Result={result}");
            //    if (rs.returnCode.Equals(Constants.RC_SUCCESS))
            //    {
            //        Global.BrokerGuid = rs.returnData.broker_guid;
            //        CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> SpPushSystemBrokerAgentInfo Broker_Guid={Global.BrokerGuid}");
            //        foreach (var item in rs.returnData.rows)
            //        {
            //            string source_ip = item.source_ip;
            //            int source_port = Int16.Parse(item.source_port);
            //            CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Debug.GetHashCode(), $"<{guid}> SpPushSystemBrokerAgentInfo ip={source_ip},port={source_port}");
            //            UdpController.SendUdp(source_ip, source_port, item.action_type);
            //        }
            //    }   
            //}
            //catch (Exception ex)
            //{
            //    Global.BrokerState = false;
            //    CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{guid}> SpPushSystemBrokerAgentInfo Error: {ex.Message}");
            //    CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{guid}> SpPushSystemBrokerAgentInfo Error: {ex}");
            //}
        }
        /// <summary>
        /// 發送電文SpPushSystemBrokerToDo，取得待處理的server_udp資料
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        static public string SpPushSystemBrokerToDo(string guid)
        {
            string result = "";
            try
            {
                Global.IsRunToDo = true;
                string action = "SpPushSystemBrokerToDo";
                var param = new Dictionary<string, string>
                        {
                            { "udp_guid" , guid}
                        };
                WebDataHandler webDataHandler = new WebDataHandler(guid);
                result = webDataHandler.DoPostNonCompress(action, "", param, "", Global.ConnString, "", HttpMethods.Get);
            }
            catch(Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"<{guid}> SpPushSystemBrokerToDo Error: {ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"<{guid}>SpPushSystemBrokerToDo Error: {ex}");
            }
            return result;
        }
        #endregion
        
    }
}
