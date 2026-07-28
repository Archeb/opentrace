using System;
using System.Collections.Generic;
using System.Linq;
using OpenTrace.Infrastructure;

namespace OpenTrace.Models
{
    class TracerouteHop
    {
        internal const int MtrHistoryLimit = 128;

        private readonly string hopNumber;
        private readonly int historyLimit;
        private long sentCount;
        private long receivedCount;
        private long latencySampleCount;
        private double latencyMean;
        private double latencyM2;
        private double bestLatency = double.MaxValue;
        private double worstLatency;
        private double lastLatency;

        public TracerouteHop(TracerouteResult hopData)
            : this(hopData.No)
        {
            AddResult(hopData);
        }

        public TracerouteHop(string hopNumber, int historyLimit = int.MaxValue)
        {
            this.hopNumber = hopNumber;
            this.historyLimit = Math.Max(1, historyLimit);
            HopData = new List<TracerouteResult>();
        }

        public void AddResult(TracerouteResult result)
        {
            if (result == null)
                return;

            sentCount++;
            lastLatency = 0;
            if (result.IP != "*")
            {
                receivedCount++;
                double latency;
                if (double.TryParse(result.Time, out latency))
                {
                    lastLatency = latency;
                    latencySampleCount++;
                    double delta = latency - latencyMean;
                    latencyMean += delta / latencySampleCount;
                    latencyM2 += delta * (latency - latencyMean);
                    bestLatency = Math.Min(bestLatency, latency);
                    worstLatency = Math.Max(worstLatency, latency);
                }
            }

            if (!string.IsNullOrEmpty(result.Latitude) &&
                !string.IsNullOrEmpty(result.Longitude))
            {
                LatestLocatedResult = result;
            }

            HopData.Add(result);
            if (HopData.Count > historyLimit)
                HopData.RemoveAt(0);
        }

        public TracerouteResult LatestLocatedResult { get; private set; }
        public string No
        {
            get
            {
                return hopNumber;
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
                return latencySampleCount < 2
                    ? 0
                    : Math.Sqrt(latencyM2 / latencySampleCount);
            }
        }
        public int Loss
        {
            get
            {
                if (sentCount == 0)
                    return 0;

                return (int)((sentCount - receivedCount) * 100 / sentCount);
            }
        }
        public long Recv
        {
            get
            {
                return receivedCount;
            }
        }
        public long Sent
        {
            get
            {
                return sentCount;
            }
        }

        public double Last
        {
            get
            {
                return lastLatency;
            }
        }

        public double Worst
        {
            get
            {
                return latencySampleCount == 0 ? 0 : worstLatency;
            }
        }

        public double Best
        {
            get
            {
                return latencySampleCount == 0 ? 0 : bestLatency;
            }
        }

        public double Average
        {
            get
            {
                return latencySampleCount == 0 ? 0 : latencyMean;
            }
        }
        public List<TracerouteResult> HopData { get; }

    }
}
