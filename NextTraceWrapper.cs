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
using System.Runtime.InteropServices;
using System.Linq;

namespace NextTrace
{
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
    enum AppStatus
    {
        Init,
        Start,
        Quit
    }
    internal class NextTraceWrapper
    {
        private Process _process;
        public AppStatus Status { get; set; } = AppStatus.Init;
        public event EventHandler AppStart;
        public event EventHandler<AppQuitEventArgs> AppQuit;
        public event EventHandler<ExceptionalOutputEventArgs> ExceptionalOutput;
        private string nexttracePath;
        private bool builtinNT = false;
        private int errorOutputCount = 0;
        public ObservableCollection<TracerouteResult> Output { get; } = new ObservableCollection<TracerouteResult>();

        public NextTraceWrapper()
        {
            string curDir = AppDomain.CurrentDomain.BaseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 检查 Windows 平台可执行文件
                List<string> winBinaryList = new List<string> { "nexttrace.exe", "nexttrace_windows_amd64.exe", "nexttrace_windows_arm64.exe", "nexttrace_windows_armv7.exe", "nexttrace_windows_386.exe" };
                foreach (string winBinaryName in winBinaryList)
                {
                    if (File.Exists(Path.Combine(curDir, winBinaryName)))
                    {
                        // 先检查根目录
                        nexttracePath = Path.Combine(curDir, winBinaryName);
                        break;
                    }
                    // 再检查PATH变量
                    string pathVar = Environment.GetEnvironmentVariable("PATH");
                    string[] pathDirs = pathVar.Split(Path.PathSeparator);
                    foreach (string pathDir in pathDirs)
                    {
                        if (File.Exists(Path.Combine(pathDir, winBinaryName)))
                        {
                            nexttracePath = Path.Combine(pathDir, winBinaryName);
                            break;
                        }
                    }
                    if (nexttracePath != null) break;
                }
            }
            else
            {
                // 检查其他平台可执行文件
                List<string> otherBinaryList = new List<string> { "nexttrace", "nexttrace_android_arm64", "nexttrace_darwin_amd64", "nexttrace_darwin_arm64", "nexttrace_dragonfly_amd64", "nexttrace_freebsd_386", "nexttrace_freebsd_amd64", "nexttrace_freebsd_arm64", "nexttrace_freebsd_armv7", "nexttrace_linux_386", "nexttrace_linux_amd64", "nexttrace_linux_arm64", "nexttrace_linux_armv5", "nexttrace_linux_armv6", "nexttrace_linux_armv7", "nexttrace_linux_mips", "nexttrace_linux_mips64", "nexttrace_linux_mips64le", "nexttrace_linux_mipsle", "nexttrace_linux_ppc64", "nexttrace_linux_ppc64le", "nexttrace_linux_riscv64", "nexttrace_linux_s390x", "nexttrace_openbsd_386", "nexttrace_openbsd_amd64", "nexttrace_openbsd_arm64", "nexttrace_openbsd_armv7" };
                foreach (string otherBinaryName in otherBinaryList)
                {
                    if (File.Exists(Path.Combine(curDir, "OpenTrace.app/Contents/MacOS", otherBinaryName)))
                    {
                        nexttracePath = Path.Combine(curDir, "OpenTrace.app/Contents/MacOS", otherBinaryName);
                        builtinNT = true;
                        break;
                    }
                    if (File.Exists(Path.Combine(curDir, otherBinaryName)))
                    {
                        nexttracePath = Path.Combine(curDir, otherBinaryName);
                        builtinNT = true;
                        break;
                    }
                    
                    string pathVar = Environment.GetEnvironmentVariable("PATH");
                    string[] pathDirs = pathVar.Split(Path.PathSeparator);
                    foreach (string pathDir in pathDirs)
                    {
                        if (File.Exists(Path.Combine(pathDir, otherBinaryName)))
                        {
                            nexttracePath = Path.Combine(pathDir, otherBinaryName);
                            break;
                        }
                    }
                    if (nexttracePath != null) break;
                }
            }

