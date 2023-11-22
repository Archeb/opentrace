using Advexp;
using Eto.Drawing;
using Eto.Forms;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using OpenTrace.Properties;

namespace OpenTrace
{
    
    class App 
    {
        public static Application app;
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

            App.app = new Application(Eto.Platform.Detect);
            App.app.Run(new MainForm());
        }
    }
}
