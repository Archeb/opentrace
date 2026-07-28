<div align="right">[<a href="readme.md">English</a> | 中文]</div>
<div align="center">

<img src="https://github.com/nxtrace/NTrace-V1/raw/main/assets/logo.png" height="200px" alt="Logo"/>

<h3>
  <a href="https://opentrace.app">🌐 官方网站</a>
</h3>

</div>

## OpenTrace

OpenTrace 是一款开源的跨平台可视化路由追踪工具。

### 使用方法

- 请从[官网](https://opentrace.app)或 [Releases 页面](https://github.com/Archeb/opentrace/releases) 下载适用于您系统的 OpenTrace。
- Linux 用户也可以通过 [Flathub](https://flathub.org/en/apps/io.github.Archeb.opentrace) 或 [Arch User Repository (AUR)](https://aur.archlinux.org/packages/opentrace-bin/) 进行安装。

<details>
<summary>如果您选择自行编译或使用非打包版本，请注意：</summary>

- **下载并安装 NextTrace**：请从[此处](https://github.com/nxtrace/NTrace-core/releases)下载对应系统架构的 NextTrace 核心文件。

- **放置核心文件**：将 NextTrace 可执行文件放入 OpenTrace 目录中，或放入系统 PATH 环境变量包含的目录中；您也可以将其放在任意位置，并在 OpenTrace 的设置中手动指定路径（macOS 用户推荐使用手动指定）。
</details>

- 如果您是 **Windows 用户** 并且想要使用 TCP/UDP 协议进行追踪，您还需要 [下载并安装 Npcap](https://npcap.com/#download)。

- 解压并运行 OpenTrace 可执行文件。

### 功能特性

- [x] 跨平台原生 GUI (Windows WPF / Linux GTK / macOS)

- [x] 您熟悉的界面逻辑，但拥有更强大的功能体验

- [x] 用户友好的图形界面及清晰易懂的参数说明

- [x] 集成 MTR (My Traceroute) 功能；NextTrace v1.5.2 及以上使用原生 MTR，不可用时静默回退兼容实现

- [x] 多语言支持 (英语、中文、法语、西班牙语、日语、俄语)

- [x] 支持自定义 DNS 解析器 (DNS, DoH)

- [x] 集成 Cloudflare 人机验证并自动设置 NextTrace API v4 Token（需 NextTrace v1.7.0 及以上）

- [x] 支持通过命令行 (CLI) 启动追踪

- [x] 支持加载本地 .MMDB 数据库

更多功能正在开发中... 欢迎提交 [功能建议](https://github.com/Archeb/opentrace/issues/new/choose)！

> **提示**：您也可以从本项目的 [Actions 页面](https://github.com/Archeb/opentrace/actions) 下载最新构建的测试版（Beta）；但请注意，测试版可能包含 Bug 或不稳定。

### 运行截图

![macOS 深色模式](./HomePage/img/macos_dark.jpg)
![Windows](./HomePage/img/windows.png)
![Linux](./HomePage/img/linux.png)
![macOS 设置界面](https://i.imgur.com/X0L6c6S.png)

### 从源码构建

OpenTrace 在不同系统上使用原生平台后端（Windows WPF、Linux GTK 和
macOS 原生应用包），因此建议尽量在目标操作系统上构建并测试。以下命令均
应在仓库根目录运行。

#### 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 目标平台所需的运行库和开发工具（Linux 上的 GTK 3，或 macOS 上的
  Xcode Command Line Tools）
- 构建 MSIX bundle 还需要：64 位 Windows、PowerShell，以及包含
  `MakeAppx.exe` 的 Windows 10/11 SDK

首次构建前先还原依赖：

```sh
dotnet restore OpenTrace.csproj
```

#### Windows

```powershell
dotnet build OpenTrace.csproj --runtime win-x64 --configuration Release --no-self-contained -f net48
dotnet build OpenTrace.csproj --runtime win-arm64 --configuration Release --no-self-contained -f net481
```

输出目录分别为 `bin/Wpf/Release/net48/win-x64` 和
`bin/Wpf/Release/net481/win-arm64`。

#### Linux

```sh
dotnet build OpenTrace.csproj --runtime linux-x64 --configuration Release --self-contained -f net8.0
dotnet build OpenTrace.csproj --runtime linux-arm64 --configuration Release --self-contained -f net8.0
```

输出目录为 `bin/Gtk/Release/net8.0/<runtime>`。

#### macOS

```sh
dotnet build OpenTrace.csproj --runtime osx-x64 --configuration Release --self-contained -f net8.0
dotnet build OpenTrace.csproj --runtime osx-arm64 --configuration Release --self-contained -f net8.0
```

应用包位于 `bin/Mac64/Release/net8.0/<runtime>/OpenTrace.app`。签名、公证和
DMG 制作需要 Apple 开发者凭据，完整发布流程可参考
`.github/workflows/build-release-macos.yml`。

上述平台命令只编译 OpenTrace 本身。制作可分发压缩包时，还需要下载对应
平台和架构的 NextTrace，将其放在 OpenTrace 可执行文件旁边（macOS 为
`OpenTrace.app/Contents/MacOS`）；也可以让用户在应用设置中选择外部
NextTrace 可执行文件。`.github/workflows` 下的发布工作流是组装正式发布包
的参考实现。

#### Microsoft Store MSIX bundle（Windows）

商店打包脚本会自动构建 x64 和 ARM64、生成包图标、下载固定版本且经过
SHA256 校验的 NextTrace 和所需 WinDivert 文件，最后生成一个合并 bundle。
包版本必须是四段数字，通常在应用版本后添加 `.0`：

```powershell
.\scripts\Build-StorePackage.ps1 -Version 1.5.2.0
```

将下面生成的未签名 bundle 上传到 Partner Center：

```text
artifacts\store\OpenTrace_1.5.2.0_x64_arm64.msixbundle
```

不要上传 `artifacts\store\packages` 下的单架构包。提交 Partner Center 不需要
本地测试证书。如果只想生成可在本机旁加载测试的已签名 bundle，可运行：

```powershell
.\scripts\Build-SignedStorePackage.ps1 -Version 1.5.2.0
```

### 致谢

OpenTrace 使用 [NextTrace](https://github.com/nxtrace/NTrace-core) 作为后端核心。

### 开源协议

OpenTrace 基于 [GPL-3.0 协议](LICENSE.txt) 开源。
