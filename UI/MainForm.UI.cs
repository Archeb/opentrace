using Eto.Drawing;
using Eto.Forms;
using System;
using System.Diagnostics;
using Resources = OpenTrace.Properties.Resources;
using OpenTrace.Services;
using OpenTrace.Infrastructure;
using OpenTrace.UI.Dialogs;

namespace OpenTrace.UI
{
    /// <summary>
    /// MainForm 的 UI 构建部分 - 包含菜单、控件创建和布局代码
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// 初始化所有 UI 组件
        /// </summary>
        private void InitializeComponent()
        {
            Title = Resources.APPTITLE + " v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MinimumSize = new Size(900, 600);

            // 创建菜单栏
            CreateMenuBar();

            // 创建控件
            CreateControls();

            // 创建布局
            CreateLayout();

            // 绑定窗口事件
            BindWindowEvents();
        }

        /// <summary>
        /// 创建菜单栏
        /// </summary>
        private void CreateMenuBar()
        {
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
        }

        /// <summary>
        /// 创建所有控件
        /// </summary>
        private void CreateControls()
        {
            // 主机输入框
            HostInputBox = new ComboBox { Text = "" };
            HostInputBox.KeyDown += HostInputBox_KeyDown;
            HostInputBox.KeyUp += HostInputBox_KeyUp;
            HostInputBox.TextChanged += resolveParamChanged;
            if (UserSettings.traceHistory != null || UserSettings.traceHistory != "")
            {
                foreach (string item in UserSettings.traceHistory.Split('\n'))
                {
                    if (item != "")
                    {
                        HostInputBox.Items.Add(item);
                    }
                }
            }

            // MTR 模式复选框
            MTRMode = new CheckBox { Text = Resources.MTR_MODE };
            MTRMode.CheckedChanged += MTRMode_CheckedChanged;

            // 已解析 IP 选择框
            ResolvedIPSelection = new DropDown { Visible = false };

            // 开始按钮
            startTracerouteButton = new Button { Text = Resources.START };
            startTracerouteButton.Click += StartTracerouteButton_Click;

            // 协议选择框
            CreateProtocolSelection();

            // 数据提供商选择框
            CreateDataProviderSelection();

            // DNS 解析器选择框
            CreateDnsResolverSelection();

            // 结果表格
            CreateTracerouteGridView();

            // 地图 WebView
            CreateMapWebView();
        }

        /// <summary>
        /// 创建协议选择下拉框
        /// </summary>
        private void CreateProtocolSelection()
        {
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
        }

        /// <summary>
        /// 创建数据提供商选择下拉框
        /// </summary>
        private void CreateDataProviderSelection()
        {
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
        }

        /// <summary>
        /// 创建 DNS 解析器选择下拉框
        /// </summary>
        private void CreateDnsResolverSelection()
        {
            dnsResolverSelection = new DropDown();
            dnsResolverSelection.SelectedKeyChanged += resolveParamChanged;
            LoadDNSResolvers();
            dnsResolverSelection.SelectedKey = UserSettings.selectedDnsResolver;
            dnsResolverSelection.SelectedKeyChanged += (sender, e) =>
            {
                UserSettings.selectedDnsResolver = dnsResolverSelection.SelectedKey;
                UserSettings.SaveSettings();
            };
        }

        /// <summary>
        /// 创建路由跟踪结果表格
        /// </summary>
        private void CreateTracerouteGridView()
        {
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
                clipboard.Clear();
                clipboard.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].IP;
            };
            copyGeolocationCommand.Executed += (sender, e) =>
            {
                if (UserSettings.combineGeoOrg)
                {
                    clipboard.Clear();
                    clipboard.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].GeolocationAndOrganization;
                }
                else
                {
                    clipboard.Clear();
                    clipboard.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].Geolocation;
                }
            };
            copyHostnameCommand.Executed += (sender, e) =>
            {
                clipboard.Clear();
                clipboard.Text = tracerouteResultCollection[tracerouteGridView.SelectedRow].Hostname;
            };

            AddGridColumnsTraceroute();
        }

        /// <summary>
        /// 创建地图 WebView
        /// </summary>
        private void CreateMapWebView()
        {
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
            mapWebView.DocumentLoaded += (sender, e) =>
            {
                ResetMap();
            };
        }

        /// <summary>
        /// 创建主布局
        /// </summary>
        private void CreateLayout()
        {
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
        }

        /// <summary>
        /// 绑定窗口事件
        /// </summary>
        private void BindWindowEvents()
        {
            SizeChanged += MainForm_SizeChanged;
            MouseDown += Dragging_MouseDown;
            MouseUp += Dragging_MouseUp;
            MouseMove += MainForm_MouseMove;
        }
    }
}
