using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenTrace
{
    class TracerouteHop
    {
        public TracerouteHop(TracerouteResult hopData)
        {
            HopData = new ObservableCollection<TracerouteResult>();
            HopData.Add(hopData);
        }
        public string No
        {
            get
            {
                return HopData[0].No;
            }
        }
        public string IP
        {
            get
            {
                List<String> uniqueIPs = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueIPs.Contains(hop.IP) && hop.IP != "*")
                        uniqueIPs.Add(hop.IP);
                }
                if (uniqueIPs.Count == 0) uniqueIPs.Add("*");
                return String.Join(Environment.NewLine, uniqueIPs);
            }
        }
        public string Geolocation
        {
            get
            {
                List<String> uniqueGeo = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueGeo.Contains(hop.Geolocation) && hop.IP != "*")
                        uniqueGeo.Add(hop.Geolocation);
                }
                return String.Join(Environment.NewLine, uniqueGeo);
            }
        }
        public string Organization
        {
            get
            {
                List<String> uniqueOrg = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueOrg.Contains(hop.Organization) && hop.IP != "*")
                        uniqueOrg.Add(hop.Organization);
                }
                return String.Join(Environment.NewLine, uniqueOrg);
            }
        }

        public string GeolocationAndOrganization
        {
            get
            {
                List<String> uniqueGeoAndOrg = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueGeoAndOrg.Contains(hop.Geolocation + " " + hop.Organization) && hop.IP != "*")
                        uniqueGeoAndOrg.Add(hop.Geolocation + " " + hop.Organization);
                }
                return String.Join(Environment.NewLine, uniqueGeoAndOrg);
            }
        }
        public string Hostname
        {
            get
            {
                List<String> uniqueHostname = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueHostname.Contains(hop.Hostname) && hop.Hostname != "" && hop.IP != "*")
                        uniqueHostname.Add(hop.Hostname);
                }
                return String.Join(Environment.NewLine, uniqueHostname);
            }
        }
        public double StandardDeviation
        {
            get
            {
                int count = 0;
                double sum = 0;
                double mean = 0;
                double stdDev = 0;

                // Calculate the mean
                foreach (TracerouteResult hop in HopData)
                {
                    if(hop.IP != "*")
                    {
                        count++;
                        sum += double.Parse(hop.Time);
                    }
                }
                mean = sum / count;

                // Calculate the standard deviation
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP != "*")
                        stdDev += Math.Pow(double.Parse(hop.Time) - mean, 2);
                }
                stdDev = Math.Sqrt(stdDev / count);

                return stdDev;
            }
        }
        public int Loss
        {
            get
            {
                int count = 0;
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP == "*")
                        count++;
                }
                return (int)((float)count / HopData.Count * 100);
            }
        }
        public int Recv
        {
            get
            {
                int count = 0;
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP != "*")
                        count++;
                }
                return count;
            }
        }
        public int Sent
        {
            get
            {
                return HopData.Count;
            }
        }

        public double Last
        {
            get
            {
                if (HopData.Count > 0 && HopData[HopData.Count - 1].IP != "*")
                    return double.Parse(HopData[HopData.Count - 1].Time);
                else
                    return 0;
            }
        }

        public double Worst
        {
            get
            {
                double worst = 0;
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP != "*" && double.Parse(hop.Time) > worst)
                        worst = double.Parse(hop.Time);
                }
                return worst;
            }
        }

        public double Best
        {
            get
            {
                double best = double.MaxValue;
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP != "*" && double.Parse(hop.Time) < best)
                        best = double.Parse(hop.Time);
                }
                if (best == double.MaxValue) best = 0;
                return best;
            }
        }

        public double Average
        {
            get
            {
                double sum = 0;
                int count = 0;
                foreach (TracerouteResult hop in HopData)
                {
                    if (hop.IP != "*")
                    {
                        sum += double.Parse(hop.Time);
                        count++;
                    }
                }
                return sum / count;
            }
        }
        public ObservableCollection<TracerouteResult> HopData { get; set; }

    }
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
