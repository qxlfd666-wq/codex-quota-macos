# Codex Quota

[English](README.en.md)

<p align="center">
  <img src="docs/images/social-preview.png" alt="Codex Quota——在账户名称旁显示 Codex 剩余额度" width="100%">
</p>

<p align="center">
  一个轻量的 macOS 与 Windows 辅助程序，在 Codex 桌面客户端左下角的账户名称旁显示套餐剩余额度。
</p>

<p align="center">
  <a href="https://github.com/qxlfd666-wq/codex-quota/actions/workflows/ci.yml"><img src="https://github.com/qxlfd666-wq/codex-quota/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/qxlfd666-wq/codex-quota/releases/latest"><img src="https://img.shields.io/github/v/release/qxlfd666-wq/codex-quota?display_name=tag&sort=semver" alt="最新版本"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/qxlfd666-wq/codex-quota" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/macOS-14%2B-black?logo=apple" alt="macOS 14 或更高版本">
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows11" alt="Windows 10 或 11">
  <img src="https://img.shields.io/badge/Windows-Beta-orange" alt="Windows 测试版">
</p>

> [!IMPORTANT]
> Codex Quota 是非官方社区项目，与 OpenAI 没有隶属、合作或背书关系。Codex、ChatGPT、OpenAI 及相关标识归各自权利人所有。

## 下载

