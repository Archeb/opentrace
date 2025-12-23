using Eto.Forms;
using System;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTrace.UI;
using OpenTrace.Infrastructure;

namespace OpenTrace
{
    
    class App 
    {
        public static Application app;
        /// <summary>
        /// 命令行参数，用于快速启动 traceroute
        /// </summary>
        public static string[] CommandLineArgs { get; set; }
        /// <summary>
        /// 命令行中的额外参数（非目标地址），直接传递给 nexttrace
        /// </summary>
        public static string[] CommandLineExtraArgs { get; set; }
        /// <summary>
        /// 命令行中指定的目标地址
        /// </summary>
        public static string CommandLineTarget { get; set; }
    }

    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            UserSettings.LoadSettings();
            
            if (!string.IsNullOrWhiteSpace(UserSettings.language))
            {
                CultureInfo.CurrentUICulture = new CultureInfo(UserSettings.language);
            }
#if NET8_0_OR_GREATER
            // 为 macOS 载入正确的 locale
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var asp = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        ArgumentList = { "-e", "user locale of (get system info)" },
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        CreateNoWindow = true
                    }
                };
                asp.Start();
                try
                {
                    var line = asp.StandardOutput.ReadLine()?.Replace("_","-");
                    var curCulture = new CultureInfo(line?.Trim()??"");
                    CultureInfo.CurrentUICulture = curCulture;
                }
                catch (Exception e) {}
            }
#endif

            // 本地化设置
            if (CultureInfo.CurrentUICulture.Name == "zh-CN" && TimeZoneInfo.Local.Id == "China Standard Time")
            {
                if (UserSettings.mapProvider == "" && UserSettings.mapProvider != null) UserSettings.mapProvider = "baidu";
                if (UserSettings.POWProvider == "" && UserSettings.POWProvider != null) UserSettings.POWProvider = "sakura";
            }
            else
            {
                if (UserSettings.mapProvider == "" && UserSettings.mapProvider != null) UserSettings.mapProvider = "google";
                if (UserSettings.POWProvider == "" && UserSettings.POWProvider != null) UserSettings.POWProvider = "api.leo.moe";
            }

            // 保存命令行参数以便在 GUI 启动后使用
            App.CommandLineArgs = args;
            
            // 解析命令行参数：识别目标地址和额外参数
            if (args.Length > 0)
            {
                ParseCommandLineArgs(args);
            }

            App.app = new Application(Eto.Platform.Detect);
            App.app.Run(new MainForm());
        }

        /// <summary>
        /// 解析命令行参数，分离目标地址和额外参数
        /// </summary>
        private static void ParseCommandLineArgs(string[] args)
        {
            var extraArgs = new System.Collections.Generic.List<string>();
            string target = null;
            
            // nexttrace 的选项参数列表（带值的参数）
            var argsWithValue = new System.Collections.Generic.HashSet<string>
            {
                "-p", "--port", "-q", "--queries", "-m", "--max-hops",
                "-d", "--data-provider", "-f", "--first", "-s", "--source",
                "-D", "--dev", "-z", "--send-time", "-i", "--ttl-time",
                "-g", "--language", "--file", "--from", "--pow-provider",
                "--parallel-requests", "--max-attempts", "--icmp-mode",
                "--source-port", "--listen", "--timeout", "--psize"
            };
            
            // 无值的开关参数
            var switchArgs = new System.Collections.Generic.HashSet<string>
            {
                "-h", "--help", "--init", "-4", "--ipv4", "-6", "--ipv6",
                "-T", "--tcp", "-U", "--udp", "-F", "--fast-trace",
                "-n", "--no-rdns", "-a", "--always-rdns", "-P", "--route-path",
                "-r", "--report", "--dn42", "-o", "--output", "-t", "--table",
                "--raw", "-j", "--json", "-c", "--classic", "-M", "--map",
                "-e", "--disable-mpls", "-V", "--version", "-C", "--no-color",
                "--deploy"
            };
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                
                if (arg.StartsWith("-"))
                {
                    // 这是一个选项参数
                    extraArgs.Add(arg);
                    
                    // 检查是否是带值的参数
                    if (argsWithValue.Contains(arg) && i + 1 < args.Length)
                    {
                        i++;
                        extraArgs.Add(args[i]);
                    }
                }
                else
                {
                    // 这是一个位置参数（目标地址）
                    if (target == null)
                    {
                        target = arg;
                    }
                    else
                    {
                        // 额外的位置参数也添加到额外参数中
                        extraArgs.Add(arg);
                    }
                }
            }
            
            App.CommandLineTarget = target;
            App.CommandLineExtraArgs = extraArgs.ToArray();
        }
    }
}
