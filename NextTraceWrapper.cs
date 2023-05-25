using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
namespace OpenTrace
{
    internal class NextTraceWrapper
    {
        private Process _process;
        public event EventHandler OnAppQuit;
        public ObservableCollection<TracerouteResult> Output { get; } = new ObservableCollection<TracerouteResult>();
        private int queries = 3;

        public NextTraceWrapper(string arguments)
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
                        Arguments = arguments,
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.GetEncoding(65001),
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                int readRestLine = 0;

                Regex match1stLine = new Regex(@"^\d{1,2}\|");
                string tracerouteBlock = "";
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

                        if (readRestLine > 0)
                        {
                            readRestLine--;

                            if (readRestLine == 0)
                            {
                                tracerouteBlock += line;
                                Output.Add(ProcessBlock(tracerouteBlock));
                            }
                            else
                            {
                                tracerouteBlock += line + "\n";
                            }
                            return;
                        }
                        Match match1 = match1stLine.Match(line);
                        if (match1.Success)
                        {
                            // 以块为单位处理
                            tracerouteBlock = line + "\n";
                            readRestLine = queries - 1;
                        }
                    }
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.WaitForExit();
                OnAppQuit?.Invoke(this, null);
            });
        }
        private TracerouteResult ProcessBlock(string block)
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
            String[] blockLines = block.Split("\n");
            foreach (string line in blockLines)
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
        public void Kill()
        {
            _process.Kill();
        }
    }
}
