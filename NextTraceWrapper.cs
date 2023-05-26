using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using OpenTrace.Properties;
using System.Collections.Generic;

namespace OpenTrace
{
    internal class NextTraceWrapper
    {
        private Process _process;
        public event EventHandler OnAppQuit;
        public ObservableCollection<TracerouteResult> Output { get; } = new ObservableCollection<TracerouteResult>();

        public NextTraceWrapper(string host, string extraArgs)
        {

            // 检查 nexttrace.exe 是否存在于当前目录
            string nexttracePath = null;
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexttrace.exe")))
            {
                nexttracePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexttrace.exe");
            }
            // 检查 Linux / macOS
            else if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexttrace")))
            {
                nexttracePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nexttrace");
            }
            // 检查 PATH 目录
            if (nexttracePath == null)
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH");
                string[] pathDirs = pathVar.Split(Path.PathSeparator);
                foreach (string pathDir in pathDirs)
                {
                    if (File.Exists(Path.Combine(pathDir, "nexttrace.exe")))
                    {
                        nexttracePath = Path.Combine(pathDir, "nexttrace.exe");
                        break;
                    }
                    else if (File.Exists(Path.Combine(pathDir, "nexttrace")))
                    {
                        nexttracePath = Path.Combine(pathDir, "nexttrace");
                        break;
                    }
                }
            }
            // 未能找到目录
            if (nexttracePath == null)
            {
                throw new FileNotFoundException("nexttrace.exe not found");
            }

            Task.Run(() =>
            {
                
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = nexttracePath,
                        Arguments = ArgumentBuilder(host,extraArgs),
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.GetEncoding(65001),
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                Regex match1stLine = new Regex(@"^\d{1,2}\|");
                string lastHop = "";
                List<string> tracerouteBlock = new List<string>();
                _process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        // 去除输出中的控制字符
                        Regex formatCleanup = new Regex(@"(\x9B|\x1B\[)[0-?]*[ -\/]*[@-~]");
                        string line = formatCleanup.Replace(e.Data, "");

                        // 忽略一些行
                        if (line.StartsWith("NextTrace v")) return;
                        if (line.StartsWith("IP Geo Data Provider")) return;
                        if (line.StartsWith("[NextTrace API]")) return;
                        if (line.StartsWith("traceroute to")) return;

                        Match match1 = match1stLine.Match(line);
                        if (match1.Success)
                        {
                            // 以块为单位处理
                            string hop = line.Split("|")[0];
                            if(hop != lastHop)
                            {
                                lastHop = hop;
                                if(tracerouteBlock.Count>0) Output.Add(ProcessBlock(tracerouteBlock));
                                tracerouteBlock = new List<string>();
                                tracerouteBlock.Add(line);
                            } else {
                                tracerouteBlock.Add(line);
                            }

                        }
                    }
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.WaitForExit();
                OnAppQuit?.Invoke(this, null);
            });
        }
        private TracerouteResult ProcessBlock(List<string> block)
        {
            string No = "";
            string IP = "*";
            string Time = "";
            string Geolocation = "";
            string AS = "";
            string Hostname = "";
            string Organization = "";
            string Latitude = "";
            string Longitude = "";
            Debug.Print(String.Join("\n",block));
            foreach (string line in block)
            {
                String[] LineData = line.Split("|");
                if (LineData.Length > 7)
                {
                    No = LineData[0];
                    if (LineData[1] == "*")
                    {
                        Time += "* / ";
                        continue;
                    }
                    IP = LineData[1];
                    Time += LineData[3] + " / ";
                    Geolocation = LineData[5] + " " + LineData[6] + " " + LineData[7] + " " + LineData[8];
                    AS = LineData[4];
                    Hostname = LineData[2];
                    Organization = LineData[9];
                    Latitude = LineData[10];
                    Longitude = LineData[11];
                }
            }
            Time = Time[..^3]; // 去掉最末尾多余的东西

            // 匹配特定网络地址
            if (new Regex(@"^(127\.)|(192\.168\.)|(10\.)|(172\.1[6-9]\.)|(172\.2[0-9]\.)|(172\.3[0-1]\.)|(::1$)|([fF][cCdD])").IsMatch(IP))
            {
                Geolocation = Resources.PRIVATE_ADDR;
            }
            if (new Regex(@"^(100\.6[4-9]\.)|(100\.[7-9][0-9]\.)|(100\.1[0-1][0-9]\.)|(100\.12[0-7]\.)").IsMatch(IP))
            {
                Geolocation = Resources.SHARED_ADDR;
            }
            if (new Regex(@"^(169\.254\.)").IsMatch(IP))
            {
                Geolocation = Resources.LINKLOCAL_ADDR;
            }
            if (new Regex(@"^(127\.)").IsMatch(IP))
            {
                Geolocation = Resources.LOOPBACK_ADDR;
            }


            return new TracerouteResult(No, IP, Time, Geolocation, AS, Hostname, Organization, Latitude, Longitude);
        }
        private string ArgumentBuilder(string host, string extraArgs)
        {
            List<string> finalArgs = new List<string>();
            finalArgs.Add(host);
            finalArgs.Add("--raw");
            string[] checkArgsFromConfList = { "queries", "port", "parallel_requests", "max_hops", "first", "send_time", "ttl_time", "source", "dev"};
            foreach(string checkArgs in checkArgsFromConfList)
            {
                if ((string)UserSettings.Default[checkArgs] != "")
                {
                    finalArgs.Add("--" + checkArgs.Replace('_', '-') + " " + (string)UserSettings.Default[checkArgs]);
                }
            }

            if ((bool)UserSettings.Default["no_rdns"] == true)
                finalArgs.Add("--no-rdns");
            finalArgs.Add(System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh") ? "--language cn" : "--language en");
            finalArgs.Add(extraArgs);
            Debug.Print(String.Join(" ", finalArgs));
            return String.Join(" ", finalArgs);
        }
        public void Kill()
        {
            _process.Kill();
        }
    }
}
