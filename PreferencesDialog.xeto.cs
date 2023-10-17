using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;
using System.Linq;
using NextTrace;
using System.Diagnostics;

namespace OpenTrace
{
    public partial class PreferencesDialog : Dialog
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        UserSettings userSettings = new UserSettings();
        public PreferencesDialog()
        {
            XamlReader.Load(this);
            foreach (var setting in userSettings.GetType().GetProperties())
            {
                TextBox settingTextBox = this.FindChild<TextBox>(setting.Name);
                if (settingTextBox != null)
                {
                    settingTextBox.Text = (string)setting.GetValue(userSettings, null);
                }
                CheckBox settingCheckBox = this.FindChild<CheckBox>(setting.Name);
                if (settingCheckBox != null)
                {
                    settingCheckBox.Checked = (bool)setting.GetValue(userSettings, null);
                }
                DropDown settingDropDown = this.FindChild<DropDown>(setting.Name);
                if (settingDropDown != null)
                {
                    settingDropDown.SelectedKey = (string)setting.GetValue(userSettings, null);
                }                
                TextArea settingTextArea = this.FindChild<TextArea>(setting.Name);
                if (settingTextArea != null)
                {
                    settingTextArea.Text = (string)setting.GetValue(userSettings, null);
                }
            }
            this.FindChild<NumericStepper>("gridSizePercentage").Value = UserSettings.gridSizePercentage * 100;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void SaveButton_Click(object sender, EventArgs e)
        {
            foreach (var setting in userSettings.GetType().GetProperties())
            {
                TextBox settingTextBox = this.FindChild<TextBox>(setting.Name);
                if (settingTextBox != null)
                {
                    setting.SetValue(userSettings, settingTextBox.Text);
                }
                CheckBox settingCheckBox = this.FindChild<CheckBox>(setting.Name);
                if (settingCheckBox != null)
                {
                    setting.SetValue(userSettings, settingCheckBox.Checked);
                }
                DropDown settingDropDown = this.FindChild<DropDown>(setting.Name);
                if (settingDropDown != null)
                {
                    setting.SetValue(userSettings, settingDropDown.SelectedKey);
                }
                TextArea settingTextArea = this.FindChild<TextArea>(setting.Name);
                if (settingTextArea != null)
                {
                    setting.SetValue(userSettings, settingTextArea.Text);
                }
            }
            UserSettings.gridSizePercentage = this.FindChild<NumericStepper>("gridSizePercentage").Value / 100;
            UserSettings.SaveSettings();
            Close();
        }
    }
}
