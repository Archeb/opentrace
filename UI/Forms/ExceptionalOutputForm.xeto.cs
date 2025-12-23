using Eto.Forms;
using Eto.Serialization.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace OpenTrace.UI.Forms
{
    public partial class ExceptionalOutputForm : Form
    {
        private TextArea OutputContainer;
        public ExceptionalOutputForm()
        {
            XamlReader.Load(this);
            OutputContainer = this.FindChild<TextArea>("OutputContainer");
        }
        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void Form_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Visible = false;
        }
        private void ReportButton_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace/issues/new/choose") { UseShellExecute = true });
        }
        public void AppendOutput(string Output)
        {
            OutputContainer.Text += Output + Environment.NewLine;
        }
    }
}
