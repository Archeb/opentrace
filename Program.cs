using Advexp;
using Eto.Drawing;
using Eto.Forms;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace OpenTrace
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            UserSettings.LoadSettings();
            
            if (UserSettings.language != "" && UserSettings.language != null)
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(UserSettings.language);
            }
            
            // 本地化设置
            if (System.Threading.Thread.CurrentThread.CurrentUICulture.Name == "zh-CN" && TimeZoneInfo.Local.Id == "China Standard Time")
            {
                if (UserSettings.mapProvider == "" && UserSettings.mapProvider != null) UserSettings.mapProvider = "baidu";
                if (UserSettings.POWProvider == "" && UserSettings.POWProvider != null) UserSettings.POWProvider = "sakura";
            }
            else
            {
                if (UserSettings.mapProvider == "" && UserSettings.mapProvider != null) UserSettings.mapProvider = "google";
                if (UserSettings.POWProvider == "" && UserSettings.POWProvider != null) UserSettings.POWProvider = "api.leo.moe";
            }

            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
