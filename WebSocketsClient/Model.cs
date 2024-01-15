using System;
using System.Collections.Generic;
//using Microsoft.AspNetCore.Http;
//using Newtonsoft.Json.Linq;

namespace WebSocketsClient
{

    public class SP_Parameter
    {
        public string pname;
        public string datatype;
        public int size;
    }

    public class SP_ParameterDetail
    {
        public string return_code;
        public string api_path_name;
        public List<SP_Parameter> rows;

        public SP_ParameterDetail()
        {
            rows = new List<SP_Parameter>();
        }
    }

    public class SP_ParameterList
    {
        public List<SP_ParameterDetail> rows;

        public SP_ParameterList()
        {
            rows = new List<SP_ParameterDetail>();
        }
    }

    public class SP_ParameterValue
    {
        public string pname;
        public string pvalue;
    }

    public class SP_ParameterDetailValue
    {
        public List<SP_ParameterValue> rows;

        public SP_ParameterDetailValue()
        {
            rows = new List<SP_ParameterValue>();
        }
    }

    public class Server_UDP
    {
        public int serial_no { get; set; }
        public string action_type { get; set; }
        public string source_ip { get; set; }
        public int source_port { get; set; }
        public string msg { get; set; }
        public string flag { get; set; }
        public DateTime flag_time { get; set; }
    }

    public class Job_Info
    {
        public string returnCode;
        public DateTime stamp;
        public bool isRunning;
    }

    public class DBResult
    {
        public string Code { get; set; }
        public string DBConnString { get; set; }
    }
}
