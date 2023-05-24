using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Eto.Serialization.Xaml;
using System;

namespace traceroute
{
    public partial class PreferencesDialog : Dialog
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        public PreferencesDialog()
        {
            XamlReader.Load(this);
            
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
    }
}
