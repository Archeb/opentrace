<div align="right">[English | <a href="readme_zh.md">中文</a>]</div>

<div align="center">

<img src="https://github.com/nxtrace/NTrace-V1/raw/main/assets/logo.png" height="200px" alt="Logo"/>

<h3>
  <a href="https://opentrace.app">🌐 Official Website</a>
</h3>

</div>

## OpenTrace

OpenTrace is an open source visualized route tracing tool.

OpenTrace 是一款跨平台可视化路由追踪工具。


### Usage

- Download OpenTrace for your system from the [official website](https://opentrace.app) or [releases](https://github.com/Archeb/opentrace/releases). Linux users can also install it via [Flathub](https://flathub.org/en/apps/io.github.Archeb.opentrace) or [Arch User Repository](https://aur.archlinux.org/packages/opentrace-bin/).

<details>
<summary>Alternatively, if you compiled it yourself, then you need to:</summary>

- Download and install NextTrace: Download NextTrace for your system architecture from [here](https://github.com/nxtrace/NTrace-core/releases).

- Place NextTrace in the OpenTrace directory, or in a directory included in your system's PATH environment variable; you can also place it anywhere and manually specify the path (recommended for macOS users).
</details>

- If you are a **Windows user** and want to use TCP/UDP Traceroute, you also need to [download and install Npcap](https://npcap.com/#download).

- Unzip and run OpenTrace(.exe)

### Features

- [x] Cross-platform native GUI (Windows WPF / Linux GTK / macOS)

- [x] An interface you are familiar with, but with even more powerful functionalities

- [x] User-friendly GUI and easy-to-understand parameter descriptions

- [x] MTR (My Traceroute) functionality; uses native NextTrace MTR with v1.5.2+ and silently falls back to the compatibility implementation when unavailable

- [x] Multi-language support (English, Chinese, French, Spanish, Japanese, Russian)

- [x] Custom DNS Resolvers (DNS, DoH)

- [x] Integrated Cloudflare verification and automatic setup for NextTrace API v4 tokens (NextTrace v1.7.0+)

- [x] Use CLI to start a trace

- [x] Supports local .MMDB database

More is coming... [Feature request](https://github.com/Archeb/opentrace/issues/new/choose) is welcome!

> **Tip**: You can also download the latest beta version of the corresponding architecture from the [Actions page of this project](https://github.com/Archeb/opentrace/actions); however, it may contain bugs or vulnerabilities, or may be unstable.

### Images

![macOS Dark](./HomePage/img/macos_dark.jpg)
![Windows](./HomePage/img/windows.png)
![Linux](./HomePage/img/linux.png)
![Preferences on macOS](https://i.imgur.com/X0L6c6S.png)

### Building from source

OpenTrace uses native platform backends (WPF on Windows, GTK on Linux, and a
native macOS app bundle). Build and test on the target operating system whenever
possible. Run all commands below from the repository root.

#### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- The runtime and development tools required by the target platform (GTK 3 on
  Linux or Xcode Command Line Tools on macOS)
- For an MSIX bundle: 64-bit Windows, PowerShell, and the Windows 10/11 SDK with
  `MakeAppx.exe`

Restore dependencies once before building:

```sh
dotnet restore OpenTrace.csproj
```

#### Windows

```powershell
dotnet build OpenTrace.csproj --runtime win-x64 --configuration Release --no-self-contained -f net48
dotnet build OpenTrace.csproj --runtime win-arm64 --configuration Release --no-self-contained -f net481
```

The outputs are under `bin/Wpf/Release/net48/win-x64` and
`bin/Wpf/Release/net481/win-arm64`.

#### Linux

```sh
dotnet build OpenTrace.csproj --runtime linux-x64 --configuration Release --self-contained -f net8.0
dotnet build OpenTrace.csproj --runtime linux-arm64 --configuration Release --self-contained -f net8.0
```

The outputs are under `bin/Gtk/Release/net8.0/<runtime>`.

#### macOS

```sh
dotnet build OpenTrace.csproj --runtime osx-x64 --configuration Release --self-contained -f net8.0
dotnet build OpenTrace.csproj --runtime osx-arm64 --configuration Release --self-contained -f net8.0
```

The app bundles are under `bin/Mac64/Release/net8.0/<runtime>/OpenTrace.app`.
Signing, notarization, and DMG creation require Apple developer credentials;
the release workflow in `.github/workflows/build-release-macos.yml` documents
that process.

These platform commands compile OpenTrace itself. When preparing a
redistributable archive, also download the matching NextTrace executable and
place it beside OpenTrace (`OpenTrace.app/Contents/MacOS` on macOS), or let the
user select an external NextTrace executable in the application settings. The
release workflows under `.github/workflows` are the reference for assembling
the published archives.

#### Microsoft Store MSIX bundle (Windows)

The Store package script builds x64 and ARM64, prepares package artwork,
downloads the pinned and SHA256-verified NextTrace payloads and required
WinDivert files, and creates one combined bundle. Use a four-part package
version; normally append `.0` to the application version:

```powershell
.\scripts\Build-StorePackage.ps1 -Version 1.5.2.0
```

Upload the resulting unsigned bundle to Partner Center:

```text
artifacts\store\OpenTrace_1.5.2.0_x64_arm64.msixbundle
```

Do not upload the architecture-specific packages under
`artifacts\store\packages`. A local certificate is not needed for Partner
Center submission. To create a signed bundle for local sideload testing only,
run:

```powershell
.\scripts\Build-SignedStorePackage.ps1 -Version 1.5.2.0
```

### Credit

OpenTrace uses [NextTrace](https://github.com/nxtrace/NTrace-core) as the backend.

### License

OpenTrace is released under the [GPL-3.0 license](LICENSE.txt).
