using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using NextTrace;
using System.Diagnostics;

namespace OpenTrace
{
    public partial class PreferencesDialog : Dialog
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private TextOutputForm textOutputForm = new TextOutputForm();
        private static NextTraceWrapper CurrentInstance { get; set; }
        UserSettings userSettings = new UserSettings();
        public PreferencesDialog()
        {
            XamlReader.Load(this);
            ApplyUserSettings();
        }

        private void ApplyUserSettings()
        {
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
            this.FindChild<NumericStepper>("maskedHops").Value = UserSettings.maskedHops;
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
            UserSettings.maskedHops = (int)this.FindChild<NumericStepper>("maskedHops").Value;
            UserSettings.SaveSettings();
            IPDBLoader.Load();
            Close();
        }

        private void HandleMMDBSelect(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filters.Add(new FileFilter("MaxMind DB", ".mmdb"));
            openFileDialog.Filters.Add(new FileFilter("All Files", ".*"));
            openFileDialog.CheckFileExists = true;
            openFileDialog.MultiSelect = false;
            openFileDialog.Title = "Select MaxMind DB";
            openFileDialog.ShowDialog(this);
            if (openFileDialog.FileName != null && openFileDialog.FileName != "")
            {
                TextBox settingTextBox = this.FindChild<TextBox>("localDBPath");
                settingTextBox.Text = openFileDialog.FileName;
            }
        }

        private void HandleMMDBPreview(object sender, EventArgs e)
        {
            TextBox settingTextBox = this.FindChild<TextBox>("localDBPath");
            if (settingTextBox == null || settingTextBox.Text == "")
            {
                return;
            }
            var oldPath = UserSettings.localDBPath;
            UserSettings.localDBPath = settingTextBox.Text;
            IPDBLoader.Load();
            settingTextBox = this.FindChild<TextBox>("testMMDBIP");
            if (settingTextBox == null || settingTextBox.Text == "")
            {
                return;
            }
            var result = IPDBLoader.DB.Find<Dictionary<string, object>>(IPAddress.Parse(settingTextBox.Text));
            textOutputForm.ClearOutput();
            reduceResult(0, result);
            textOutputForm.Show();
        }

        private void reduceResult(int depth, Dictionary<string, object> result)
        {
            var prefix = Enumerable.Range(1, depth).Aggregate("", (current, _) => current + "  ");
            foreach (var item in result)
            {
                if (item.Value is Dictionary<string, object>)
                {
                    textOutputForm.AppendOutput(prefix + (item.Key + ": "));
                    reduceResult(depth + 1, (Dictionary<string, object>)item.Value);
                }
                else if (item.Value is List<object>)
                {
                    textOutputForm.AppendOutput(prefix + (item.Key + ": "));
                    reduceResult(depth + 1, (List<object>)item.Value);
                }
                else
                {
                    textOutputForm.AppendOutput(prefix + (item.Key + ": " + item.Value));
                }
            }
        }

        private void reduceResult(int depth, List<object> result)
        {
            var prefix = Enumerable.Range(1, depth).Aggregate("", (current, _) => current + "  ");
            var i = 0;
            foreach (var item in result)
            {
                if (item is Dictionary<string, object>)
                {
                    textOutputForm.AppendOutput(prefix + (i + ": "));
                    reduceResult(depth + 1, (Dictionary<string, object>)item);
                }
                else if (item is List<object>)
                {
                    textOutputForm.AppendOutput(prefix + (i + ": "));
                    reduceResult(depth + 1, (List<object>)item);
                }
                else
                {
                    textOutputForm.AppendOutput(prefix + item);
                }
                i++;
            }
        }

        private void HandleMMDBPreset(object sender, EventArgs e)
        {

            var setting = this.FindChild<DropDown>("localDBPreset");
            switch (setting.SelectedKey)
            {
                case "geoip2-city":
                    UserSettings.localDBAddr = "{.country.names.zh-CN} {.subdivisions.0.names.zh-CN} {.city.names.zh-CN}";
                    UserSettings.localDBOrg = "";
                    UserSettings.localDBLat = "{.location.latitude}";
                    UserSettings.localDBLon = "{.location.longitude}";
                    UserSettings.localDBASN = "";
                    UserSettings.localDBHostname = "";
                    break;
                case "ipinfo-loc":
                    UserSettings.localDBAddr = "{.country} {.region} {.city}";
                    UserSettings.localDBOrg = "";
                    UserSettings.localDBLat = "{.lat}";
                    UserSettings.localDBLon = "{.lng}";
                    UserSettings.localDBASN = "";
                    UserSettings.localDBHostname = "";
                    break;
                case "ipinfo-org":
                    UserSettings.localDBAddr = "";
                    UserSettings.localDBOrg = "{.name} {.hosting} {.domain}";
                    UserSettings.localDBLat = "";
                    UserSettings.localDBLon = "";
                    UserSettings.localDBASN = "";
                    UserSettings.localDBHostname = "";
                    break;
                default:
                    return;
            }
            ApplyUserSettings();
        }
    }
}
