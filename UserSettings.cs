using Advexp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTrace
{
    internal class UserSettings : Advexp.Settings<UserSettings>
    {

        [Setting(Name = "executablePath", Default = "")]
        public static String executablePath { get; set; }

        [Setting(Name = "arguments", Default = "")]        
        public static String arguments { get; set; }

        [Setting(Name = "queries", Default = "")]
        public static String queries { get; set; }
        
        [Setting(Name = "port", Default = "")]
        public static String port { get; set; }
        
        [Setting(Name = "parallel_request", Default = "")]
        public static String parallel_request { get; set; }

        [Setting(Name = "max_hops", Default = "")]
        public static String max_hops { get; set; }

        [Setting(Name = "first", Default = "")]
        public static String first { get; set; }

        [Setting(Name = "send_time", Default = "")]
        public static String send_time { get; set; }

        [Setting(Name = "ttl_time", Default = "")]
        public static String ttl_time { get; set; }

        [Setting(Name = "source", Default = "")]
        public static String source { get; set; }

        [Setting(Name = "dev", Default = "")]
        public static String dev { get; set; }

        [Setting(Name = "IPInsightToken", Default = "")]
        public static String IPInsightToken { get; set; }

        [Setting(Name = "IPInfoToken", Default = "")]
        public static String IPInfoToken { get; set; }

        [Setting(Name = "ChunZhenEndpoint", Default = "")]
        public static String ChunZhenEndpoint { get; set; }

        [Setting(Name = "language", Default = "")]
        public static String language { get; set; }

        [Setting(Name = "mapProvider", Default = "")]
        public static String mapProvider { get; set; }

        [Setting(Name = "combineGeoOrg", Default = false)]
        public static bool combineGeoOrg { get; set; }

        [Setting(Name = "no_rdns", Default = false)]
        public static bool no_rdns { get; set; }

    }
}
