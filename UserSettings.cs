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
        public static string executablePath { get; set; }

        [Setting(Name = "arguments", Default = "")]
        public static string arguments { get; set; }

        [Setting(Name = "queries", Default = "")]
        public static string queries { get; set; }

        [Setting(Name = "port", Default = "")]
        public static string port { get; set; }

        [Setting(Name = "parallel_request", Default = "")]
        public static string parallel_request { get; set; }

        [Setting(Name = "max_hops", Default = "")]
        public static string max_hops { get; set; }

        [Setting(Name = "first", Default = "")]
        public static string first { get; set; }

        [Setting(Name = "send_time", Default = "")]
        public static string send_time { get; set; }

        [Setting(Name = "ttl_time", Default = "")]
        public static string ttl_time { get; set; }

        [Setting(Name = "source", Default = "")]
        public static string source { get; set; }

        [Setting(Name = "dev", Default = "")]
        public static string dev { get; set; }

        [Setting(Name = "IPInsightToken", Default = "")]
        public static string IPInsightToken { get; set; }

        [Setting(Name = "IPInfoToken", Default = "")]
        public static string IPInfoToken { get; set; }

        [Setting(Name = "ChunZhenEndpoint", Default = "")]
        public static string ChunZhenEndpoint { get; set; }

        [Setting(Name = "language", Default = "")]
        public static string language { get; set; }

        [Setting(Name = "mapProvider", Default = "")]
        public static string mapProvider { get; set; }

        [Setting(Name = "combineGeoOrg", Default = false)]
        public static bool combineGeoOrg { get; set; }

        [Setting(Name = "rdns_mode", Default = "default")]
        public static string rdns_mode { get; set; }

        [Setting(Name = "timeRounding", Default = false)]
        public static bool timeRounding { get; set; }

        [Setting(Name = "hideMapPopup", Default = false)]
        public static bool hideMapPopup { get; set; }

        [Setting(Name = "traceHistory", Default = "")]
        public static string traceHistory { get; set; }

        [Setting(Name = "LeoMoeAPI_HOSTPORT", Default = "")]
        public static string LeoMoeAPI_HOSTPORT { get; set; }

        [Setting(Name = "NextTraceProxy", Default = "")]
        public static string NextTraceProxy { get; set; }

        [Setting(Name = "IPAPI_Base", Default = "")]
        public static string IPAPI_Base { get; set; }

        [Setting(Name = "hideAddICMPFirewallRule", Default = false)]
        public static bool hideAddICMPFirewallRule { get; set; }

        [Setting(Name = "enable_ip2region", Default = false)]
        public static bool enable_ip2region { get; set; }
        
        [Setting(Name = "enable_ipinfolocal", Default = false)]
        public static bool enable_ipinfolocal { get; set; }
        
        [Setting(Name = "customDNSResolvers", Default = "8.8.8.8#Google DNS\nhttps://cloudflare-dns.com/dns-query#CloudFlare DoH")]
        public static string customDNSResolvers { get; set; }

        [Setting(Name = "POWProvider", Default = "")]
        public static string POWProvider { get; set; }

        [Setting(Name = "gridSizePercentage", Default = 0.5)]
        public static double gridSizePercentage { get; set; }

        [Setting(Name = "localDBPath", Default = "")]
        public static string localDBPath { get; set; }

        [Setting(Name = "localDBAddr", Default = "")]
        public static string localDBAddr { get; set; }

        [Setting(Name = "localDBOrg", Default = "")]
        public static string localDBOrg { get; set; }

        [Setting(Name = "localDBLat", Default = "")]
        public static string localDBLat { get; set; }

        [Setting(Name = "localDBLon", Default = "")]
        public static string localDBLon { get; set; }

        [Setting(Name = "localDBASN", Default = "")]
        public static string localDBASN { get; set; }
        
        [Setting(Name = "localDBHostname", Default = "")]
        public static string localDBHostname { get; set; }

        [Setting(Name = "checkUpdateOnStartup", Default = true)]
        public static bool checkUpdateOnStartup { get; set; }

        [Setting(Name = "maskedHops", Default = 0)]
        public static int maskedHops { get; set; }

        [Setting(Name = "maskedHopsMode", Default = "ip_half")]
        public static string maskedHopsMode { get; set; }

        [Setting(Name = "selectedDnsResolver", Default = "system")]
        public static string selectedDnsResolver { get; set; }

        [Setting(Name = "selectedProtocol", Default = "")]
        public static string selectedProtocol { get; set; }

        [Setting(Name = "selectedDataProvider", Default = "")]
        public static string selectedDataProvider { get; set; }
    }
}
