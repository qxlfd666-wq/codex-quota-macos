# Codex Quota 推广文案库

本文档用于项目软启动期间的社区发布。发布前请先阅读 [`launch-checklist.md`](launch-checklist.md)，把文中的链接替换为对应渠道的 UTM 链接，并再次确认下载、截图和风险说明与最新版本一致。

## 发布时必须保留的事实

- 项目名称：Codex Quota
- 项目地址：`https://github.com/qxlfd666-wq/codex-quota`
- 支持平台：macOS 14+；Windows 10/11 x64；Windows 11 ARM64
- Windows 版仍为 Beta，当前社区构建未使用商业代码签名证书，首次运行可能触发 Microsoft Defender SmartScreen
- macOS 社区构建当前未使用 Apple Developer ID 签名与公证，首次运行可能显示“未识别的开发者”
- 这是非官方社区项目，与 OpenAI 没有隶属或合作关系
- 额度读取发生在用户本机：应用通过本机 `codex app-server` 获取套餐额度，不读取 `~/.codex/auth.json`，不上传或保存登录 token
- ChatGPT 套餐账户可返回百分比；API Key 按量计费模式不会返回套餐百分比
- 安装包只应从本项目 GitHub Releases 下载，并可使用随附的 SHA-256 文件核验

不要使用“官方”“绝对安全”“零风险”“已签名”“已上架 Microsoft Store / Mac App Store”等尚不成立的描述，也不要把 SmartScreen 提示解释成杀毒软件误报。

## 核心信息

### 一句话中文介绍

Codex Quota 是一个开源的 macOS / Windows 小工具，把 Codex 套餐剩余额度百分比和进度条显示在客户端左下角用户名旁边。

### One-line English pitch

Codex Quota is an open-source macOS and Windows companion that shows your remaining Codex plan quota beside your account name.

### 中文短帖

做了一个开源小工具 Codex Quota：把 Codex 套餐剩余额度百分比和进度条直接放在客户端左下角用户名旁边，点击徽标还能改颜色。支持 macOS 和 Windows；Windows 版目前是未签名 Beta，可能触发 SmartScreen。额度通过本机 `codex app-server` 读取，不读取或保存登录 token。非 OpenAI 官方项目。项目地址：{CHANNEL_URL}

### English short post

I built Codex Quota, an open-source macOS/Windows companion that puts your remaining Codex plan quota beside your account name, with a small progress bar and customizable color. Windows is currently an unsigned beta and may trigger SmartScreen. Quota data is read locally through `codex app-server`; the app does not read or store login tokens. Unofficial and not affiliated with OpenAI: {CHANNEL_URL}

## 素材组合

建议准备以下素材，并保持界面中的账号名称、通知和路径不含隐私信息：

1. **首图 / 社交预览图（1280 × 640）**：产品名、一句话价值、macOS + Windows、真实额度徽标局部截图。不要在图上写“官方”或使用容易造成官方背书误解的 OpenAI 标识。
2. **10–15 秒演示 GIF**：打开 Codex → 用户名旁出现百分比和进度条 → 点击徽标 → 选择另一种颜色 → 徽标更新。循环播放，宽度建议 800–1200 px，尽量控制在 8 MB 内。
3. **功能截图**：完整展示左侧栏和用户名旁的徽标，让观看者立刻理解显示位置。
4. **颜色选择截图**：展示点击徽标后出现系统取色器，以及修改后的结果。
5. **跨平台图**：macOS 和 Windows 实机画面并排；如果 Windows 画面尚未经过真实机器验证，不要使用合成图冒充实机截图。
6. **安装透明度图**：在面向 Windows 用户的帖子中，附上 SmartScreen 原因、GitHub Releases 来源和 SHA-256 核验方法；它是说明图，不应伪装成“一键无警告安装”。

各渠道的首图建议：V2EX / Linux.do / 掘金用完整功能截图加 GIF；即刻用 1 张首图加 1 个 GIF；Reddit 与 OpenAI Developer Community 先放 GIF；Show HN 让仓库 README 的首屏自行说明产品；Product Hunt 使用社交预览图、GIF、macOS 实机图、Windows Beta 说明图组成 gallery。