            // 检查是否手动指定了可执行文件
            if (UserSettings.executablePath != "")
            {
                if (File.Exists(UserSettings.executablePath))
                {
                    nexttracePath = UserSettings.executablePath;
                }
                else
                {
                    throw new IOException(UserSettings.executablePath);
                }
            }
            // 未能找到可执行文件
            if (nexttracePath == null)
            {
                throw new FileNotFoundException("nexttrace.exe not found in any location");
            }
        }

        public void Run(string host, bool MTRMode, params string[] extraArgs)
        {
            Task.Run(() =>
            {
                Console.WriteLine($"Using NextTrace: {nexttracePath}");
                string arguments;
                if (MTRMode)
                {
                    arguments = ArgumentBuilder(host, extraArgs.Concat(new string[] { "--queries 1" }).ToArray(), new string[] { "queries" });
                }
                else
                {
                    arguments = ArgumentBuilder(host, extraArgs);
                }

#if NET8_0_OR_GREATER
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && 
                    Array.Find(extraArgs, e => e == "-T" || e == "-U") != null &&
                    Environment.UserName != "root")
                {
                    FileSystemInfo fa = new FileInfo(nexttracePath);
                    if ((fa.UnixFileMode & UnixFileMode.SetUser) == 0) 
                    {
                        if (!builtinNT)
                            App.app.Invoke(() => {
                                Eto.Forms.MessageBox.Show(Resources.MISSING_COMP_PRIV_MACOS);
                            });
                        else 
                        {
                            var elvp = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "/usr/bin/osascript",
                                    ArgumentList = {
                                        "-e",
                                        $"do shell script \"chown root:admin '{nexttracePath}' && chmod +sx '{nexttracePath}'\" with administrator privileges with prompt \"{Resources.MISSING_PRIV_MACOS}\"",
                                    },
                                    UseShellExecute = false,
                                    RedirectStandardOutput = false,
                                    RedirectStandardError = false,
                                    CreateNoWindow = true
                                }
                            };
                            elvp.Start();
                            elvp.WaitForExit();
                        }
                        Status = AppStatus.Quit;
                        AppQuit?.Invoke(this, new AppQuitEventArgs(0));
                        return;
                    }
                }
#endif
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = nexttracePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        StandardOutputEncoding = Encoding.GetEncoding(65001),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                if (UserSettings.IPInsightToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINSIGHT_TOKEN", UserSettings.IPInsightToken);
                if (UserSettings.IPInfoToken != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPINFO_TOKEN", UserSettings.IPInfoToken);
                if (UserSettings.ChunZhenEndpoint != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_CHUNZHENURL", UserSettings.ChunZhenEndpoint);
                if (UserSettings.LeoMoeAPI_HOSTPORT != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_HOSTPORT", UserSettings.LeoMoeAPI_HOSTPORT);
                if (UserSettings.NextTraceProxy != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_PROXY", UserSettings.NextTraceProxy);
                if (UserSettings.POWProvider != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_POWPROVIDER", UserSettings.POWProvider);
                if (UserSettings.IPAPI_Base != "") _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_IPAPI_BASE", UserSettings.IPAPI_Base);

                if (MTRMode) // 添加环境变量让NextTrace进入持续追踪模式
                    _process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_UNINTERRUPTED", "1");

                Regex match1stLine = new Regex(@"^\d{1,2}\|");
                _process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        // Debug.Print(e.Data);
                        // 去除输出中的控制字符
                        Regex formatCleanup = new Regex(@"(\x9B|\x1B\[)[0-?]*[ -\/]*[@-~]");
                        string line = formatCleanup.Replace(e.Data, "");

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
                            if (line.StartsWith("[NextTrace API]")) return;
                            ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(false, line));
                            if (errorOutputCount < 100)
                            {
                                errorOutputCount++;
                            }
                            else
                            {
                                Kill(); // 错误输出过多，强制结束
                            }
                        }
                    }
                };
                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        //Debug.Print(e.Data);
                        ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(true, e.Data));
                        if (errorOutputCount < 100)
                        {
                            errorOutputCount++;
                        }
                        else
                        {
                            Kill(); // 错误输出过多，强制结束
                        }
                    }
                };
                _process.Exited += (sender, e) =>
                {
                    Debug.Print("Exited");
                    Status = AppStatus.Quit;
                    AppQuit?.Invoke(this, new AppQuitEventArgs(_process.ExitCode));
                };
                _process.EnableRaisingEvents = true;
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                Status = AppStatus.Start;
                AppStart?.Invoke(this, null);
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
            string[] LineData = line.Split('|');
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
            if (new Regex(@"^((127\.)|(192\.168\.)|(10\.)|(172\.1[6-9]\.)|(172\.2[0-9]\.)|(172\.3[0-1]\.)|(::1$)|([fF][cCdD]))").IsMatch(IP))
            {
                Geolocation = Resources.PRIVATE_ADDR;
            }
            if (new Regex(@"^((100\.6[4-9]\.)|(100\.[7-9][0-9]\.)|(100\.1[0-1][0-9]\.)|(100\.12[0-7]\.))").IsMatch(IP))
            {
                Geolocation = Resources.SHARED_ADDR;
            }
            if (new Regex(@"^169\.254\.").IsMatch(IP))
            {
                Geolocation = Resources.LINKLOCAL_ADDR;
            }
            if (new Regex(@"^127\.").IsMatch(IP))
            {
                Geolocation = Resources.LOOPBACK_ADDR;
            }

            // 打码 IP 地址
            // maskedHopsMode 设置包含 ip_half, ip_full, ip_geo, all 四种打码模式
            // maskedHops 指示打码的跳数
            
            
            if (UserSettings.maskedHops > 0 && int.Parse(No) <= UserSettings.maskedHops)
            {
                if (UserSettings.maskedHopsMode == "ip_half")
                {
                    if (IP.Contains(":"))
                    {
                        // IPv6 全部打码
                        IP = "****";
                    }
                    else if (IP.Contains("."))
                    {
                        // IPv4 打码后 2 节
                        IP = string.Join(".", IP.Split('.').Take(2).Concat(new string[] { "xx", "xx" }));   
                    }
                    // 删除主机名
                    Hostname = "";
                }
                else if (UserSettings.maskedHopsMode == "ip_full")
                {
                    IP = "****";
                    // 删除主机名
                    Hostname = "";
                }
                else if (UserSettings.maskedHopsMode == "ip_geo")
                {
                    IP = "****";
                    Geolocation = "****";
                    // 删除主机名
                    Hostname = "";
                }
                else if (UserSettings.maskedHopsMode == "all")
                {
                    IP = "****";
                    Geolocation = "****";
                    AS = "****";
                    Hostname = "";
                    Organization = "****";
                    Latitude = "****";
                    Longitude = "****";
                }
            }


            return new TracerouteResult(No, IP, Time, Geolocation, AS, Hostname, Organization, Latitude, Longitude);
        }
        private string ArgumentBuilder(string host, string[] extraArgs, string[] ignoreUserArgs = null)
        {
            List<string> finalArgs = new List<string>();
            finalArgs.Add(host);
            finalArgs.Add("--raw");
            finalArgs.Add("--map");
            var checkArgsFromConfList = new List<string> { "queries", "port", "parallel_requests", "max_hops", "first", "send_time", "ttl_time", "source", "dev" };

            UserSettings userSettings = new UserSettings();
            foreach (var setting in userSettings.GetType().GetProperties())
            {
                if (checkArgsFromConfList.Contains(setting.Name) && (ignoreUserArgs == null || !ignoreUserArgs.Contains(setting.Name)))
                {
                    if ((string)setting.GetValue(userSettings, null) != "")
                        finalArgs.Add("--" + setting.Name.Replace('_', '-') + " " + (string)setting.GetValue(userSettings, null));
                }
            }
            if (UserSettings.rdns_mode == "disable") finalArgs.Add("-n");
            if (UserSettings.rdns_mode == "always") finalArgs.Add("-a");
            finalArgs.Add(System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh") ? "--language cn" : "--language en");
            finalArgs.Add(UserSettings.arguments);
            finalArgs.AddRange(extraArgs);
            Debug.Print(String.Join(" ", finalArgs));
            return String.Join(" ", finalArgs);
        }
        public void Kill()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }

        // 验证IP有效性，返回处理后的IP（如把IPv6转为缩写形式等）IP无效则返回null。
        private string ValidateIP(string IP)
        {
            return null;
        }
    }
}
