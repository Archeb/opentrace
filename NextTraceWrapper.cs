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
using OpenTrace;

namespace NextTrace
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
    class ExceptionalOutputEventArgs : EventArgs
    {
        public bool IsErrorOutput { get; set; }
        public string Output { get; set; }
        public ExceptionalOutputEventArgs(bool isErrorOutput, string output)
        {
            IsErrorOutput = isErrorOutput;
            Output = output;
        }
    }
    class AppQuitEventArgs : EventArgs
    {
        public int ExitCode { get; set; }
        public AppQuitEventArgs(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
    enum Modes
    {
        MTR,
        Traceroute
    }
    internal class NextTraceWrapper
    {
        public Modes RunningMode;
        public bool Quitting;
        private Process _process;
        public event EventHandler<AppQuitEventArgs> AppQuit;
        public event EventHandler<HostResolvedEventArgs> HostResolved;
        public event EventHandler<ExceptionalOutputEventArgs> ExceptionalOutput;

        private string nexttracePath;
        public ObservableCollection<TracerouteResult> Output { get; } = new ObservableCollection<TracerouteResult>();

        public NextTraceWrapper()
        {

            // 检查 nexttrace.exe 是否存在于当前目录
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
            if (File.Exists(UserSettings.Default.exectuablePath))
            {
                nexttracePath = UserSettings.Default.exectuablePath;
            }
            // 未能找到可执行文件
            if (nexttracePath == null)
            {
                throw new FileNotFoundException("nexttrace.exe not found");
            }
        }

        public void RunTraceroute(string host, string extraArgs)
        {
            Quitting = false;
            RunningMode = Modes.Traceroute;
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
                if (UserSettings.Default.IPInsightToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINSIGHT_TOKEN", UserSettings.Default.IPInsightToken);
                if (UserSettings.Default.IPInfoToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINFO_TOKEN", UserSettings.Default.IPInfoToken);
                if (UserSettings.Default.ChunZhenEndpoint != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_CHUNZHENURL", UserSettings.Default.ChunZhenEndpoint);

                Regex match1stLine = new Regex(@"^\d{1,2}\|");
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
                            Output.Add(ProcessLine(line));
                        }
                        else
                        {
                            if (line.StartsWith("NextTrace ")) return;
                            if (line.StartsWith("traceroute to ")) return;
                            if (line.StartsWith("IP Geo Data Provider")) return;
                            Debug.Print(line);
                            ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(false, line));
                        }
                    }
                };
                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Debug.Print(e.Data);
                        ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(true, e.Data));
                    }
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                _process.WaitForExit();
                AppQuit?.Invoke(this, new AppQuitEventArgs(_process.ExitCode));
            });
        }
        public void RunMTR(string host, string extraArgs)
        {
            Quitting = false;
            RunningMode = Modes.MTR;
            Task.Run(() =>
            {
                while(Quitting != true)
                {
                    _process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = nexttracePath,
                            Arguments = ArgumentBuilder(host, extraArgs + " --queries 1", new List<string> {"queries"}),
                            UseShellExecute = false,
                            StandardOutputEncoding = Encoding.GetEncoding(65001),
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    if (UserSettings.Default.IPInsightToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINSIGHT_TOKEN", UserSettings.Default.IPInsightToken);
                    if (UserSettings.Default.IPInfoToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINFO_TOKEN", UserSettings.Default.IPInfoToken);
                    if (UserSettings.Default.ChunZhenEndpoint != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_CHUNZHENURL", UserSettings.Default.ChunZhenEndpoint);

                    Regex match1stLine = new Regex(@"^\d{1,2}\|");
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
                                Output.Add(ProcessLine(line));
                            }
                            else
                            {
                                if (line.StartsWith("NextTrace ")) return;
                                if (line.StartsWith("traceroute to ")) return;
                                if (line.StartsWith("IP Geo Data Provider")) return;
                                Debug.Print(line);
                                Quitting = true; // 非正常输出，结束MTR
                                ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(false, line));
                            }
                        }
                    };
                    _process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            Debug.Print(e.Data);
                            Quitting = true; // 非正常输出，结束MTR
                            ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(true, e.Data));
                        }
                    };
                    _process.Start();
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _process.WaitForExit();
                    if (_process.ExitCode != 0) Quitting = true; // 非正常退出，结束MTR
                }
                AppQuit?.Invoke(this, new AppQuitEventArgs(_process.ExitCode));
            });
        }
        private TracerouteResult ProcessLine(string line)
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
            string[] LineData = line.Split("|");
            if (LineData.Length > 7)
            {
                No = LineData[0];
                if (LineData[1] == "*")
                {
                    Time = "*";
                }
                else
                {
                    IP = LineData[1];
                    Time = LineData[3];
                    Geolocation = LineData[5] + " " + LineData[6] + " " + LineData[7] + " " + LineData[8];
                    AS = LineData[4];
                    Hostname = LineData[2];
                    Organization = LineData[9];
                    Latitude = LineData[10];
                    Longitude = LineData[11];
                }
            }

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
        private string ArgumentBuilder(string host, string extraArgs, List<string> ignoreUserArgs = null)
        {
            List<string> finalArgs = new List<string>();
            finalArgs.Add(host);
            finalArgs.Add("--raw");
            finalArgs.Add("--map");
            string[] checkArgsFromConfList = { "queries", "port", "parallel_requests", "max_hops", "first", "send_time", "ttl_time", "source", "dev" };
            foreach (string checkArgs in checkArgsFromConfList)
            {
                if ((string)UserSettings.Default[checkArgs] != "" && (ignoreUserArgs == null || !ignoreUserArgs.Contains(checkArgs)))
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
            Quitting = true;
            if (_process != null)
                _process.Kill();
        }

        // 验证IP有效性，返回处理后的IP（如把IPv6转为缩写形式等）IP无效则返回null。
        private string ValidateIP(string IP)
        {
            return null;
        }
    }
}
