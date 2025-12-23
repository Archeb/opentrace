using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using OpenTrace.Services;
using OpenTrace.Models;
using OpenTrace.Infrastructure;
using OpenTrace.UI.Forms;
using System.Net;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;

namespace OpenTrace.UI
{
    public partial class MainForm : Form
    {
        private ObservableCollection<TracerouteHop> tracerouteResultCollection = new ObservableCollection<TracerouteHop>();
        private static NextTraceWrapper CurrentInstance { get; set; }
        private ComboBox HostInputBox;
        private GridView tracerouteGridView;
        private CheckBox MTRMode;
        private WebView mapWebView;
        private DropDown ResolvedIPSelection;
        private DropDown dataProviderSelection;
        private DropDown protocolSelection;
        private DropDown dnsResolverSelection;
        private Button startTracerouteButton;
        private bool gridResizing = false;
        private bool appForceExiting = false;
        private bool enterPressed = false;

        private ExceptionalOutputForm exceptionalOutputForm = new ExceptionalOutputForm();
        private Clipboard clipboard = new Clipboard();
        private DnsResolverService dnsResolverService = new DnsResolverService();
        private UpdateService updateService = new UpdateService();
        private PlatformService platformService = new PlatformService();

        public MainForm()
        {
            // 初始化 UI 组件
            InitializeComponent();

            // 平台特定检查
            platformChecks();

            // 异步检查更新
            CheckUpdateAsync();

            // 自动聚焦输入框
            HostInputBox.Focus();
        }

        private async void CheckUpdateAsync()
        {
            string currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string latestVersion = await updateService.CheckForUpdateAsync(currentVersion);
            if (latestVersion != null)
            {
                App.app.Invoke(() =>
                {
                    App.app.MainForm.Title += " " + string.Format(Resources.UPDATE_AVAILABLE, latestVersion);
                });
            }
        }

        private void LoadDNSResolvers()
        {
            dnsResolverSelection.Items.Clear();
            var resolvers = dnsResolverService.GetResolverList();
            foreach (var resolver in resolvers)
            {
                dnsResolverSelection.Items.Add(new ListItem { Text = resolver.Text, Key = resolver.Key });
            }
            dnsResolverSelection.SelectedIndex = 0;
        }

        // 初始化期间进行平台特定检查
        private void platformChecks()
        {
            platformService.RunPlatformChecks();
        }

