using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using OpenTrace.Properties;
using System.Collections.Generic;
using OpenTrace.Models;
using OpenTrace.Infrastructure;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using Eto.Forms;

namespace OpenTrace.Services
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
    class TracerouteResultEventArgs : EventArgs
    {
        public TracerouteResult Result { get; }
        public TracerouteResultEventArgs(TracerouteResult result)
        {
            Result = result;
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
        internal static readonly Version NativeMtrMinimumVersion = new Version(1, 5, 2);
        internal static readonly Version ApiV4MinimumVersion = new Version(1, 7, 0);

        private static readonly Regex RawRowPattern = new Regex(
            @"^\d{1,2}\|",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AnsiEscapePattern = new Regex(
            @"(\x9B|\x1B\[)[0-?]*[ -\/]*[@-~]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PrivateAddressPattern = new Regex(
            @"^((127\.)|(192\.168\.)|(10\.)|(172\.1[6-9]\.)|(172\.2[0-9]\.)|(172\.3[0-1]\.)|(::1$)|([fF][cCdD]))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SharedAddressPattern = new Regex(
            @"^((100\.6[4-9]\.)|(100\.[7-9][0-9]\.)|(100\.1[0-1][0-9]\.)|(100\.12[0-7]\.))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LinkLocalAddressPattern = new Regex(
            @"^169\.254\.",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex LoopbackAddressPattern = new Regex(
            @"^127\.",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private Process _process;
        private readonly object processLock = new object();
        private volatile bool stopRequested;
        private bool appStartRaised;
        private Version detectedVersion;
        private bool versionDetectionAttempted;
        public AppStatus Status { get; set; } = AppStatus.Init;
        public event EventHandler AppStart;
        public event EventHandler<AppQuitEventArgs> AppQuit;
        public event EventHandler<ExceptionalOutputEventArgs> ExceptionalOutput;
        public event EventHandler<TracerouteResultEventArgs> ResultReceived;
        private string nexttracePath;
        private int errorOutputCount = 0;
        private PlatformService platformService = new PlatformService();

        public NextTraceWrapper()
        {
            string curDir = AppDomain.CurrentDomain.BaseDirectory;

            // A user-selected executable always takes precedence over the bundled
            // copy. This remains supported for both portable and Store packages.
            if (!string.IsNullOrWhiteSpace(UserSettings.executablePath))
            {
                if (File.Exists(UserSettings.executablePath))
                {
                    nexttracePath = UserSettings.executablePath;
                    return;
                }

                throw new IOException(UserSettings.executablePath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Prefer the current process architecture. Store packages place
                // the architecture-matched binary at the application root as
                // nexttrace.exe, while portable builds may use an upstream name.
                List<string> winBinaryList;
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    winBinaryList = new List<string> { "nexttrace.exe", "nexttrace_windows_arm64.exe" };
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                {
                    winBinaryList = new List<string> { "nexttrace.exe", "nexttrace_windows_386.exe" };
                }
                else
                {
                    winBinaryList = new List<string> { "nexttrace.exe", "nexttrace_windows_amd64.exe", "nexttrace_windows_386.exe" };
                }

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
                    string[] pathDirs = (pathVar ?? "").Split(Path.PathSeparator);
                    foreach (string pathDir in pathDirs)
                    {
                        if (string.IsNullOrWhiteSpace(pathDir)) continue;
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
                        break;
                    }
                    if (File.Exists(Path.Combine(curDir, otherBinaryName)))
                    {
                        nexttracePath = Path.Combine(curDir, otherBinaryName);
                        break;
                    }
                    
                    string pathVar = Environment.GetEnvironmentVariable("PATH");
                    string[] pathDirs = (pathVar ?? "").Split(Path.PathSeparator);
                    foreach (string pathDir in pathDirs)
                    {
                        if (string.IsNullOrWhiteSpace(pathDir)) continue;
                        if (File.Exists(Path.Combine(pathDir, otherBinaryName)))
                        {
                            nexttracePath = Path.Combine(pathDir, otherBinaryName);
                            break;
                        }
                    }
                    if (nexttracePath != null) break;
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
            stopRequested = false;
            appStartRaised = false;
            errorOutputCount = 0;
            Task.Run(() =>
            {
                Console.WriteLine($"Using NextTrace: {nexttracePath}");

#if NET8_0_OR_GREATER
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
                    Array.Find(extraArgs, e => e == "-T" || e == "-U") != null &&
                    Environment.UserName != "root")
                {
                    FileSystemInfo fa = new FileInfo(nexttracePath);
                    if ((fa.UnixFileMode & UnixFileMode.SetUser) == 0) 
                    {
                        App.app.Invoke(() => {
                            DialogResult dr = MessageBox.Show(Resources.MISSING_COMP_PRIV_TEXT, Resources.TCP_UDP_REQUIREMENTS_TITLE, MessageBoxButtons.YesNo);
                            if (dr == DialogResult.Yes)
                            {
                                platformService.RestartAsAdministrator(host, () => {
                                    MessageBox.Show(Resources.RESTART_AS_ADMIN_FAILED, Resources.TCP_UDP_REQUIREMENTS_TITLE, MessageBoxButtons.OK);
                                    Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace/wiki/How-to-manually-set-the-required-permissions-for-TCP-UDP-traceroute-on-macOS-and-Linux") { UseShellExecute = true });
                                });
                            } else {
                                Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace/wiki/How-to-manually-set-the-required-permissions-for-TCP-UDP-traceroute-on-macOS-and-Linux") { UseShellExecute = true });
                            }
                        });
                        Status = AppStatus.Quit;
                        AppQuit?.Invoke(this, new AppQuitEventArgs(0));
                        return;
                    }
                }
#endif
                if (stopRequested)
                {
                    Status = AppStatus.Quit;
                    AppQuit?.Invoke(this, new AppQuitEventArgs(0));
                    return;
                }

                bool useNativeMtr = MTRMode && SupportsNativeMtr();
                StartNextTraceProcess(host, MTRMode, extraArgs, useNativeMtr);
            });
        }

        private void StartNextTraceProcess(string host, bool mtrMode, string[] extraArgs, bool useNativeMtr)
        {
            if (stopRequested)
            {
                Status = AppStatus.Quit;
                AppQuit?.Invoke(this, new AppQuitEventArgs(0));
                return;
            }

            string arguments;
            if (mtrMode && useNativeMtr)
            {
                // Native MTR raw mode emits the same 12-column stream consumed by
                // ProcessLine. Ignore the configured query count so the stream
                // remains continuous until the user stops it.
                arguments = ArgumentBuilder(
                    host,
                    extraArgs.Concat(new string[] { "--mtr" }).ToArray(),
                    new string[] { "queries" });
            }
            else if (mtrMode)
            {
                // Compatibility path used by pre-v1.5.2 and by binaries whose
                // native MTR flavor cannot start.
                arguments = ArgumentBuilder(
                    host,
                    extraArgs.Concat(new string[] { "--queries 1" }).ToArray(),
                    new string[] { "queries" });
            }
            else
            {
                arguments = ArgumentBuilder(host, extraArgs);
            }

            var process = new Process
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

            AddEnvironmentVariable(process, "NEXTTRACE_IPINSIGHT_TOKEN", UserSettings.IPInsightToken);
            AddEnvironmentVariable(process, "NEXTTRACE_IPINFO_TOKEN", UserSettings.IPInfoToken);
            AddEnvironmentVariable(process, "NEXTTRACE_CHUNZHENURL", UserSettings.ChunZhenEndpoint);
            AddEnvironmentVariable(process, "NEXTTRACE_HOSTPORT", UserSettings.NextTrace_HOSTPORT);
            AddEnvironmentVariable(process, "NEXTTRACE_PROXY", UserSettings.NextTraceProxy);
            AddEnvironmentVariable(process, "NEXTTRACE_POWPROVIDER", UserSettings.POWProvider);
            AddEnvironmentVariable(process, "NEXTTRACE_IPAPI_BASE", UserSettings.IPAPI_Base);
            if (SupportsApiV4())
                AddEnvironmentVariable(process, "NEXTTRACE_API_V4_TOKEN", UserSettings.NextTraceAPIV4Token);

            if (mtrMode && !useNativeMtr)
                process.StartInfo.EnvironmentVariables.Add("NEXTTRACE_UNINTERRUPTED", "1");

            int nativeOutputSeen = 0;
            int nativeDestinationHop = 0;
            int fallbackStarted = 0;
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null) return;

                // 去除输出中的控制字符
                string line = AnsiEscapePattern.Replace(e.Data, "");

                Match match1 = RawRowPattern.Match(line);
                if (match1.Success)
                {
                    TracerouteResult result = ProcessLine(line, host);
                    if (result == null)
                    {
                        if (useNativeMtr &&
                            Interlocked.CompareExchange(ref nativeOutputSeen, 0, 0) == 0)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                            }
                        }
                        else
                        {
                            HandleExceptionalOutput(false, line);
                        }
                        return;
                    }

                    if (useNativeMtr)
                        Interlocked.Exchange(ref nativeOutputSeen, 1);

                    int hopNumber;
                    if (useNativeMtr && int.TryParse(result.No, out hopNumber))
                    {
                        int finalHop = Interlocked.CompareExchange(ref nativeDestinationHop, 0, 0);
                        if (finalHop > 0 && hopNumber > finalHop)
                            return;

                        if (result.IsDestination &&
                            (finalHop == 0 || hopNumber < finalHop))
                        {
                            Interlocked.Exchange(ref nativeDestinationHop, hopNumber);
                        }
                    }

                    // Results form a stream in MTR mode. Raising an event avoids
                    // retaining every raw row for the lifetime of the process.
                    ResultReceived?.Invoke(this, new TracerouteResultEventArgs(result));
                    return;
                }

                if (line.StartsWith("NextTrace ")) return;
                if (line.IndexOf("hops max") > -1) return;
                if (line.StartsWith("IP Geo Data Provider")) return;
                if (line.StartsWith("[NextTrace API]")) return;

                // Do not expose errors from a failed native-MTR probe. If it exits
                // before producing a raw row, the Exited handler silently starts
                // the compatibility implementation instead.
                if (!useNativeMtr || Interlocked.CompareExchange(ref nativeOutputSeen, 0, 0) != 0)
                    HandleExceptionalOutput(false, line);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null) return;
                if (!useNativeMtr || Interlocked.CompareExchange(ref nativeOutputSeen, 0, 0) != 0)
                    HandleExceptionalOutput(true, e.Data);
            };
            process.Exited += (sender, e) =>
            {
                try
                {
                    // Ensure all asynchronous stdout/stderr callbacks have
                    // completed before AppQuit stops the UI batching timer.
                    process.WaitForExit();
                }
                catch
                {
                }

                int exitCode;
                try
                {
                    exitCode = process.ExitCode;
                }
                catch
                {
                    exitCode = -1;
                }

                if (useNativeMtr &&
                    !stopRequested &&
                    Interlocked.CompareExchange(ref nativeOutputSeen, 0, 0) == 0)
                {
                    Debug.Print("Native NextTrace MTR unavailable; using compatibility mode.");
                    errorOutputCount = 0;
                    process.Dispose();
                    if (Interlocked.Exchange(ref fallbackStarted, 1) == 0)
                        StartNextTraceProcess(host, mtrMode, extraArgs, false);
                    return;
                }

                Debug.Print("Exited");
                Status = AppStatus.Quit;
                AppQuit?.Invoke(this, new AppQuitEventArgs(exitCode));
                process.Dispose();
            };

            process.EnableRaisingEvents = true;
            lock (processLock)
            {
                _process = process;
            }

            if (stopRequested)
            {
                process.Dispose();
                Status = AppStatus.Quit;
                AppQuit?.Invoke(this, new AppQuitEventArgs(0));
                return;
            }

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Status = AppStatus.Start;
                if (!appStartRaised)
                {
                    appStartRaised = true;
                    AppStart?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                process.Dispose();
                if (useNativeMtr && !stopRequested)
                {
                    Debug.Print("Native NextTrace MTR failed to start; using compatibility mode.");
                    if (Interlocked.Exchange(ref fallbackStarted, 1) == 0)
                        StartNextTraceProcess(host, mtrMode, extraArgs, false);
                    return;
                }

                Status = AppStatus.Quit;
                ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(true, exception.Message));
                AppQuit?.Invoke(this, new AppQuitEventArgs(-1));
            }
        }

        private static void AddEnvironmentVariable(Process process, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                process.StartInfo.EnvironmentVariables[name] = value;
        }

        private void HandleExceptionalOutput(bool isErrorOutput, string output)
        {
            ExceptionalOutput?.Invoke(this, new ExceptionalOutputEventArgs(isErrorOutput, output));
            if (errorOutputCount < 100)
            {
                errorOutputCount++;
            }
            else
            {
                Kill();
            }
        }

        private bool SupportsNativeMtr()
        {
            Version version = DetectNextTraceVersion();
            return version != null && version.CompareTo(NativeMtrMinimumVersion) >= 0;
        }

        private bool SupportsApiV4()
        {
            if (string.IsNullOrWhiteSpace(UserSettings.NextTraceAPIV4Token))
                return false;

            DateTimeOffset expiresAt;
            if (!string.IsNullOrWhiteSpace(UserSettings.NextTraceAPIV4TokenExpiresAt) &&
                DateTimeOffset.TryParse(UserSettings.NextTraceAPIV4TokenExpiresAt, out expiresAt) &&
                expiresAt <= DateTimeOffset.UtcNow)
            {
                // Let NextTrace fall back to its v3 provider instead of repeatedly
                // sending a known-expired v4 credential.
                return false;
            }

            Version version = DetectNextTraceVersion();
            return version != null && version.CompareTo(ApiV4MinimumVersion) >= 0;
        }

        private Version DetectNextTraceVersion()
        {
            if (versionDetectionAttempted)
                return detectedVersion;

            versionDetectionAttempted = true;
            var output = new StringBuilder();
            var outputLock = new object();
            try
            {
                using (var versionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = nexttracePath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.GetEncoding(65001)
                    }
                })
                {
                    versionProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (outputLock)
                                output.AppendLine(e.Data);
                        }
                    };
                    versionProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (outputLock)
                                output.AppendLine(e.Data);
                        }
                    };
                    versionProcess.Start();
                    versionProcess.BeginOutputReadLine();
                    versionProcess.BeginErrorReadLine();
                    if (!versionProcess.WaitForExit(3000))
                    {
                        versionProcess.Kill();
                        return null;
                    }
                    versionProcess.WaitForExit();
                }
            }
            catch (Exception exception)
            {
                Debug.Print("Unable to detect NextTrace version: " + exception.Message);
                return null;
            }

            string versionOutput;
            lock (outputLock)
                versionOutput = output.ToString();
            detectedVersion = ParseNextTraceVersion(versionOutput);
            return detectedVersion;
        }

        internal static Version ParseNextTraceVersion(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            Match match = Regex.Match(
                output,
                @"(?<!\d)v?(\d+)\.(\d+)\.(\d+)(?:[-+][0-9A-Za-z.-]+)?",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            Version version;
            return Version.TryParse(
                match.Groups[1].Value + "." +
                match.Groups[2].Value + "." +
                match.Groups[3].Value,
                out version)
                ? version
                : null;
        }
        private TracerouteResult ProcessLine(string line, string destination)
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
            bool IsDestination = false;
            string[] LineData = line.Split('|');
            if (LineData.Length < 2)
                return null;

            No = LineData[0];
            if (LineData[1] == "*")
            {
                // NextTrace intentionally emits compact timeout rows such as
                // "2|*||||||" (8 fields). They are valid raw output even though
                // successful rows carry the full 12 fields.
                if (LineData.Length < 8)
                    return null;
                Time = "*";
            }
            else
            {
                if (LineData.Length < 12)
                    return null;
                IP = LineData[1];
                Time = LineData[3];
                Geolocation = LineData[5] + " " + LineData[6] + " " + LineData[7] + " " + LineData[8];
                AS = LineData[4];
                Hostname = LineData[2];
                Organization = LineData[9];
                Latitude = LineData[10];
                Longitude = LineData[11];
                IsDestination = string.Equals(IP, destination, StringComparison.OrdinalIgnoreCase);
            }

            // 匹配特定网络地址
            if (PrivateAddressPattern.IsMatch(IP))
            {
                Geolocation = Resources.PRIVATE_ADDR;
            }
            if (SharedAddressPattern.IsMatch(IP))
            {
                Geolocation = Resources.SHARED_ADDR;
            }
            if (LinkLocalAddressPattern.IsMatch(IP))
            {
                Geolocation = Resources.LINKLOCAL_ADDR;
            }
            if (LoopbackAddressPattern.IsMatch(IP))
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


            return new TracerouteResult(No, IP, Time, Geolocation, AS, Hostname, Organization, Latitude, Longitude, IsDestination);
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
            stopRequested = true;
            try
            {
                lock (processLock)
                {
                    if (_process != null && !_process.HasExited)
                        _process.Kill();
                }
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
