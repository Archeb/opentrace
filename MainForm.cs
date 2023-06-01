using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using OpenTrace.Properties;
using System.Linq;
using NextTrace;

namespace OpenTrace
{
    

    public partial class MainForm : Form
    {
        private ObservableCollection<TracerouteHop> tracerouteResultCollection = new ObservableCollection<TracerouteHop>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        private static double gridSizePercentage = 0.5;
        private ComboBox HostInputBox;
        private GridView tracerouteGridView;
        private CheckBox MTRMode;
        private WebView mapWebView;
        private DropDown dataProviderSelection;
        private DropDown protocolSelection;
        private Button startTracerouteButton;
        private bool gridResizing = false;
        private bool appForceExiting = false;

        public MainForm()
        {
            Title = Resources.APPTITLE;
            MinimumSize = new Size(860, 600);

            // 创建菜单项
            var newWindowCommand = new Command { MenuText = Resources.NEW, ToolBarText = Resources.NEW_WINDOW_TEXT, Shortcut = Application.Instance.CommonModifier | Keys.N };
            newWindowCommand.Executed += (sender, e) =>
            {
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            };

            // var exportCommand = new Command { MenuText = Resources.EXPORT};

            var quitCommand = new Command { MenuText = Resources.QUIT, Shortcut = Application.Instance.CommonModifier | Keys.Q };
            quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            var aboutCommand = new Command { MenuText = Resources.ABOUT };
            aboutCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace") { UseShellExecute = true });

            var preferenceCommand = new Command { MenuText = Resources.PREFERENCES };
            preferenceCommand.Executed += (sender, e) => new PreferencesDialog().ShowModal();

            // 创建菜单栏
            Menu = new MenuBar
            {
                Items =
                {
                    new SubMenuItem { Text = Resources.FILE, Items = {
                            newWindowCommand,
                            /* new SubMenuItem { Text = Resources.EXPORT_TO , Items = {
                                    new Command { MenuText = "HTML" },
                                    new Command { MenuText = "Plain text (CSV)" }
                            } }, */
                            preferenceCommand,
                            quitCommand
                        } },
                     new SubMenuItem { Text = Resources.HELP , Items = {
                             aboutCommand
                         } }
                }
            };

            // 创建控件
            HostInputBox = new ComboBox { Text = "" };
            MTRMode = new CheckBox { Text = Resources.MTR_MODE };
            startTracerouteButton = new Button { Text = Resources.START };
            protocolSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "ICMP" ,Key= ""},
                    new ListItem{Text = "TCP",Key = "-T" },
                    new ListItem{Text = "UDP",Key = "-U" },
                },
                SelectedIndex = 0,
                ToolTip = Resources.PROTOCOL_FOR_TRACEROUTING
            };
            dataProviderSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "LeoMoeAPI" ,Key= ""},
                    new ListItem{Text = "IPInfo",Key = "--data-provider IPInfo" },
                    new ListItem{Text = "IP.SB",Key = "--data-provider IP.SB" },
                    new ListItem{Text = "Ip2region",Key = "--data-provider Ip2region" },
                    new ListItem{Text = "IPInsight" ,Key = "--data-provider IPInsight" },
                    new ListItem{Text = "IPAPI.com" ,Key = "--data-provider IPAPI.com" },
                    new ListItem{Text = "IPInfoLocal" ,Key = "--data-provider IPInfoLocal" },
                    new ListItem{Text = "CHUNZHEN" , Key = "--data-provider CHUNZHEN"}
                },
                SelectedIndex = 0,
                ToolTip = Resources.IP_GEO_DATA_PROVIDER
            };

            tracerouteGridView = new GridView { DataStore = tracerouteResultCollection };
            AddGridColumnsTraceroute();

            mapWebView = new WebView
            {
                Url = new Uri("https://lbs.baidu.com/jsdemo/demo/webgl0_0.htm")
            };

            // 绑定控件事件
            SizeChanged += MainForm_SizeChanged;
            MouseDown += Dragging_MouseDown;
            MouseUp += Dragging_MouseUp;
            MouseMove += MainForm_MouseMove;
            tracerouteGridView.MouseUp += Dragging_MouseUp;
            tracerouteGridView.SelectedRowsChanged += TracerouteGridView_SelectedRowsChanged;
            startTracerouteButton.Click += StartTracerouteButton_Click;
            HostInputBox.KeyUp += HostInputBox_KeyUp;
            MTRMode.CheckedChanged += MTRMode_CheckedChanged;

            // 使用 Table 布局创建页面
            var layout = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows = {
                    new TableRow {
                        Cells = {
                        new TableLayout {
                            Spacing = new Size(10,10),
                            Rows =
                            {
                                new TableRow
                                {
                                    Cells =
                                    {
                                        new TableCell(HostInputBox,true),
                                        MTRMode,
                                        protocolSelection,
                                        dataProviderSelection,
                                        startTracerouteButton
                                    }
                                }
                            }
                        }
                    }
                    },
                    new TableRow {
                        Cells = {tracerouteGridView}
                    },
                    new TableRow{
                        Cells = {mapWebView}
                    },
                }
            };
            Content = layout;
        }

        private void MTRMode_CheckedChanged(object sender, EventArgs e)
        {
            if ((bool)MTRMode.Checked)
            {
                AddGridColumnsMTR();
            }
            else
            {
                AddGridColumnsTraceroute();
            }
        }

        private void HostInputBox_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.Key == Keys.Enter)
            {
                if (CurrentInstance != null)  StopTraceroute();
                StartTracerouteButton_Click(sender, e);
            }
        }

        private void TracerouteGridView_SelectedRowsChanged(object sender, EventArgs e)
        {
            FocusMapPoint(tracerouteGridView.SelectedRow);
        }

        private void StartTracerouteButton_Click(object sender, EventArgs e)
        {
            if (CurrentInstance != null)
            {
                StopTraceroute();
                return;
            }
            tracerouteResultCollection.Clear(); // 清空原有GridView
            ResetMap(); // 重置地图
            Title = Resources.APPTITLE;
            NextTraceWrapper instance;
            try
            {
                instance = new NextTraceWrapper();
            }
            catch (FileNotFoundException)
            {
                // 询问是否下载 NextTrace
                DialogResult dr = MessageBox.Show(Resources.MISSING_COMP_TEXT,
                     Resources.MISSING_COMP, MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://mtr.moe/") { UseShellExecute = true });
                }
                return;
            }
            HostInputBox.Items.Add(new ListItem { Text = HostInputBox.Text });
            CurrentInstance = instance;
            startTracerouteButton.Text = Resources.STOP;
            
            // 处理NextTrace实例发回的结果
            instance.Output.CollectionChanged += (sender, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Application.Instance.InvokeAsync(() =>
                    {
                        int HopNo = int.Parse(((TracerouteResult)e.NewItems[0]).No);
                        if (HopNo > tracerouteResultCollection.Count)
                        {
                            // 正常添加新的跳
                            tracerouteResultCollection.Add(new TracerouteHop((TracerouteResult)e.NewItems[0]));
                            UpdateMap((TracerouteResult)e.NewItems[0]);
                            tracerouteGridView.ScrollToRow(tracerouteResultCollection.Count - 1);
                        } else {
                            // 修改现有的跳
                            tracerouteResultCollection[HopNo - 1].HopData.Add((TracerouteResult)e.NewItems[0]);
                            tracerouteGridView.ReloadData(HopNo - 1);
                        }
                    });
                }
            };
            instance.HostResolved += (object sender, HostResolvedEventArgs e) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    Title = Resources.APPTITLE + ": " + e.Host + " (" + e.IP + ")";
                });
            };
            instance.ExceptionalOutput += (object sender, ExceptionalOutputEventArgs e) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    MessageBox.Show(e.Output, Resources.ERR_MSG, e.IsErrorOutput ? MessageBoxType.Warning : MessageBoxType.Information);
                });
            };
            instance.AppQuit += (object sender, AppQuitEventArgs e) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    if (appForceExiting != true)
                    {
                        // 主动结束
                        startTracerouteButton.Text = Resources.START;
                        CurrentInstance = null;
                        if (e.ExitCode != 0)
                        {
                            MessageBox.Show(Resources.EXCEPTIONAL_EXIT_MSG + e.ExitCode, MessageBoxType.Warning);
                        }
                    }
                    else
                    {
                        // 强制结束
                        appForceExiting = false;
                    }
                });
            };
            if ((bool)MTRMode.Checked)
            {
                instance.RunMTR(HostInputBox.Text, dataProviderSelection.SelectedKey);
            }
            else
            {
                instance.RunTraceroute(HostInputBox.Text, dataProviderSelection.SelectedKey);
            }
            
        }
        private void StopTraceroute()
        {
            appForceExiting = true;
            CurrentInstance.Kill();
            startTracerouteButton.Text = Resources.START;
            CurrentInstance = null;
        }

        /*
         * 处理拖拽调整 GridView 大小
         */
        private void Dragging_MouseUp(object sender, MouseEventArgs e)
        {
            gridResizing = false;
            mapWebView.Enabled = true;
        }

        private void Dragging_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Location.Y >= tracerouteGridView.Bounds.Bottom + 15 && e.Location.Y <= tracerouteGridView.Bounds.Bottom + 20)
            {
                gridResizing = true;
                mapWebView.Enabled = false;
            }

        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            // 设置鼠标指针
            if (e.Location.Y >= tracerouteGridView.Bounds.Bottom + 15 && e.Location.Y <= tracerouteGridView.Bounds.Bottom + 20)
            {
                this.Cursor = Cursors.SizeBottom;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }

            if (e.Buttons == MouseButtons.Primary && gridResizing)
            {
                if ((int)e.Location.Y > (tracerouteGridView.Bounds.Top + 100)) // 最小调整为100px
                {

                    tracerouteGridView.Height = (int)e.Location.Y - tracerouteGridView.Bounds.Top - 15;
                    gridSizePercentage = (double)tracerouteGridView.Height / (Height - 75); // 保存比例
                }
            }
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            int gridHeight;
            int totalHeight = this.Height - 75; // 减去边距和上面的文本框的75px
            gridHeight = (int)(totalHeight * gridSizePercentage);
            tracerouteGridView.Height = gridHeight; // 按比例还原高度
        }
        private void UpdateMap(TracerouteResult result)
        {
            // 把 Result 转换为 JSON
            string resultJson = JsonSerializer.Serialize(result);
            // 通过 ExecuteScriptAsync 把结果传进去
            mapWebView.ExecuteScriptAsync(@"window.opentrace.addHop(`" + resultJson + "`);");
        }
        private void FocusMapPoint(int hopNo)
        {
            mapWebView.ExecuteScriptAsync(@"window.opentrace.focusHop(" + hopNo + ");");
        }
        private void ResetMap()
        {
            // 重置或者初始化地图
            mapWebView.ExecuteScriptAsync(OpenTrace.Properties.Resources.baiduMap);
        }
        private void AddGridColumnsTraceroute()
        {
            tracerouteGridView.Columns.Clear();
            // 指定栏位数据源
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].No) },
                HeaderText = "#"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].IP) },
                HeaderText = "IP"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell
                {
                    Binding = Binding.Property<TracerouteHop, string>(r =>
                    String.Join(" / ", r.HopData.Select(d => d.Time))
                )
                },
                HeaderText = Resources.TIME_MS
            });
            // 合并位置和运营商
            if (UserSettings.Default.combineGeoOrg == true)
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].Geolocation + " " + r.HopData[0].Organization) },
                    HeaderText = Resources.GEOLOCATION
                });
            }
            else
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].Geolocation) },
                    HeaderText = Resources.GEOLOCATION
                });
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].Organization) },
                    HeaderText = Resources.ORGANIZATION
                });
            }
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.HopData[0].Hostname) },
                HeaderText = Resources.HOSTNAME
            });
        }
        private void AddGridColumnsMTR()
        {
            tracerouteGridView.Columns.Clear();
            // 指定栏位数据源
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.No) },
                HeaderText = "#"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.IP) },
                HeaderText = "IP"
            });
            // 合并位置和运营商
            if (UserSettings.Default.combineGeoOrg == true)
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.GeolocationAndOrganization) },
                    HeaderText = Resources.GEOLOCATION
                });
            }
            else
            {
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Geolocation) },
                    HeaderText = Resources.GEOLOCATION
                });
                tracerouteGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Organization) },
                    HeaderText = Resources.ORGANIZATION
                });
            }
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Loss.ToString()) },
                HeaderText = Resources.LOSS
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Sent.ToString()) },
                HeaderText = Resources.SENT
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Recv.ToString()) },
                HeaderText = Resources.RECV
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Last.ToString()) },
                HeaderText = Resources.LAST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Worst.ToString()) },
                HeaderText = Resources.WORST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Best.ToString()) },
                HeaderText = Resources.BEST
            });

            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Average.ToString("0.##")) },
                HeaderText = Resources.AVRG
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.StandardDeviation.ToString("0.##")) },
                HeaderText = Resources.STDEV
            });
            /* TODO
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => "TODO") },
                HeaderText = Resources.HISTORY
            }); */
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Hostname) },
                HeaderText = Resources.HOSTNAME
            });
        }
    }
}
