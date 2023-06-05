using Advexp;
using Eto.Drawing;
using Eto.Forms;
using System;
using System.Configuration;
using System.Globalization;
using System.Windows.Input;

namespace OpenTrace
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            UserSettings.LoadSettings();
            
            if (UserSettings.language != "")
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(UserSettings.language);
            }
            if(UserSettings.mapProvider == "")
            {
                // 本地化地图供应商设置
                if (System.Threading.Thread.CurrentThread.CurrentUICulture.Name == "zh-CN")
                {
                    UserSettings.mapProvider = "baidu"; 
                }
                else
                {
                    UserSettings.mapProvider = "google";
                }
            }
            
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
