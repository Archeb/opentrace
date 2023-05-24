using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.Threading;
using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using System.IO;

namespace traceroute
{
    class TracerouteResult
    {
        public TracerouteResult(string no, string ip, string time, string geolocation, string ASNumber, string hostname, string organization, string latitude, string longitude)
        {
            No = no;
            IP = ip;
            Time = time;
            Geolocation = geolocation;
            AS = ASNumber;
            Hostname = hostname;
            Organization = organization;
            Latitude = latitude;
            Longitude = longitude;
        }
        public string No { get; set; }
        public string IP { get; set; }
        public string Time { get; set; }
        public string Geolocation { get; set; }
        public string AS { get; set; }
        public string Hostname { get; set; }
        public string Organization { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
    }

    public partial class MainForm : Form
    {
        private ObservableCollection<TracerouteResult> tracerouteResultCollection = new ObservableCollection<TracerouteResult>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        private static double gridSizePercentage = 0.5;
        private Dialog preferenceDialog = new PreferencesDialog();
        private TextBox IPTextBox;
        private GridView tracerouteGridView;
        private WebView mapWebView;
        private DropDown dataProviderSelection;
        private DropDown protocolSelection;
        private Button startTracerouteButton;
        private bool gridResizing = false;

        public MainForm()
        {
            Title = "OpenTrace";
            MinimumSize = new Size(860, 600);

            // 创建菜单项
            var newWindowCommand = new Command { MenuText = "&New", ToolBarText = "Create a new traceroute window.", Shortcut = Application.Instance.CommonModifier | Keys.N };
            newWindowCommand.Executed += (sender, e) =>
            {
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            };

            var exportCommand = new Command { MenuText = "&Export", ToolBarText = "Export" };

            var quitCommand = new Command { MenuText = "&Quit", Shortcut = Application.Instance.CommonModifier | Keys.Q };
            quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            var aboutCommand = new Command { MenuText = "&About..." };
            aboutCommand.Executed += (sender, e) => new AboutDialog().ShowDialog(this);

            var preferenceCommand = new Command { MenuText = "&Preferences" };
            preferenceCommand.Executed += (sender, e) => preferenceDialog.ShowModal();

            // 创建菜单栏
            Menu = new MenuBar
            {
                Items =
                {
                    new SubMenuItem { Text = "&File", Items = {
                            newWindowCommand,
                            new SubMenuItem { Text = "&Export To" , Items = {
                                    new Command { MenuText = "HTML" },
                                    new Command { MenuText = "Plain text (CSV)" }
                            } },
                        } },
                     new SubMenuItem { Text = "&Help" , Items = {
                             new SubMenuItem { Text = "&Language" , Items = {
                                    new Command { MenuText = "English" },
                                    new Command { MenuText = "简体中文" }
                            } },
                             aboutCommand
                         } }
                },
                ApplicationItems = { preferenceCommand },
                QuitItem = quitCommand
            };

            // 创建控件
            IPTextBox = new TextBox { Text = "" };

            startTracerouteButton = new Button { Text = "Start" };
            protocolSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "ICMP" ,Key= ""},
                    new ListItem{Text = "TCP",Key = "-T" },
                    new ListItem{Text = "UDP",Key = "-U" },
                },
                SelectedIndex = 0,
                ToolTip = "Protocol for tracerouting"
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
                ToolTip = "IP Geograph Data Provider"
            };

            tracerouteGridView = new GridView { DataStore = tracerouteResultCollection };

            // 绑定数据源
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.No) },
                HeaderText = "#"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.IP) },
                HeaderText = "IP"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.Time) },
                HeaderText = "Time(ms)"
            });
            /* 合并位置和运营商功能
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.Geolocation + " " + r.Organization) },
                HeaderText = "Geolocation"
            });
            */
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.Geolocation) },
                HeaderText = "Geolocation"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.Organization) },
                HeaderText = "Organization"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteResult, string>(r => r.Hostname) },
                HeaderText = "Hostname"
            });

            mapWebView = new WebView
            {
                Url = new Uri("https://lbs.baidu.com/jsdemo/demo/webgl0_0.htm")
            };

            // 绑定控件事件，允许拖拽改变 GridView 高度。
            SizeChanged += MainForm_SizeChanged;
            MouseDown += Dragging_MouseDown;
            MouseUp += Dragging_MouseUp;
            MouseMove += MainForm_MouseMove;
            tracerouteGridView.MouseUp += Dragging_MouseUp;
            tracerouteGridView.SelectedRowsChanged += TracerouteGridView_SelectedRowsChanged;
            startTracerouteButton.Click += StartTracerouteButton_Click;

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
                                        new TableCell(IPTextBox,true),
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

        private void TracerouteGridView_SelectedRowsChanged(object sender, EventArgs e)
        {
            FocusMapPoint(tracerouteGridView.SelectedRow);
        }

        private void StartTracerouteButton_Click(object sender, EventArgs e)
        {
            // 检查 nexttrace.exe 是否存在
            if (!File.Exists("nexttrace.exe"))
            {
                DialogResult dr = MessageBox.Show("nexttrace.exe is missing. Please put it in the same directory as the OpenTrace executable. Would you like to download it?",
                     "Missing Component", MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://mtr.moe/") { UseShellExecute = true });
                }
                return;
            }
            if (CurrentInstance != null)
            {
                CurrentInstance.Kill();
                startTracerouteButton.Text = "Start";
                CurrentInstance = null;
                return;
            }
            tracerouteResultCollection.Clear(); // 清空原有GridView
            ResetMap(); // 重置地图
            startTracerouteButton.Text = "Stop";
            var instance = new NextTraceWrapper(IPTextBox.Text + " --raw " + dataProviderSelection.SelectedKey);
            CurrentInstance = instance;
            instance.Output.CollectionChanged += (sender, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Application.Instance.InvokeAsync(() =>
                    {
                        tracerouteResultCollection.Add((TracerouteResult)e.NewItems[0]);
                        UpdateMap((TracerouteResult)e.NewItems[0]);
                        tracerouteGridView.ScrollToRow(tracerouteResultCollection.Count - 1);
                    });
                }
            };
            instance.OnAppQuit += (sender, e) =>
            {
                Application.Instance.InvokeAsync(() =>
                {
                    startTracerouteButton.Text = "Start";
                    CurrentInstance = null;
                });
            };
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
            mapWebView.ExecuteScriptAsync(traceroute.Properties.Resources.baiduMap);
        }
    }
}