---

## V2EX

建议节点：`分享创造`。语气以“解决自己的小痛点、邀请测试”为主，不做夸张增长叙事。

### 标题

> [分享创造] 做了一个把 Codex 剩余额度显示在用户名旁的小工具，支持 macOS / Windows

### 正文

> 最近用 Codex 时经常想知道套餐还剩多少额度，但每次单独去找用量信息会打断当前工作。我做了一个很小的开源工具 **Codex Quota**，把剩余额度百分比和细进度条直接显示在 Codex 客户端左下角的用户名旁边。
>
> 目前有这些功能：
>
> - 支持 macOS 和 Windows
> - 百分比与进度条跟随 Codex 窗口
> - 点击徽标可自定义颜色
> - 每 60 秒自动刷新，也可手动刷新
> - 不修改或注入 Codex 客户端
>
> 额度读取发生在本机：工具通过本机 `codex app-server` 获取套餐额度，不读取 `~/.codex/auth.json`，也不上传或保存登录 token。ChatGPT 套餐账户可返回百分比，API Key 按量计费模式不会返回这个值。
>
> 先说明两个限制：这是非 OpenAI 官方的社区项目；Windows 版目前还是未签名 Beta，第一次打开可能触发 SmartScreen。macOS 社区构建也尚未使用 Developer ID 签名与公证。请只从 GitHub Releases 下载，并按需要核对 SHA-256。
>
> 项目和下载：{CHANNEL_URL}
>
> 现在最想收集两类反馈：不同屏幕缩放下徽标是否对齐，以及 Windows 上“无法获取额度”的诊断信息。欢迎提 Issue，也欢迎直接指出安装说明哪里不够清楚。

### 跟帖短回复

> 谢谢测试。遇到问题时请附上系统版本、Codex 版本、屏幕缩放比例和工具里的“复制诊断信息”；请勿粘贴 `auth.json` 或任何 token。Issue：{ISSUES_URL}

### 截图顺序

演示 GIF → 用户名旁的完整截图 → 颜色选择截图 → Windows Beta / SmartScreen 说明图。

---

## Linux.do

虽然项目当前没有 Linux 桌面版，Linux.do 仍适合收集开发者对本机 app-server、跨平台实现和打包流程的反馈。标题和首段应直接写明“目前只支持 macOS / Windows”，避免 Linux 用户误以为已有 Linux 包。

### 标题

> [开源] Codex Quota：在用户名旁显示剩余额度的 macOS / Windows 小工具

### 正文

> 写了一个开源桌面小工具 **Codex Quota**，用来解决一个很具体的问题：在 Codex 客户端里随时看到套餐剩余额度，不用离开当前界面。
>
> 它把百分比和进度条贴在左下角账户名称旁，点击可以改颜色，并随 Codex 窗口显示或隐藏。目前支持 **macOS 和 Windows**，暂时没有 Linux 桌面包。
>
> 实现上，应用在本机启动 `codex app-server`，调用 `account/rateLimits/read`，再将 `usedPercent` 换算成剩余值。它不读取 `~/.codex/auth.json`，不保存或上传登录 token，也不向 Codex 客户端注入代码。
>
> 项目是非 OpenAI 官方社区项目。Windows 版目前为未签名 Beta，可能出现 SmartScreen；macOS 构建也尚未进行 Developer ID 签名与公证。下载时请认准 GitHub Releases，并可核对随附的 SHA-256。
>
> 仓库：{CHANNEL_URL}
>
> 欢迎从实现和安全边界上拍砖，也希望有人帮忙测试 Windows ARM64、不同 DPI 和多显示器场景。

### 短帖

> 开源了 Codex Quota：在 Codex 左下角用户名旁显示套餐剩余额度和进度条，可点选颜色。支持 macOS / Windows（Windows 当前为未签名 Beta，可能触发 SmartScreen），额度仅通过本机 `codex app-server` 读取，不读取或保存 token。非 OpenAI 官方项目：{CHANNEL_URL}

### 截图顺序

真实演示 GIF → 数据读取流程简图 → macOS / Windows 并排图 → 已知限制。

---

## 即刻

### 标题 / 首句

