using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Configuration;

namespace traceroute
{
    public partial class PreferencesDialog : Dialog
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        public PreferencesDialog()
        {
            XamlReader.Load(this);
            var appSettings = ConfigurationManager.AppSettings;

            if (appSettings.Count == 0)
            {
                Debug.Print("AppSettings is empty.");
            }
            else
            {
                foreach (var key in appSettings.AllKeys)
                {
                    TextBox setting = this.FindChild<TextBox>(key);
                    if (setting != null)
                    {
                        setting.Text = appSettings[key];
                    }
                }
            }
        }

        private void PreferencesDialog_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Visible = false;
        }
        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void SaveButton_Click(object sender, EventArgs e)
        {

            IEnumerable<TextBox> TracerouteSettings = this.Children.OfType<TextBox>();
            foreach (TextBox setting in TracerouteSettings)
            {
                AddUpdateAppSettings(setting.ID, setting.Text);
            }
        }
        private static void ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? "Not Found";
                Console.WriteLine(result);
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
        }

        private static void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {
                MessageBox.Show("Error writing app settings");
            }
        }
    }
}
