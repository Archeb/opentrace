namespace OpenTrace.Models
{
    class TracerouteResult
    {
        public TracerouteResult(string no, string ip, string time, string geolocation, string ASNumber, string hostname, string organization, string latitude, string longitude)
        {
            No = no;
            IP = ip;
            Time = time;
            Geolocation = geolocation;
            AS = ASNumber;
            Hostname = hostname;
            Organization = organization;
            Latitude = latitude;
            Longitude = longitude;
        }
        public string No { get; set; }
        public string IP { get; set; }
        public string Time { get; set; }
        public string Geolocation { get; set; }
        public string AS { get; set; }
        public string Hostname { get; set; }
        public string Organization { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
    }
}