> 给 Codex 补上了一个我一直想要的小功能：在名字旁直接看剩余额度。

### 正文

> 做了个开源小工具 **Codex Quota**，把套餐剩余额度百分比和细进度条放到 Codex 左下角用户名旁边。点击徽标还能换颜色，工作时不用再切出去找用量。
>
> 支持 macOS 和 Windows。额度通过本机 `codex app-server` 读取，不读取或保存登录 token；项目非 OpenAI 官方。Windows 目前是未签名 Beta，首次运行可能触发 SmartScreen，建议只从 GitHub Releases 下载并核对 SHA-256。
>
> 想找一些真实用户帮我测对齐、多屏和 Windows DPI：{CHANNEL_URL}

### 超短帖

> 把 Codex 剩余额度做成了用户名旁的一枚小徽标，还能点一下换颜色。开源，支持 macOS / Windows；Windows 是未签名 Beta。非官方项目，额度仅从本机 Codex 读取：{CHANNEL_URL}

### 截图顺序

首图用用户名与额度徽标的近景；第二张放 10–15 秒 GIF；第三张放取色器。正文折叠前必须出现项目链接和 Windows Beta 提示。

---

## 掘金

适合发布一篇实现复盘，而不是只贴下载链接。文章应包含问题、实现边界、跨平台差异和开源邀请。

### 标题

> 我给 Codex 做了一个剩余额度悬浮徽标：从 macOS 到 Windows 的开源实现

### 摘要

> Codex Quota 是一个开源的 macOS / Windows 桌面伴侣：它将套餐剩余额度显示在 Codex 客户端左下角用户名旁，并提供进度条、颜色定制和自动刷新。本文介绍如何通过本机 `codex app-server` 读取额度、如何让徽标跟随客户端窗口，以及跨平台打包中遇到的限制。

### 正文开场

> 使用 Codex 时，我希望不用离开当前任务就能看到套餐还剩多少额度。于是我做了 **Codex Quota**：一枚贴在账户名称旁的小徽标，用百分比和进度条呈现剩余量。
>
> 这个项目没有修改 Codex 客户端，也没有读取登录文件。应用在本机启动 `codex app-server`，调用 `account/rateLimits/read`，根据返回的 `usedPercent` 计算剩余比例。所有额度读取都在本机完成，工具本身不上传或保存登录 token。
>
> 仓库与下载：{CHANNEL_URL}

### 建议文章结构

1. 为什么把额度放在用户名旁，而不是再做一个独立窗口
2. `account/rateLimits/read` 的本机调用与数据最小化
3. macOS 窗口跟随、点击区域和原生取色器
4. Windows WinForms、托盘、DPI 和 Codex 可执行文件探测
5. 失败诊断：为什么 API Key 模式没有套餐百分比
6. 当前分发限制：Windows 未签名 Beta 会触发 SmartScreen，macOS 尚未 Developer ID 签名与公证
7. 下一步：签名、安装体验和更多实机测试

### 结尾短帖

> 项目目前支持 macOS 14+、Windows x64 和 ARM64，是非 OpenAI 官方社区项目。Windows 构建尚未签名，可能触发 SmartScreen；请仅从 GitHub Releases 下载并核对 SHA-256。如果你愿意帮忙测试多屏、DPI 或 Windows ARM64，欢迎到 Issue 留下环境与诊断信息：{CHANNEL_URL}

### 截图顺序

首屏 GIF → 界面定位图 → 数据读取流程图 → macOS / Windows 结构对照 → 安装与风险说明。

---

## Reddit r/codex

Before posting, check the current subreddit rules and active self-promotion or usage megathreads. Use a text post with an actual demo rather than a link-only submission.

### Title

> I built an open-source macOS/Windows overlay that shows remaining Codex quota beside your account name

### Body

