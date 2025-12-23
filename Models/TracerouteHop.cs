using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenTrace.Infrastructure;

namespace OpenTrace.Models
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
        public string Time
        {
            get
            {
                if (UserSettings.timeRounding)
                {
                    var formattedTimes = HopData.Select(d =>
                    {
                        if (d.Time == "*") return "*";
                        double timeValue;
                        if (double.TryParse(d.Time, out timeValue))
                        {
                            return Math.Round(timeValue).ToString();
                        }
                        return d.Time; // Return original string if parsing fails
                    });
                    return String.Join(" / ", formattedTimes);
                }
                else
                {
                    return String.Join(" / ", HopData.Select(d => d.Time));
                }
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
        public string AS
        {
            get
            {
                List<String> uniqueAS = new List<string>();
                foreach (TracerouteResult hop in HopData)
                {
                    if (!uniqueAS.Contains(hop.AS) && hop.AS != "" && hop.IP != "*")
                        uniqueAS.Add(hop.AS);
                }
                return String.Join(Environment.NewLine, uniqueAS);
            }
        }
        public double StandardDeviation
        {
            get
            {
                var validTimes = new List<double>();
                foreach (var hop in HopData)
                {
                    double timeValue;
                    if (hop.IP != "*" && double.TryParse(hop.Time, out timeValue))
                    {
                        validTimes.Add(timeValue);
                    }
                }

                if (validTimes.Count < 2)
                {
                    return 0;
                }

                double mean = validTimes.Average();
                double sumOfSquares = validTimes.Sum(time => Math.Pow(time - mean, 2));

                return Math.Sqrt(sumOfSquares / validTimes.Count);
            }
        }
        public int Loss
        {
            get
            {
                if (HopData.Count == 0)
                {
                    return 0;
                }
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
                {
                    double timeValue;
                    if (double.TryParse(HopData[HopData.Count - 1].Time, out timeValue))
                    {
                        return timeValue;
                    }
                }
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
                    double timeValue;
                    if (hop.IP != "*" && double.TryParse(hop.Time, out timeValue))
                    {
                        if (timeValue > worst)
                        {
                            worst = timeValue;
                        }
                    }
                }
                return worst;
            }
        }

        public double Best
        {
            get
            {
                double best = double.MaxValue;
                bool foundValue = false;
                foreach (TracerouteResult hop in HopData)
                {
                    double timeValue;
                    if (hop.IP != "*" && double.TryParse(hop.Time, out timeValue))
                    {
                        foundValue = true;
                        if (timeValue < best)
                        {
                            best = timeValue;
                        }
                    }
                }
                return foundValue ? best : 0;
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
                    double timeValue;
                    if (hop.IP != "*" && double.TryParse(hop.Time, out timeValue))
                    {
                        sum += timeValue;
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0;
            }
        }
        public ObservableCollection<TracerouteResult> HopData { get; set; }

    }
}
