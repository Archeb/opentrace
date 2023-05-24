using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Configuration;
using Resources = traceroute.Properties.Resources;

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
            if (appSettings.Count != 0)
            {
                foreach (var key in appSettings.AllKeys)
                {
                    TextBox settingTextBox = this.FindChild<TextBox>(key);
                    if (settingTextBox != null)
                    {
                        settingTextBox.Text = appSettings[key];
                    }
                    CheckBox settingCheckBox = this.FindChild<CheckBox>(key);
                    if(settingCheckBox != null)
                    {
                        settingCheckBox.Checked = Convert.ToBoolean(appSettings[key]);
                    }
                    DropDown settingDropDown = this.FindChild<DropDown>(key);
                    if (settingDropDown != null)
                    {
                        settingDropDown.SelectedKey = appSettings[key];
                    }
                }
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void SaveButton_Click(object sender, EventArgs e)
        {

            foreach (TextBox setting in this.Children.OfType<TextBox>())
            {
                AddUpdateAppSettings(setting.ID, setting.Text);
            }
            foreach (CheckBox setting in this.Children.OfType<CheckBox>())
            {
                AddUpdateAppSettings(setting.ID, setting.Checked.ToString());
            }
            foreach (DropDown setting in this.Children.OfType<DropDown>())
            {
                AddUpdateAppSettings(setting.ID, setting.SelectedKey);
            }
            Close();
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
                MessageBox.Show(Resources.ERR_WRITING_SETTTINGS);
            }
        }
    }
}
