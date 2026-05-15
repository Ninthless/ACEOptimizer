# ACE Optimizer

ACE Optimizer is a lightweight Windows utility that detects ACE anti-cheat processes directly and reduces their CPU impact by lowering process priority and pinning them to the last CPU core.

简体中文：ACE Optimizer 是一个 Windows 桌面工具，会直接检测 ACE 反作弊进程，并通过降低优先级、绑定最后一个 CPU 核心来减少 CPU 占用。

## Highlights

- Direct ACE process detection without a game allowlist
- Sets detected ACE processes to `Idle` priority
- Pins detected ACE processes to the last CPU core
- Runs quietly in the system tray
- Optional startup task via Windows Task Scheduler with elevation support
- English and Simplified Chinese UI resources

## Monitored Processes

| Process | Description |
| --- | --- |
| `SGuard64` | ACE user-mode process |
| `SGuardSvc64` | ACE service process |

## Platform

ACE Optimizer is built for Windows 10 and Windows 11.

Some ACE versions require administrator privileges before Windows allows priority or CPU-affinity changes. If optimization is blocked, restart ACE Optimizer as administrator when prompted.

## Download

Download the latest installer or portable archive from GitHub Releases:

[ACE Optimizer Releases](https://github.com/Ninthless/ACEOptimizer/releases)

Release assets usually include:

| File | Description |
| --- | --- |
| `ACEOptimizer_Setup_v*.exe` | Windows installer |
| `ACEOptimizer_Portable_v*.zip` | Portable single-file build |

## Usage

1. Start ACE Optimizer.
2. Launch a game or application that starts ACE.
3. ACE Optimizer detects `SGuard64` or `SGuardSvc64` automatically.
4. When access is allowed, the detected process is moved to `Idle` priority and pinned to the last CPU core.
5. Use the tray icon to reopen or exit the app.

## Tech Stack

- .NET 8
- WPF
- WPF-UI 3.0.4
- H.NotifyIcon.Wpf 2.1.4
- Inno Setup
- GitHub Actions

## Build

```powershell
dotnet restore
dotnet build
```

## Publish Locally

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

## Automated Release

The repository includes a GitHub Actions workflow that builds and publishes releases from the `main` branch.

To create a new release:

1. Update the version in `ACEOptimizer.csproj`.
2. Commit and push to `main`.
3. The workflow builds the app, creates the installer and portable archive, creates a Git tag, and publishes a GitHub Release.

Example:

```xml
<Version>1.2.5</Version>
<AssemblyVersion>1.2.5.0</AssemblyVersion>
<FileVersion>1.2.5.0</FileVersion>
```

If the matching tag already exists, the release workflow skips publishing.

## Notes

- ACE Optimizer only changes process priority and CPU affinity for the monitored ACE process names.
- It does not modify game files, anti-cheat files, drivers, or system services.
- Compatibility can change if ACE changes process names or blocks priority and affinity updates.

## Author

Created by [@Ninthless](https://github.com/Ninthless).