> I kept wanting to see my remaining Codex plan quota without leaving the client, so I built **Codex Quota**.
>
> It adds a small percentage and progress bar beside the account name in the lower-left corner of Codex. The badge follows the Codex window, refreshes automatically, and opens a native color picker when clicked.
>
> A few implementation and privacy details:
>
> - macOS 14+ and Windows x64/ARM64 are supported.
> - Quota data is read locally through `codex app-server` using `account/rateLimits/read`.
> - The companion does not read `~/.codex/auth.json`, store login tokens, inject code into Codex, or upload quota data.
> - ChatGPT plan accounts can return a percentage; API-key pay-as-you-go mode does not expose this plan percentage.
>
> This is an unofficial community project and is not affiliated with OpenAI. **The Windows build is currently an unsigned beta**, so Microsoft Defender SmartScreen may warn on first launch. The macOS community build is not Developer ID signed/notarized yet either. Please download only from this repository's Releases and verify the published SHA-256 if needed.
>
> Demo, source, and downloads: {CHANNEL_URL}
>
> I would especially appreciate feedback on Windows DPI scaling, ARM64, multi-monitor positioning, and whether the install notes are clear enough. Please never include an auth file or token in a bug report.

### Short version

> I built Codex Quota, an open-source macOS/Windows companion that shows remaining plan quota beside your Codex account name. Custom color, progress bar, local `codex app-server` data only; it does not read/store login tokens. Unofficial project. Windows is currently an unsigned beta and may trigger SmartScreen: {CHANNEL_URL}

### Media order

Demo GIF → full Codex sidebar screenshot → color customization → Windows beta disclosure.

---

## OpenAI Developer Community

Use the Codex category and frame the post as a community project plus a request for compatibility feedback. Do not imply endorsement by OpenAI.

### Title

> Open-source Codex quota badge for macOS and Windows — looking for compatibility feedback

### Body

> I made an unofficial open-source companion called **Codex Quota**. It displays the remaining ChatGPT plan quota as a percentage and progress bar beside the account name in the lower-left corner of the Codex client.
>
> The app reads quota data locally by launching `codex app-server` and calling `account/rateLimits/read`. It does not read `~/.codex/auth.json`, store or upload login tokens, modify the Codex client, or inject code. API-key pay-as-you-go sessions do not provide the plan percentage this UI expects.
>
> Current builds support macOS 14+ and Windows x64/ARM64. The Windows release is still an **unsigned beta**, so SmartScreen may warn on first launch; the macOS community build is not Developer ID signed/notarized yet. Users should download only from GitHub Releases and can verify the provided SHA-256 files.
>
> Repository, demo, and downloads: {CHANNEL_URL}
>
> This is a community project, not an OpenAI product and not affiliated with OpenAI. I am looking for feedback on window positioning, different display scales, Windows ARM64, and any Codex version compatibility issues.

### Short version

> Sharing an unofficial open-source Codex quota badge for macOS/Windows. It reads the percentage locally through `codex app-server`, does not read or store login tokens, and puts the result beside the account name. Windows is an unsigned beta and may trigger SmartScreen. Feedback welcome: {CHANNEL_URL}

### Media order

Demo GIF first, followed by one architecture/privacy diagram and platform screenshots.

---

## Show HN

Follow Show HN rules: the repository and downloadable build must be accessible without sign-up, and the author should stay available for technical questions after posting.

### Title

> Show HN: Codex Quota – See remaining Codex plan usage beside your account name

### Body

> I built Codex Quota because checking plan usage outside the Codex client kept interrupting my workflow. It is a small open-source macOS/Windows companion that places the remaining quota percentage and a progress bar beside the account name in the lower-left corner.
>
> The interesting part is that it does not modify or inject into the Codex client. It launches the local `codex app-server`, calls `account/rateLimits/read`, and converts the returned `usedPercent` into a remaining value. It does not read `~/.codex/auth.json`, store login tokens, or upload quota data.
>
> The macOS implementation follows the Codex window and uses the native color panel. The Windows implementation uses WinForms, a tray menu, DPI-aware positioning, and x64/ARM64 self-contained builds.
>
> Source and demo: {CHANNEL_URL}
>
> Current limitations: it works with ChatGPT plan accounts, not API-key pay-as-you-go usage; the sidebar needs to remain expanded; and UI layout changes in Codex may require an offset update. This is an unofficial project with no OpenAI affiliation. Windows is currently an unsigned beta and may trigger SmartScreen, while macOS is not yet Developer ID signed/notarized.
>
> I would value feedback on the implementation boundaries, cross-platform window tracking, and ways to make distribution less intimidating before a broader launch.