        private void resolveParamChanged(object sender, EventArgs e)
        {
            // 如果文本框被修改，则隐藏 DNS 解析选择框
            if (ResolvedIPSelection.Visible)
            {
                ResolvedIPSelection.Items.Clear();
                ResolvedIPSelection.Visible = false;
            }
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

        private void HostInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && !enterPressed)
            {
                enterPressed = true;
            }
        }
        private void HostInputBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && enterPressed)
            {
                enterPressed = false;
                StartTracerouteButton_Click(sender, e);
            } else if (e.Key == Keys.Enter && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // 似乎是上游兼容性问题
                StartTracerouteButton_Click(sender, e);
            }
        }

        private void TracerouteGridView_SelectedRowsChanged(object sender, EventArgs e)
        {
            FocusMapPoint(tracerouteGridView.SelectedRow);
        }

        private void StartTracerouteButton_Click(object sender, EventArgs e)
        {
            
            if(protocolSelection.SelectedValue.ToString() != "ICMP" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 检测是否有管理员权限
                // 检测并提示用户安装Npcap，默认WinDivert有安装
                MessageBox.Show(Resources.WINDOWS_TCP_UDP_UNSUPPORTED);
                return;
            }
            if (CurrentInstance != null)
            {
                StopTraceroute();
                return;
            }
            try
            {
                CurrentInstance = new NextTraceWrapper();
            }
            catch (FileNotFoundException)
            {
                // 未能在默认搜寻目录中找到NextTrace，询问是否下载 NextTrace
                DialogResult dr = MessageBox.Show(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Resources.MISSING_COMP_TEXT_MACOS : Resources.MISSING_COMP_TEXT ,
                 Resources.MISSING_COMP, MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://github.com/nxtrace/Ntrace-V1/releases/") { UseShellExecute = true });
                }
                CurrentInstance = null;
                return;
            }
            catch(IOException exception)
            {
                // 未能在指定的位置找到 NextTrace
                MessageBox.Show(string.Format(Resources.MISSING_SPECIFIED_COMP, exception.Message), Resources.MISSING_COMP);

                CurrentInstance = null;
                return;
            }
            if(HostInputBox.Text == "")
            {
                MessageBox.Show(Resources.EMPTY_HOSTNAME_MSGBOX);
                CurrentInstance = null;
                return;
            }

            tracerouteResultCollection.Clear(); // 清空原有GridView
            ResetMap(); // 重置地图
            Title = Resources.APPTITLE;
            // 处理文本框输入
            string readyToUseIP;
            if (ResolvedIPSelection.Visible == true && ResolvedIPSelection.SelectedIndex != 0)
            {
                readyToUseIP = ResolvedIPSelection.SelectedKey;
                Title = Resources.APPTITLE + ": " + HostInputBox.Text + " (" + readyToUseIP + ")";
            }
            else if(ResolvedIPSelection.Visible == true && ResolvedIPSelection.SelectedIndex == 0)
            {
                MessageBox.Show(Resources.SELECT_IP_MSGBOX);
                CurrentInstance = null;
                return;
            }
            else
            {
                ResolvedIPSelection.Visible = false; // 隐藏 IP 选择框
                IPAddress userInputAddress;
                // 去除输入框两侧的空格
                HostInputBox.Text = HostInputBox.Text.Trim();

                Uri uri;
                if (Uri.TryCreate(HostInputBox.Text, UriKind.Absolute, out uri) && uri.Host != "")
                {
                    // 是合法的 URL
                    HostInputBox.Text = uri.Host;
                }

                // 如果有冒号而且有点(IPv4)，去除冒号后面的内容
                if (HostInputBox.Text.IndexOf(":") != -1 && HostInputBox.Text.IndexOf(".") != -1)
                {
                    HostInputBox.Text = HostInputBox.Text.Split(':')[0];
                }
                if (IPAddress.TryParse(HostInputBox.Text, out userInputAddress))
                {
                    // 是合法的 IPv4 / IPv6，把程序处理后的IP放回文本框
                    HostInputBox.Text = userInputAddress.ToString();
                    readyToUseIP = userInputAddress.ToString();
                    Title = Resources.APPTITLE + ": " + readyToUseIP;
                }
                else { 
                    try
                    {
                        // 需要域名解析
                        Title = Resources.APPTITLE + ": " + HostInputBox.Text;
                        IPAddress[] resolvedAddresses = ResolveHost(HostInputBox.Text);
                        if (resolvedAddresses.Length > 1)
                        {
                            if (UserSettings.autoIPSelection == "manual")
                            {
                                // 手动选择 IP
                                ResolvedIPSelection.Items.Clear();
                                ResolvedIPSelection.Items.Add(Resources.SELECT_IP_DROPDOWN);
                                foreach (IPAddress resolvedAddress in resolvedAddresses)
                                {
                                    ResolvedIPSelection.Items.Add(resolvedAddress.ToString());
                                }
                                ResolvedIPSelection.SelectedIndex = 0;
                                ResolvedIPSelection.Visible = true;
                                CurrentInstance = null;
                                return;
                            }
                            else
                            {
                                IPAddress selectedIP = null;

                                if (UserSettings.autoIPSelection == "first_ipv4")
                                {
                                    selectedIP = resolvedAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                                }
                                else if (UserSettings.autoIPSelection == "first_ipv6")
                                {
                                    selectedIP = resolvedAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);
                                }

                                // 回退到第一个可用的 IP
                                if (selectedIP == null)
                                {
                                    selectedIP = resolvedAddresses[0];
                                }

                                readyToUseIP = selectedIP.ToString();
                            }
                        }
                        else
                        {
                            readyToUseIP = resolvedAddresses[0].ToString();
                        }
                        Title = Resources.APPTITLE + ": " + HostInputBox.Text + " (" + readyToUseIP + ")";
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                        MessageBox.Show(string.Format(Resources.NAME_NOT_RESOLVED, HostInputBox.Text), MessageBoxType.Warning);
                        Title = Resources.APPTITLE;
                        CurrentInstance = null;
                        return;
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(exception.Message, MessageBoxType.Error);
                        Title = Resources.APPTITLE;
                        CurrentInstance = null;
                        return;
                    }
                }
            }

            string newText = HostInputBox.Text;
            // 清理重复记录
            IList<IListItem> clone = HostInputBox.Items.ToList();
            foreach (var toRemove in clone.Where(s => s.Text == newText))
            {
                HostInputBox.Items.Remove(toRemove); // 不知道为什么清理掉 ComboBox 的 Item 会把同名文本框的内容一起清掉
            }
            HostInputBox.Text = newText; // 所以得在这里重新放回去
            HostInputBox.Items.Insert(0, new ListItem { Text = newText });
            while (HostInputBox.Items.Count > 20) // 清理20条以上记录
            {
                HostInputBox.Items.RemoveAt(HostInputBox.Items.Count - 1);
            }
            UserSettings.traceHistory = String.Join("\n", HostInputBox.Items.Select(item => item.Text));
            UserSettings.SaveSettings();

            startTracerouteButton.Text = Resources.STOP;

            // 处理NextTrace实例发回的结果
            CurrentInstance.Output.CollectionChanged += Instance_OutputCollectionChanged;
            CurrentInstance.ExceptionalOutput += Instance_ExceptionalOutput;
            CurrentInstance.AppQuit += Instance_AppQuit;
            CurrentInstance.Run(readyToUseIP, (bool)MTRMode.Checked, dataProviderSelection.SelectedKey, protocolSelection.SelectedKey);
            
        }

        private IPAddress[] ResolveHost(string host)
        {
            string resolver = dnsResolverSelection.SelectedKey;
            return dnsResolverService.ResolveHost(host, resolver);
        }

        private void Instance_AppQuit(object sender, AppQuitEventArgs e)
        {
            Application.Instance.InvokeAsync(() =>
            {
                CurrentInstance = null;
                startTracerouteButton.Text = Resources.START;
                if (appForceExiting != true && (e.ExitCode != 0))
                {
                    // 主动结束，退出代码不为 0 则证明有异常
                    MessageBox.Show(Resources.EXCEPTIONAL_EXIT_MSG + e.ExitCode, MessageBoxType.Warning);
                }
                // 强制结束一般退出代码不为 0，不提示异常。
                appForceExiting = false;
            });
        }

        private void Instance_ExceptionalOutput(object sender, ExceptionalOutputEventArgs e)
        {
            Application.Instance.InvokeAsync(() =>
            {
                exceptionalOutputForm.Show();
                if (!exceptionalOutputForm.Visible)
                {
                    exceptionalOutputForm.Visible = true;
                }
                exceptionalOutputForm.AppendOutput(e.Output);
            });
        }

        private void Instance_OutputCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Application.Instance.InvokeAsync(() =>
                {
                    try
                    {
                        TracerouteResult result = (TracerouteResult)e.NewItems[0];
                        result = IPDBLoader.Rewrite(result);
                        int HopNo = int.Parse(result.No);
                        if (HopNo > tracerouteResultCollection.Count)
                        {
                            // 正常添加新的跳
                            tracerouteResultCollection.Add(new TracerouteHop(result));
                            UpdateMap(result);
                            tracerouteGridView.ScrollToRow(tracerouteResultCollection.Count - 1);
                        }
                        else
                        {
                            // 修改现有的跳
                            tracerouteResultCollection[HopNo - 1].HopData.Add(result);

                            // 仅当存在经纬度数据时更新地图
                            if (result.Latitude != "" && result.Longitude != "")
                            {
                                UpdateMap(result, HopNo - 1);
                            }
                            
                            tracerouteGridView.ReloadData(HopNo - 1);
                        }
                    } catch (Exception exception)
                    {
                        MessageBox.Show($"Message: ${exception.Message} \nSource: ${exception.Source} \nStackTrace: ${exception.StackTrace}", "Exception Occurred");
                    }
                });
            }
        }

        private void StopTraceroute()
        {
            if (CurrentInstance != null && !appForceExiting) {
                appForceExiting = true;
                CurrentInstance.Kill();
            }
        }

        /*
         * 处理拖拽调整 GridView 大小
         */
        private void Dragging_MouseUp(object sender, MouseEventArgs e)
        {
            gridResizing = false;
            mapWebView.Enabled = true;
            // save grid size percentage
            UserSettings.SaveSettings();
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
                    UserSettings.gridSizePercentage = (double)tracerouteGridView.Height / (Height - 75); // 保存比例
                }
            }
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            int gridHeight;
            int totalHeight = this.Height - 75; // 减去边距和上面的文本框的75px
            gridHeight = (int)(totalHeight * UserSettings.gridSizePercentage);
            tracerouteGridView.Height = gridHeight; // 按比例还原高度
        }
        private void UpdateMap(TracerouteResult result)
        {
            try
            {
                // 把 Result 转换为 JSON
                string resultJson = JsonConvert.SerializeObject(result);
                // 通过 ExecuteScript 把结果传进去
                mapWebView.ExecuteScriptAsync(@"window.opentrace.updateHop(`" + resultJson + "`);");
            }
            catch (Exception e)
            {
                MessageBox.Show($"Message: ${e.Message} \nSource: ${e.Source} \nStackTrace: ${e.StackTrace}", "Exception Occurred");
            }
        }
        private void UpdateMap(TracerouteResult result, int hopNo)
        {
            try
            {
                // 把 Result 转换为 JSON
                string resultJson = JsonConvert.SerializeObject(result);
                // 通过 ExecuteScript 把结果传进去
                mapWebView.ExecuteScriptAsync(@"window.opentrace.updateHop(`" + resultJson + "`" + "," + hopNo.ToString() +");");
            }
            catch (Exception e)
            {
                MessageBox.Show($"Message: ${e.Message} \nSource: ${e.Source} \nStackTrace: ${e.StackTrace}", "Exception Occurred");
            }
        }
        private void FocusMapPoint(int hopNo)
        {
            try
            {
                mapWebView.ExecuteScriptAsync(@"window.opentrace.focusHop(" + hopNo + ");");
            }
            catch (Exception e)
            {
                MessageBox.Show($"Message: ${e.Message} \nSource: ${e.Source} \nStackTrace: ${e.StackTrace}", "Exception Occurred");
            }
        }
        private void ResetMap()
        {
            try
            {
                // 重置或者初始化地图
                switch (mapWebView.Url.Host)
                {
                    case "geo-devrel-javascript-samples.web.app":
                        mapWebView.ExecuteScriptAsync(OpenTrace.Properties.Resources.googleMap);
                        break;
                    case "lbs.baidu.com":
                        mapWebView.ExecuteScriptAsync(OpenTrace.Properties.Resources.baiduMap);
                        break;
                }
                mapWebView.ExecuteScriptAsync("window.opentrace.reset(" + UserSettings.hideMapPopup.ToString().ToLower() + ")");
            } catch (Exception e)
            {
                MessageBox.Show($"Message: ${e.Message} \nSource: ${e.Source} \nStackTrace: ${e.StackTrace}", "Exception Occurred");
            }
        }
        private void AddGridColumnsTraceroute()
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
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Time) },
                HeaderText = Resources.TIME_MS
            });
            // 合并位置和运营商
            if (UserSettings.combineGeoOrg == true)
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
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Hostname) },
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
            if (UserSettings.combineGeoOrg == true)
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
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.AS) },
                HeaderText = "AS"
            });
            tracerouteGridView.Columns.Add(new GridColumn
            {
                DataCell = new TextBoxCell { Binding = Binding.Property<TracerouteHop, string>(r => r.Hostname) },
                HeaderText = Resources.HOSTNAME
            });
        }
    }
}
