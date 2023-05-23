using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.ComponentModel;

namespace traceroute
{
    public partial class PreferencesForm : Form
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        public PreferencesForm()
        {
            Title = "Preferences";
            MinimumSize = new Size(600, 400);
            Closing += PreferencesForm_Closing;

            // create layout
            var layout = new TableLayout
            {
                Padding = new Padding(10), // padding around cells
                Spacing = new Size(5, 5), // spacing between each cell
                Rows = {
                    new TableRow {
                        Cells={
                        new TableLayout{
                            Spacing = new Size(10,10),
                            Rows =
                            {
                                new TableRow
                                {
                                    Cells =
                                    {
                                        new TableCell(
                                            new DropDown
                                            {
                                                Items =
                                                {
                                                    new ListItem{Text="A"},
                                                    new ListItem{Text="B"},
                                                    new ListItem{Text="C"},
                                                },
                                                SelectedIndex=0,
                                                ToolTip="СЎПо"
                                            }
                                            )
                                    }
                                }
                            }
                        }
                    }
                    },
                    new TableRow {
                        ScaleHeight = true,
                        Cells = {
                                new TextArea()
                        }
                    },
                }
            };
            Content = layout;
        }

        private void PreferencesForm_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Visible = false;
        }

    }
}
