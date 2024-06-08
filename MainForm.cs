using Eto.Drawing;
using Eto.Forms;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.IO;
using Resources = OpenTrace.Properties.Resources;
using NextTrace;
using System.Net;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using System.Threading.Tasks;
using System.Net.Sockets;
using Ae.Dns.Protocol.Records;

namespace OpenTrace
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

        public MainForm()
        {
            Title = Resources.APPTITLE + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MinimumSize = new Size(900, 600);

            // 创建菜单项
            var newWindowCommand = new Command { MenuText = Resources.NEW, ToolBarText = Resources.NEW_WINDOW_TEXT, Shortcut = Application.Instance.CommonModifier | Keys.N };
            newWindowCommand.Executed += (sender, e) =>
            {
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            };

            var quitCommand = new Command { MenuText = Resources.QUIT, Shortcut = Application.Instance.CommonModifier | Keys.Q };
            quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            var OTHomePageCommand = new Command { MenuText = "OpenTrace " + Resources.HOMEPAGE };
            OTHomePageCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace") { UseShellExecute = true });

            var DownloadLatestCommand = new Command { MenuText = Resources.DOWNLOAD_LATEST };
            DownloadLatestCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://github.com/Archeb/opentrace/releases") { UseShellExecute = true });

            var NTHomePageCommand = new Command { MenuText = "NextTrace " + Resources.HOMEPAGE };
            NTHomePageCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://www.nxtrace.org/") { UseShellExecute = true });

            var NTWikiCommand = new Command { MenuText = "NextTrace Wiki" };
            NTWikiCommand.Executed += (sender, e) => Process.Start(new ProcessStartInfo("https://github.com/nxtrace/NTrace-core/wiki") { UseShellExecute = true });

            var preferenceCommand = new Command { MenuText = Resources.PREFERENCES, Shortcut = Application.Instance.CommonModifier | Keys.Comma };
            preferenceCommand.Executed += (sender, e) =>
            {
                new PreferencesDialog().ShowModal(this);
                // 关闭设置后刷新 DNS 服务器列表
                LoadDNSResolvers();
                // 刷新grid高度大小
                MainForm_SizeChanged(sender, e);
            };

            // 创建菜单栏
            Menu = new MenuBar
            {
                Items =
                {
                    new SubMenuItem { Text = Resources.FILE, Items = {
                            newWindowCommand,
                            preferenceCommand,
                            quitCommand
                        } },
                     new SubMenuItem { Text = Resources.HELP , Items = {
                             OTHomePageCommand,
                             DownloadLatestCommand,
                             NTHomePageCommand,
                             NTWikiCommand
                         } }
                }
            };

            // 创建控件
            HostInputBox = new ComboBox { Text = "" };
            HostInputBox.KeyDown += HostInputBox_KeyDown;
            HostInputBox.KeyUp += HostInputBox_KeyUp;
            HostInputBox.TextChanged += resolveParamChanged;
            if(UserSettings.traceHistory != null || UserSettings.traceHistory!= "")
            {
                foreach (string item in UserSettings.traceHistory.Split('\n'))
                {
                    if(item != "")
                    {
                        HostInputBox.Items.Add(item);
                    }
                }
            }

            MTRMode = new CheckBox { Text = Resources.MTR_MODE };
            MTRMode.CheckedChanged += MTRMode_CheckedChanged;

            ResolvedIPSelection = new DropDown { Visible = false };

            startTracerouteButton = new Button { Text = Resources.START };
            startTracerouteButton.Click += StartTracerouteButton_Click;

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
            protocolSelection.SelectedKey = UserSettings.selectedProtocol;
            protocolSelection.SelectedKeyChanged += (sender, e) =>
            {
                UserSettings.selectedProtocol = protocolSelection.SelectedKey;
                UserSettings.SaveSettings();
            };

            dataProviderSelection = new DropDown
            {
                Items = {
                    new ListItem{Text = "LeoMoeAPI", Key= ""},
                    new ListItem{Text = "IPInfo", Key = "--data-provider IPInfo" },
                    new ListItem{Text = "IP.SB ", Key = "--data-provider IP.SB" },
                    new ListItem{Text = "IP-API.com", Key = "--data-provider IPAPI.com" },
                    new ListItem{Text = Resources.DISABLE_IPGEO, Key = "--data-provider disable-geoip"}
                },
                SelectedIndex = 0,
                ToolTip = Resources.IP_GEO_DATA_PROVIDER
            };

            if (UserSettings.ChunZhenEndpoint != "") dataProviderSelection.Items.Add(new ListItem { Text = "CHUNZHEN", Key = "--data-provider chunzhen" });
            if (UserSettings.IPInsightToken != "") dataProviderSelection.Items.Add(new ListItem { Text = "IPInsight", Key = "--data-provider IPInsight" });
            if (UserSettings.enable_ip2region == true) dataProviderSelection.Items.Add(new ListItem { Text = "Ip2region", Key = "--data-provider Ip2region" });
            if (UserSettings.enable_ipinfolocal == true) dataProviderSelection.Items.Add(new ListItem { Text = "IPInfoLocal", Key = "--data-provider IPInfoLocal" });

            dataProviderSelection.SelectedKey = UserSettings.selectedDataProvider;
            dataProviderSelection.SelectedKeyChanged += (sender, e) =>
            {
                UserSettings.selectedDataProvider = dataProviderSelection.SelectedKey;
                UserSettings.SaveSettings();
            };

            if (UserSettings.localDBPath != "") IPDBLoader.Load();

            dnsResolverSelection = new DropDown();
            dnsResolverSelection.SelectedKeyChanged += resolveParamChanged;
            LoadDNSResolvers();
            dnsResolverSelection.SelectedKey = UserSettings.selectedDnsResolver;
            dnsResolverSelection.SelectedKeyChanged += (sender, e) =>
            {
                UserSettings.selectedDnsResolver = dnsResolverSelection.SelectedKey;
                UserSettings.SaveSettings();
            };

            tracerouteGridView = new GridView { DataStore = tracerouteResultCollection };
            tracerouteGridView.MouseUp += Dragging_MouseUp;
            tracerouteGridView.SelectedRowsChanged += TracerouteGridView_SelectedRowsChanged;
            var copyIPCommand = new Command { MenuText = Resources.COPY + "IP" };
            var copyGeolocationCommand = new Command { MenuText = Resources.COPY + Resources.GEOLOCATION };
            var copyHostnameCommand = new Command { MenuText = Resources.COPY + Resources.HOSTNAME };
            tracerouteGridView.ContextMenu = new ContextMenu
            {
                Items = {
                    copyIPCommand,
                    copyGeolocationCommand,
                    copyHostnameCommand
                }
            };
            copyIPCommand.Executed += (sender, e) =>
            {
                Clipboard.Instance.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].IP;
            };
            copyGeolocationCommand.Executed += (sender, e) =>
            {
                if (UserSettings.combineGeoOrg)
                {
                    Clipboard.Instance.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].GeolocationAndOrganization;
                }
                else
                {
                    Clipboard.Instance.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].Geolocation;
                }
            };
            copyHostnameCommand.Executed += (sender, e) =>
            {
                Clipboard.Instance.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].Hostname;
            };

            AddGridColumnsTraceroute();

            mapWebView = new WebView();
            switch (UserSettings.mapProvider)
            {
                case "baidu":
                    mapWebView.Url = new Uri("https://lbs.baidu.com/jsdemo/demo/webgl0_0.htm");
                    break;
                case "google":
                    mapWebView.Url = new Uri("https://geo-devrel-javascript-samples.web.app/samples/map-simple/app/dist/");
                    break;
            }
            mapWebView.DocumentLoaded += (sender6, e6) => {
                ResetMap();
            };

            platformChecks();
            
            // 绑定窗口事件
            SizeChanged += MainForm_SizeChanged;
            MouseDown += Dragging_MouseDown;
            MouseUp += Dragging_MouseUp;
            MouseMove += MainForm_MouseMove;

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
                                        ResolvedIPSelection,
                                        MTRMode,
                                        protocolSelection,
                                        dnsResolverSelection,
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

            // check update async
            Task.Run(() => CheckUpdateAsync());

            HostInputBox.Focus(); // 自动聚焦输入框
        }

        private void CheckUpdateAsync()
        {
            if(!UserSettings.checkUpdateOnStartup) return;
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri("https://api.github.com/repos/Archeb/opentrace/releases/latest")
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenTrace");
            var response = httpClient.GetAsync("").Result;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    var definition = new { tag_name = "" };
                    var json = JsonConvert.DeserializeAnonymousType(result, definition);
                    string latestVersion = json.tag_name;
                    string currentVersion = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    if (latestVersion != currentVersion)
                    {
                        App.app.Invoke(() =>
                        {
                            App.app.MainForm.Title += " " + string.Format(Resources.UPDATE_AVAILABLE, latestVersion);
                        });
                    }
                }
                catch { }
            }
        }

        private void LoadDNSResolvers()
        {
            dnsResolverSelection.Items.Clear();
            dnsResolverSelection.Items.Add(new ListItem { Text = Resources.SYSTEM_DNS_RESOLVER, Key = "system" });
            if (UserSettings.customDNSResolvers != null || UserSettings.customDNSResolvers != "")
            {
                string resolvers = UserSettings.customDNSResolvers.Replace("\r", "");
                foreach (string item in resolvers.Split('\n'))
                {
                    string[] resolver = item.Split('#');
                    IPAddress resolverIP;
                    if (resolver[0] != "" && (resolver[0].IndexOf("https://") == 0 || IPAddress.TryParse(resolver[0], out resolverIP)))
                    {
                        dnsResolverSelection.Items.Add(new ListItem { Text = resolver.Length == 2 ? resolver[1] : resolver[0], Key = resolver[0] });
                    }
                }
            }
            dnsResolverSelection.SelectedIndex = 0;
        }

        // 初始化期间进行平台特定检查
        private void platformChecks()
        {
            
            // macOS 被隔离，请求释放
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && AppDomain.CurrentDomain.BaseDirectory.StartsWith("/private/var/folders"))
            {
                App.app.Invoke(() => {
                    MessageBox.Show(Resources.MACOS_QUARANTINE);
                });
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && UserSettings.hideAddICMPFirewallRule != true) tryAddICMPFirewallRule();
        }
        
        private void tryAddICMPFirewallRule()
        {
            // 提示 Windows 用户添加防火墙规则放行 ICMP 
            if (MessageBox.Show(Resources.ASK_ADD_ICMP_FIREWALL_RULE, MessageBoxButtons.YesNo, MessageBoxType.Question) == DialogResult.Yes)
            {
                // 以管理员权限运行命令
                var allowIcmp = new Process();
                allowIcmp.StartInfo.FileName = "cmd.exe";
                allowIcmp.StartInfo.UseShellExecute = true;
                allowIcmp.StartInfo.Verb = "runas";
                allowIcmp.StartInfo.Arguments = "/c \"netsh advfirewall firewall add rule name=\"\"\"All ICMP v4 (NextTrace)\"\"\" dir=in action=allow protocol=icmpv4:any,any && netsh advfirewall firewall add rule name=\"\"\"All ICMP v6 (NextTrace)\"\"\" dir=in action=allow protocol=icmpv6:any,any\"";
                try
                {
                    allowIcmp.Start();
                    UserSettings.hideAddICMPFirewallRule = true;
                    UserSettings.SaveSettings();
                }
                catch (Win32Exception)
                {
                    MessageBox.Show(Resources.FAILED_TO_ADD_RULES, MessageBoxType.Error);
                }
            }
            else
            {
                UserSettings.hideAddICMPFirewallRule = true;
                UserSettings.SaveSettings();
            }
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
                            readyToUseIP = resolvedAddresses[0].ToString();
                            Title = Resources.APPTITLE + ": " + HostInputBox.Text + " (" + readyToUseIP + ")";
                        }
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
            if(resolver == "system")
            {
                // 使用系统解析
                return Dns.GetHostAddresses(host);
            }else if (resolver.IndexOf("https://") == 0)
            {
                // 使用DoH
                var httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri(resolver)
                };
                IDnsClient dnsClient = new DnsHttpClient(httpClient);
                DnsMessage result = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host))).Result;
                if(result.Answers.Count == 0)
                {
                    throw new SocketException();
                }
                else
                {
                    List<IPAddress> addressList = new List<IPAddress>();
                    foreach (DnsResourceRecord answer in result.Answers)
                    {
                        if (answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.A || answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.AAAA)
                        {
                            addressList.Add(((DnsIpAddressResource)answer.Resource).IPAddress);
                        }
                    }
                    return addressList.ToArray();
                }
            }
            else
            {
                // 使用传统 DNS
                IDnsClient dnsClient = new DnsUdpClient(IPAddress.Parse(resolver));
                DnsMessage result = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host))).Result;

                if (result.Answers.Count == 0)
                {
                    throw new SocketException();
                }
                else
                {
                    List<IPAddress> addressList = new List<IPAddress>();
                    foreach (DnsResourceRecord answer in result.Answers)
                    {
                        if (answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.A || answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.AAAA)
                        {
                            addressList.Add(((DnsIpAddressResource)answer.Resource).IPAddress);
                        }
                    }
                    return addressList.ToArray();
                }
            }
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
                mapWebView.ExecuteScriptAsync(@"window.opentrace.addHop(`" + resultJson + "`);");
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
