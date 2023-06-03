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
            
            if (UserSettings.language != null)
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(UserSettings.language);
            }
            
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
