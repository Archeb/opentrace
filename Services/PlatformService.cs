using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Eto.Forms;
using OpenTrace.Infrastructure;
using Resources = OpenTrace.Properties.Resources;

namespace OpenTrace.Services
{
    /// <summary>
    /// 平台相关服务，处理操作系统差异化的逻辑
    /// </summary>
    public class PlatformService
    {
        /// <summary>
        /// 执行平台特定的初始化检查
        /// </summary>
        public void RunPlatformChecks()
        {
            // macOS 被隔离，请求释放
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && 
                AppDomain.CurrentDomain.BaseDirectory.StartsWith("/private/var/folders"))
            {
                Application.Instance.Invoke(() =>
                {
                    MessageBox.Show(Resources.MACOS_QUARANTINE);
                });
            }

            // Windows ICMP 防火墙规则
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
                UserSettings.hideAddICMPFirewallRule != true)
            {
                TryAddICMPFirewallRule();
            }
        }

        /// <summary>
        /// 尝试添加 Windows ICMP 防火墙规则
        /// </summary>
        /// <returns>是否成功添加规则</returns>
        public bool TryAddICMPFirewallRule()
        {
            // 提示 Windows 用户添加防火墙规则放行 ICMP 
            if (MessageBox.Show(Resources.ASK_ADD_ICMP_FIREWALL_RULE, MessageBoxButtons.YesNo, MessageBoxType.Question) == DialogResult.Yes)
            {
                // 以管理员权限运行命令
                var allowIcmp = new Process();
                allowIcmp.StartInfo.FileName = "cmd.exe";
                allowIcmp.StartInfo.UseShellExecute = true;
                allowIcmp.StartInfo.Verb = "runas";
                allowIcmp.StartInfo.Arguments = "/c \"netsh advfirewall firewall add rule name=\"\"\"All ICMP v4 (NextTrace)\"\"\" dir=in action=allow protocol=icmpv4:any,any && netsh advfirewall firewall add rule name=\"\"\"All ICMP v6 (NextTrace)\"\"\" dir=in action=allow protocol=icmpv6:any,any\"";
                try
                {
                    allowIcmp.Start();
                    UserSettings.hideAddICMPFirewallRule = true;
                    UserSettings.SaveSettings();
                    return true;
                }
                catch (Win32Exception)
                {
                    MessageBox.Show(Resources.FAILED_TO_ADD_RULES, MessageBoxType.Error);
                    return false;
                }
            }
            else
            {
                UserSettings.hideAddICMPFirewallRule = true;
                UserSettings.SaveSettings();
                return false;
            }
        }

        /// <summary>
        /// 检查当前平台是否支持指定的协议
        /// </summary>
        /// <param name="protocol">协议名称</param>
        /// <returns>是否支持</returns>
        public bool IsProtocolSupported(string protocol)
        {
            // Windows 不支持 TCP/UDP 协议（除非安装 Npcap）
            if (protocol != "ICMP" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查当前进程是否以管理员权限运行
        /// </summary>
        /// <returns>是否为管理员</returns>
        public bool IsAdministrator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// 检查 Npcap 是否已安装
        /// </summary>
        /// <returns>是否已安装</returns>
        public bool IsNpcapInstalled()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            // 检查 Npcap 的安装路径
            string npcapPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "Npcap"
            );

            if (System.IO.Directory.Exists(npcapPath))
            {
                return true;
            }

            // 检查注册表
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Npcap"))
                {
                    if (key != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // 忽略注册表访问错误
            }

            return false;
        }

        /// <summary>
        /// 检查 WinDivert.dll 是否存在
        /// </summary>
        /// <returns>是否存在</returns>
        public bool IsWinDivertInstalled()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            // 检查应用程序目录下的 WinDivert.dll
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string winDivertPath = System.IO.Path.Combine(appDir, "WinDivert.dll");
            
            if (System.IO.File.Exists(winDivertPath))
            {
                return true;
            }

            // 检查 64 位版本
            string winDivert64Path = System.IO.Path.Combine(appDir, "WinDivert64.sys");
            if (System.IO.File.Exists(winDivert64Path))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查 Windows TCP/UDP 模式所需的所有条件
        /// </summary>
        /// <returns>元组：(是否满足所有条件, 是否安装Npcap, 是否有WinDivert)</returns>
        public (bool AllMet, bool HasNpcap, bool HasWinDivert) CheckWindowsTcpUdpRequirements()
        {
            bool hasNpcap = IsNpcapInstalled();
            bool hasWinDivert = IsWinDivertInstalled();
            
            bool allMet = hasWinDivert && hasNpcap;
            
            return (allMet, hasNpcap, hasWinDivert);
        }

        /// <summary>
        /// 以管理员身份重新启动应用程序
        /// </summary>
        /// <param name="arguments">启动参数</param>
        /// <param name="onFailed">提权失败时的回调（用于 macOS/Linux 异步场景）</param>
        public void RestartAsAdministrator(string arguments = "", Action onFailed = null)
        {
            // 获取当前进程的可执行文件路径
            string exePath = Process.GetCurrentProcess().MainModule?.FileName;
            
            if (string.IsNullOrEmpty(exePath))
            {
                onFailed?.Invoke();
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Arguments = arguments,
                        Verb = "runas"
                    };

                    Process.Start(startInfo);
                    
                    // 关闭当前应用程序
                    Application.Instance.Quit();
                }
                catch (Win32Exception)
                {
                    // 用户取消了 UAC 提示
                    onFailed?.Invoke();
                }
                catch (Exception)
                {
                    onFailed?.Invoke();
                }
#if NET8_0_OR_GREATER
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string shellCommand = $"'{exePath}' {arguments} > /dev/null 2>&1 &";

                string escapedShellCommand = shellCommand
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\""); 

                string finalScript = $"do shell script \"{escapedShellCommand}\" with administrator privileges with prompt \"{Resources.TCP_UDP_RUN_AS_ADMIN}\"";

                var elvp = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        ArgumentList = { "-e", finalScript },
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true 
                };

                elvp.Exited += (sender, e) =>
                {
                    Application.Instance.AsyncInvoke(() =>
                    {
                        if (elvp.ExitCode == 0)
                        {
                            Application.Instance.Quit();
                        }
                        else
                        {
                            Console.WriteLine($"Failed to run as administrator: ExitCode: {elvp.ExitCode}");
                            onFailed?.Invoke();
                        }
                        
                        elvp.Dispose();
                    });
                };

                try
                {
                    elvp.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to run as administrator: {ex.Message}");
                    onFailed?.Invoke();
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // 使用 pkexec 请求管理员权限
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "pkexec",
                        ArgumentList = { exePath, arguments },
                        UseShellExecute = false
                    };

                    var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                    
                    process.Exited += (sender, e) =>
                    {
                        if (process.ExitCode == 0)
                        {
                            Application.Instance.Invoke(() =>
                            {
                                Application.Instance.Quit();
                            });
                        }
                        else
                        {
                            Application.Instance.Invoke(() =>
                            {
                                onFailed?.Invoke();
                            });
                        }
                    };

                    process.Start();
                }
                catch
                {
                    onFailed?.Invoke();
                }
#endif
            }
        }

        /// <summary>
        /// 检测系统是否启用暗色模式
        /// </summary>
        /// <returns>是否为暗色模式</returns>
        public bool IsSystemDarkModeEnabled()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return IsLinuxDarkMode();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return IsMacOSDarkMode();
                }
                // Windows 由于 WPF 限制，暂不支持暗色模式
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测 Linux (GNOME) 系统是否启用暗色模式
        /// </summary>
        private bool IsLinuxDarkMode()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = "get org.gnome.desktop.interface color-scheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null) return false;
                    
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    // GNOME 返回值可能是 'prefer-dark' 或 'default' 等
                    return output.Contains("dark");
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测 macOS 系统是否启用暗色模式
        /// </summary>
        private bool IsMacOSDarkMode()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "defaults",
                    Arguments = "read -g AppleInterfaceStyle",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null) return false;
                    
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    // macOS 在暗色模式下返回 "Dark"，浅色模式下会报错（键不存在）
                    return output.Equals("Dark", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // 浅色模式下 defaults 命令会失败（键不存在），所以返回 false
                return false;
            }
        }
    }
}