### Short follow-up

> For transparency: downloads come from GitHub Releases with SHA-256 files. The current Windows beta is not code-signed, so SmartScreen can appear; the warning is not presented as a false positive. The app reads quota through the local Codex process and does not ask users for credentials.

### Media order

Show HN itself is text-first. Make the repository README open with the GIF and a visible “Windows unsigned beta” note; avoid image-only explanations.

---

## Product Hunt

**Do not schedule the Product Hunt launch while Windows remains an unsigned Beta unless the Windows download is clearly labeled as an early-access build.** Prefer waiting until at least one low-friction, trusted distribution path is ready (for example, a signed installer or store listing) and the first six days of feedback have resolved critical install issues. The following copy is a draft for that later launch.

### Product name

> Codex Quota

### Tagline

> See your remaining Codex quota without leaving the client

### Short description

> An open-source macOS and Windows companion that adds a remaining-quota percentage and customizable progress bar beside your Codex account name. It reads data locally through `codex app-server` and never asks for your login token.

### First comment / maker story

> Hi Product Hunt — I built Codex Quota to remove a small but repeated interruption from my day: leaving Codex just to check how much plan usage remains.
>
> The app places a compact percentage and progress bar beside the account name, follows the Codex window, refreshes automatically, and lets you pick a color. It supports macOS and Windows.
>
> I deliberately kept the data boundary narrow. Quota is read on the user's machine through the local `codex app-server`; Codex Quota does not read `~/.codex/auth.json`, ask for credentials, store login tokens, inject into Codex, or upload quota data.
>
> This is an unofficial open-source community project and is not affiliated with OpenAI. You can inspect the source, download builds, and verify release checksums here: {CHANNEL_URL}
>
> I would love feedback on the visual placement, accessibility of the color options, multi-monitor behavior, and which distribution format would make installation easiest for you.

### Short social post for launch day

> Codex Quota is live: an open-source macOS/Windows companion that keeps your remaining Codex plan quota beside your account name. Local quota reading, customizable progress bar, no login token collection. Unofficial and not affiliated with OpenAI: {CHANNEL_URL}

### Gallery order

1. 1280 × 640 value proposition image
2. 10–15 second real workflow GIF or MP4
3. macOS full-window screenshot
4. Windows full-window screenshot with the exact supported version
5. Local data-flow and privacy diagram
6. Color customization and accessibility options

If the Windows build is still unsigned at launch time, add “Windows beta — unsigned build may trigger SmartScreen” to the short description and first gallery image; do not hide it below the fold.

---

## 发布者回复模板

### 无法获取额度

> 谢谢反馈。请先确认使用的是最新版 Codex Quota、官方 Codex 客户端已通过 ChatGPT 套餐账户登录，并在托盘菜单选择“复制诊断信息”。请附上系统版本、Codex 版本和诊断信息，但不要粘贴 `auth.json` 或任何 token。API Key 按量计费模式目前不会返回套餐百分比。

### SmartScreen / 风险提示

> Windows 版目前是尚未使用商业代码签名证书的 Beta，因此 SmartScreen 可能阻止首次打开。这不是在声称警告为误报。请只从项目 GitHub Releases 下载，并用随附的 SHA-256 文件核对；如果你不愿绕过系统警告，可以先从源码构建或等待签名版本。

### macOS 未识别开发者

> 当前 macOS 社区构建尚未使用 Apple Developer ID 签名与公证，所以系统可能显示“未识别的开发者”。请只使用 GitHub Releases 中的构建并核对 SHA-256；如果你不希望调整系统安全设置，可以从源码构建或等待签名版本。

### 隐私疑问

> 额度通过用户本机的 `codex app-server` 读取。Codex Quota 不读取 `~/.codex/auth.json`，不要求、上传或保存登录 token，也不注入或修改 Codex 客户端。实现和具体调用都可以在仓库中审查。

### 与 OpenAI 的关系

> Codex Quota 是独立维护的非官方开源社区项目，与 OpenAI 没有隶属、合作或背书关系。
