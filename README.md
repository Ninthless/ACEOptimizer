# ACE Optimizer

ACE 反作弊 CPU 优化工具，用于降低 ACE 反作弊引擎对 CPU 的占用。

## 功能

- 自动检测支持的游戏进程
- 将 ACE 进程优先级降为 Idle
- 将 ACE 进程绑定到最后一个 CPU 核心
- 支持最小化到系统托盘
- 支持开机自启（通过任务计划程序，支持 UAC 提权）

## 支持的游戏

| 游戏 | 进程名 |
|------|--------|
| 三角洲行动 | `DeltaForceClient-Win64-Shipping` |
| 无畏契约 (VALORANT) | `VALORANT-Win64-Shipping` |

## 技术栈

- .NET 8 + WPF
- WPF-UI 3.0.4 (Fluent Design)
- H.NotifyIcon.Wpf 2.1.4
- Inno Setup (安装程序)

## 构建

```bash
dotnet build
```

## 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 自动发布流程

项目使用 GitHub Actions 自动构建发布。

**推送代码到 main 分支即可自动发布**（需要更新 `.csproj` 中的版本号）：

1. 修改 `ACEOptimizer.csproj` 中的 `<Version>` 版本号
2. 提交并推送到 main 分支
3. GitHub Actions 自动构建并发布

```xml
<!-- ACEOptimizer.csproj -->
<Version>1.2.1</Version>
```

> 注意：如果版本号对应的 tag 已存在，则不会重复发布。

工作流会自动：
1. 读取 `.csproj` 中的版本号
2. 构建 .NET 项目
3. 生成安装程序（Inno Setup）
4. 创建便携版 ZIP
5. 创建 Git tag
6. 发布 GitHub Release

## 作者

[@Ninthless](https://github.com/Ninthless)
