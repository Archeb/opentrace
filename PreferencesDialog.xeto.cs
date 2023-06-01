using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Configuration;
using OpenTrace.Properties;
using NextTrace;

namespace OpenTrace
{
    public partial class PreferencesDialog : Dialog
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        public PreferencesDialog()
        {
            XamlReader.Load(this);
            
            foreach (SettingsProperty setting in UserSettings.Default.Properties)
            {
                TextBox settingTextBox = this.FindChild<TextBox>(setting.Name);
                if (settingTextBox != null)
                {
                settingTextBox.Text = (string)UserSettings.Default[setting.Name];
                }
                CheckBox settingCheckBox = this.FindChild<CheckBox>(setting.Name);
                if(settingCheckBox != null)
                {
                    settingCheckBox.Checked = (bool)UserSettings.Default[setting.Name];
            }
                DropDown settingDropDown = this.FindChild<DropDown>(setting.Name);
                if (settingDropDown != null)
                {
                    settingDropDown.SelectedKey = (string)UserSettings.Default[setting.Name];
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
                UserSettings.Default[setting.ID] = setting.Text;
            }
            foreach (CheckBox setting in this.Children.OfType<CheckBox>())
            {
                UserSettings.Default[setting.ID] = setting.Checked;
            }
            foreach (DropDown setting in this.Children.OfType<DropDown>())
            {
                UserSettings.Default[setting.ID] = setting.SelectedKey;
            }
            UserSettings.Default.Save();
            Close();
        }
    }
}
