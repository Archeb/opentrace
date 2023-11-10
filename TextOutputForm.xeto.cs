using Eto.Forms;
using Eto.Serialization.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace OpenTrace
{
    public partial class TextOutputForm : Form
    {
        private TextArea OutputContainer;
        public TextOutputForm()
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
        public void AppendOutput(string Output)
        {
            OutputContainer.Text += Output + Environment.NewLine;
        }
        public void ClearOutput()
        {
            OutputContainer.Text = "";
        }
    }
}
