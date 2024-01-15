using System;
//using FCMAgent.API;
using System.Collections.Generic;
using System.Threading;

namespace WebSocketsClient
{
    static class Constants
    {
        public const string RC_SUCCESS = "0000";
        public const string RC_NODATA = "0410";
        public const string RC_MULTI_REQUEST = "0230";
        public const string RC_OTHER_EXCEPTION = "0300";
        public const string RC_NEED_EXTERNAL_DBCONN = "0310";
        public const string RC_DATABASE_EXCEPTION = "0900";

        public const string LOG_INFO = "Info";   // LOG 型態
        public const string LOG_ERROR = "Error"; // LOG 型態
        public const string LOG_TRACE = "Trace"; // LOG 型態
        public const string LOG_DEBUG = "Debug"; // LOG 型態
        
        public const int    SERVICE_BROKER_TIMEOUT = 86400; // Service Broker Convertion的回收，單位：秒

        public const string JOB_RUN_TYPE_TIMER = "Timer";
        public const string JOB_RUN_TYPE_EVENT = "Event";
    }

    public enum Log_Level
    {
        Error = 3,  // 錯誤 - error conditions 
        Info =  6,  // 資訊 - informational 
        Debug = 7   // 除錯 - debug-level messages 
    }

    public static class Global
    {
        public static string ConnString { get; set; }

        public static string ContentType { get; set; }

        public static string BrokerGuid { get; set; }

        public static Dictionary<string, Job_Info> LastJobRCDictionary { get; set; } //儲存上次執行的Job_Run的資訊(key=sp)，包含：結果、時間、執行狀態。

        public static SP_ParameterList SpParameterList { get; set; }

        public static bool IsRunToDo { get; set; } //判斷SpPushSystemBrokeToDo是否正在執行

        public static String BrokerService { get; set; } //儲存啟動Broker啟動結果

        public static bool BrokerState { get; set; } //儲存Broker服務目前狀態

        public static bool isStartResult { get; set; } //儲存啟動Broker結果
        public static bool isStopResult { get; set; } //儲存關閉Broker結果 
        public static bool isRegResult { get; set; }  //儲存Broker註冊(監聽資料表)結果
        public static bool isGetDBConnResult { get; set; } //判斷資料庫連線是否由資源密碼元件取得
        public static bool isDBException { get; set; } //判斷是否發生資料庫異常

        public static string Broker_Service_Name { get; set; }
        public static string Broker_Queue_Name { get; set; }

        public static Dictionary<string, CancellationTokenSource> CTSDictionary { get; set; }
        //public static LinkedList<string> InsertionOrder { get; set; } // 用於維護插入顺序的鏈表

    }
}
