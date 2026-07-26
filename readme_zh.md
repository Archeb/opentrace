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

- [x] 集成 MTR (My Traceroute) 功能

- [x] 多语言支持 (英语、中文、法语、西班牙语、日语、俄语)

- [x] 支持自定义 DNS 解析器 (DNS, DoH)

- [x] 支持通过命令行 (CLI) 启动追踪

- [x] 支持加载本地 .MMDB 数据库

更多功能正在开发中... 欢迎提交 [功能建议](https://github.com/Archeb/opentrace/issues/new/choose)！

> **提示**：您也可以从本项目的 [Actions 页面](https://github.com/Archeb/opentrace/actions) 下载最新构建的测试版（Beta）；但请注意，测试版可能包含 Bug 或不稳定。

### Microsoft Store 程序包

商店构建使用已保留的 `NYALabs.OpenTrace` 身份，生成包含 x64 与 ARM64
的 MSIX bundle，并内置固定版本、经过 SHA256 校验的 NextTrace。构建和
提交步骤见 [STORE-SUBMISSION.md](STORE-SUBMISSION.md)。

### 运行截图

![macOS 深色模式](./HomePage/img/macos_dark.jpg)
![Windows](./HomePage/img/windows.png)
![Linux](./HomePage/img/linux.png)
![macOS 设置界面](https://i.imgur.com/X0L6c6S.png)

### 致谢

OpenTrace 使用 [NextTrace](https://github.com/nxtrace/NTrace-core) 作为后端核心。

### 开源协议

OpenTrace 基于 [GPL-3.0 协议](LICENSE.txt) 开源。
