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
            var settings = Properties.UserSettings.Default;
            if (settings.settingsNeedUpgrade) {
                settings.Upgrade();
                settings.settingsNeedUpgrade = false;
                settings.Save();
            }
            
            if (Properties.UserSettings.Default.language != "")
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.UserSettings.Default.language);
            }
            
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
