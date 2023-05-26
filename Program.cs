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
            
            if (Properties.UserSettings.Default.language != "")
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.UserSettings.Default.language);
            }
            
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
