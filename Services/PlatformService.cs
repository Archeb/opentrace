using System;
using System.ComponentModel;
using System.Diagnostics;
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
    }
}
