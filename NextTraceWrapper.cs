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
    class HostResolvedEventArgs : EventArgs
    {
        public string Host { get; set; }
        public string IP { get; set; }
        public HostResolvedEventArgs(string host, string ip)
        {
            Host = host;
            IP = ip;
        }
    }
    internal class NextTraceWrapper
    {
        private Process _process;
        public event EventHandler AppQuit;
        public event EventHandler<HostResolvedEventArgs> HostResolved;


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
            // 检查是否手动指定了可执行文件
            if (File.Exists(Properties.UserSettings.Default.exectuablePath))
            {
                nexttracePath = Properties.UserSettings.Default.exectuablePath;
            }
            // 未能找到可执行文件
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
                        Arguments = ArgumentBuilder(host, extraArgs),
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.GetEncoding(65001),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                if (Properties.UserSettings.Default.IPInsightToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINSIGHT_TOKEN", Properties.UserSettings.Default.IPInsightToken);
                if (Properties.UserSettings.Default.IPInfoToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINFO_TOKEN", Properties.UserSettings.Default.IPInfoToken);
                if (Properties.UserSettings.Default.ChunZhenEndpoint != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_CHUNZHENURL", Properties.UserSettings.Default.ChunZhenEndpoint);

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

                        Match matchHostResolve = new Regex(@"^traceroute to (.*?) \((.*?)\),").Match(line);
                        if (matchHostResolve.Success)
                        {
                            HostResolved.Invoke(this, new HostResolvedEventArgs(matchHostResolve.Groups[2].Value, matchHostResolve.Groups[1].Value));
                        }

                        Match match1 = match1stLine.Match(line);
                        if (match1.Success)
                        {
                            // 以块为单位处理
                            string hop = line.Split("|")[0];
                            if (hop != lastHop)
                            {
                                lastHop = hop;
                                if (tracerouteBlock.Count > 0) Output.Add(ProcessBlock(tracerouteBlock));
                                tracerouteBlock = new List<string>();
                                tracerouteBlock.Add(line);
                            }
                            else
                            {
                                tracerouteBlock.Add(line);
                            }

                        }
                        else
                        {
                            Debug.Print(line);
                        }
                    }
                };
                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Debug.Print(e.Data);

                    }
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.WaitForExit();
                // 传递最后的输出
                if (tracerouteBlock.Count > 0) Output.Add(ProcessBlock(tracerouteBlock));
                AppQuit?.Invoke(this, null);
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
            Debug.Print(String.Join("\n", block));
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
            string[] checkArgsFromConfList = { "queries", "port", "parallel_requests", "max_hops", "first", "send_time", "ttl_time", "source", "dev" };
            foreach (string checkArgs in checkArgsFromConfList)
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

        // 验证IP有效性，返回处理后的IP（如把IPv6转为缩写形式等）IP无效则返回null。
        private string ValidateIP(string IP)
        {
            return null;
        }
    }
}