| 平台 | 直接下载 | 系统要求 |
| --- | --- | --- |
| macOS — Apple Silicon 与 Intel | [Codex-Quota-macOS.zip](https://github.com/qxlfd666-wq/codex-quota/releases/latest/download/Codex-Quota-macOS.zip) | macOS 14 或更高版本 |
| Windows x64 **Beta** | [Codex-Quota-Windows-x64.exe](https://github.com/qxlfd666-wq/codex-quota/releases/latest/download/Codex-Quota-Windows-x64.exe) | Windows 10/11 x64 |
| Windows ARM64 **Beta** | [Codex-Quota-Windows-arm64.exe](https://github.com/qxlfd666-wq/codex-quota/releases/latest/download/Codex-Quota-Windows-arm64.exe) | Windows 11 ARM64 |

[最新 Release](https://github.com/qxlfd666-wq/codex-quota/releases/latest) 还包含 Windows `.zip` 包，以及每个下载文件对应的 `.sha256` 校验文件。

> [!WARNING]
> 当前社区构建尚未使用 Apple Developer ID 或 Windows 商业代码签名证书，因此 macOS Gatekeeper 或 Microsoft Defender SmartScreen 可能显示未知开发者提示。请只从本仓库 Releases 下载，并核对对应的 SHA-256。完成签名与更广泛的安装环境验证前，Windows 版应视为 Beta 测试版。

## 动态预览

<p align="center">
  <img src="docs/images/demo.gif" alt="Codex Quota 显示剩余额度并修改徽标颜色" width="760">
</p>

## 功能

- 在 Codex 原有的头像和账户名称旁显示剩余百分比与细进度条。
- 默认使用红色；点击徽标可选择任意颜色，并在下次启动时保留选择。
- Codex 窗口移动、缩放或切换显示器时，徽标会跟随窗口。
- 仅在 Codex 位于前台时显示；Codex 最小化或切换到其他应用后自动隐藏。
- 每 60 秒自动刷新，也可从 macOS 菜单栏或 Windows 系统托盘手动刷新。
- Windows 可在托盘菜单中开启“登录时启动”。
- 使用本机 `codex app-server`，不会向官方 Codex 应用注入代码或修改客户端。
- 不读取或保存 Codex 登录令牌。

## 截图

### 账户名称旁的剩余额度

<img src="docs/images/quota-badge.png" alt="Codex 左下角账户名称旁的剩余额度徽标和进度条" width="660">

### 点击徽标选择颜色

<img src="docs/images/color-picker.png" alt="从 Codex Quota 徽标打开原生取色器" width="250">

## 使用要求

- 当前版本的官方 Codex 桌面客户端，并保持左侧边栏展开。
- 使用会返回套餐使用限制的 ChatGPT 账户登录 Codex。
- API Key 按量计费模式不会返回可供本程序显示的套餐百分比。

Codex Quota 没有独立主窗口。只有徽标自身会响应鼠标点击，覆盖层的其余部分不会拦截 Codex 的键盘或鼠标操作。

## macOS 安装

1. 下载并解压 `Codex-Quota-macOS.zip`。
2. 将 `Codex Quota.app` 移入 `/Applications`，然后打开。
3. 打开 Codex，额度徽标会出现在左下角账户名称旁。
4. 点击徽标可修改颜色；菜单栏的 `%` 图标可查看状态、刷新、打开 Codex 或退出。

如果首次启动被 Gatekeeper 拦截，请先确认应用来自本仓库并尝试打开一次，然后前往**系统设置 → 隐私与安全性**，在 Codex Quota 对应提示处选择**仍要打开**。

## Windows 安装 — Beta

1. 大多数电脑请下载 `Codex-Quota-Windows-x64.exe`；ARM Windows 设备请选择 ARM64 版本。
2. 将这个免安装程序放到准备长期保留的位置，然后运行。
3. 打开 Codex，额度徽标会出现在左下角账户名称旁。
4. 点击徽标可修改颜色；右键系统托盘的 `%` 图标可刷新、修改颜色、设置登录时启动、复制诊断信息或退出。

由于程序目前没有代码签名，SmartScreen 可能阻止运行。仅当文件来自本仓库且已核对校验值时，才选择**更多信息 → 仍要运行**。如果开启登录时启动后移动了程序，需要先关闭再重新开启该选项，让 Windows 记录新路径。

### Windows 无法获取额度

v1.0.1 已修复多个 Codex 安装环境下的 helper 检测问题。如果托盘仍提示无法读取额度：

1. 升级到 v1.0.1 或更高版本。
2. 打开最新版官方 Codex 客户端，并确认已使用 ChatGPT 账户登录；API Key 按量计费模式不会返回套餐百分比。
3. 右键托盘的 `%` 图标，选择**复制诊断信息**，提交[问题反馈](https://github.com/qxlfd666-wq/codex-quota/issues/new)时附上结果。不要提供登录 token 或 `auth.json` 内容。

如果自动检测仍失败，高级用户可将环境变量 `CODEX_QUOTA_CODEX_PATH` 设为支持 `app-server` 的 `codex.exe` 完整路径，然后重启 Codex Quota。

Codex 当前没有公开账户栏坐标接口，因此覆盖层会跟随当前左下角边栏布局。如果未来 Codex 更新改变了布局，徽标偏移量也可能需要更新。

## 校验下载文件

请从同一个 Release 下载程序及其对应的 `.sha256` 文件。macOS 可运行：

```bash
shasum -a 256 -c Codex-Quota-macOS.zip.sha256
```

Windows 可运行下面的命令，并将结果与对应 `.sha256` 文件中的值比较：

```powershell
Get-FileHash .\Codex-Quota-Windows-x64.exe -Algorithm SHA256
```

## 从源码构建

### macOS

安装 Xcode 16 或更新版本，然后直接运行：

```bash
swift run CodexQuota
```

构建本机架构的 `.app` 与 `.zip`：

```bash
./scripts/build-app.sh
open "dist/Codex Quota.app"
```

构建 Apple Silicon + Intel 通用 Release 压缩包：

```bash
./scripts/build-universal-app.sh
```

### Windows

在 Windows 上安装 .NET 8 SDK，然后运行：

```powershell
dotnet run --project Windows/CodexQuota.Windows/CodexQuota.Windows.csproj
```

构建自包含的 x64 单文件程序：

```powershell
dotnet publish Windows/CodexQuota.Windows/CodexQuota.Windows.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## 测试

```bash
swift test
dotnet test Windows/CodexQuota.Core.Tests/CodexQuota.Core.Tests.csproj
dotnet test Windows/CodexQuota.Windows.Tests/CodexQuota.Windows.Tests.csproj
```

GitHub Actions 会在 Pull Request 和 `main` 分支变更时验证 macOS 与 Windows 构建；推送 `v*` 标签后会自动创建 GitHub Release，并附带各平台安装包与 SHA-256 文件。

## 数据来源与隐私

Codex Quota 会启动本机 `codex app-server`，完成 JSONL 初始化后调用：

```json
{ "method": "account/rateLimits/read", "id": 3 }
```

程序用 `100 - usedPercent` 计算剩余额度。如果主要与次要窗口同时存在，则显示较低的剩余值，让徽标反映更紧张的限制。

Codex Quota 不会读取 `~/.codex/auth.json`、扫描会话历史、修改 Codex 客户端，也不会自行调用私有额度 HTTP 接口。认证与令牌刷新完全交给本机 Codex 进程。徽标颜色仅保存在本地；Windows 只有在用户主动开启登录时启动时，才会添加当前用户级别的启动注册表项。

## 许可证

[MIT](LICENSE)
