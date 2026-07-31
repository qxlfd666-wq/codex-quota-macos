# Codex Quota for macOS

一个原生 macOS 透明贴附徽标，把 Codex 套餐剩余额度显示在现有 Codex 客户端左下角的用户名右侧。

> 这是非官方社区项目，与 OpenAI 没有隶属或合作关系。Codex、ChatGPT 及 OpenAI 名称归其各自权利人所有。

## 功能

- 百分比直接贴附在 Codex 原有的 `头像 + 用户名` 账户行
- 红色细进度条直观显示剩余额度
- Codex 位于前台时显示，切换到其他应用或最小化 Codex 时自动隐藏
- 跟随 Codex 窗口移动、缩放、切换显示器和全屏空间
- 每 60 秒自动刷新，也可从菜单栏的 `%` 图标手动刷新
- 点击额度徽标可用 macOS 原生取色器自定义颜色，并自动记住选择
- 使用本机 Codex 官方 app-server，不读取或保存登录令牌
- 不修改 `/Applications/ChatGPT.app`，不破坏 OpenAI 代码签名和自动更新
- 不需要辅助功能或屏幕录制权限

## 系统要求

- macOS 14 或更高版本
- 已安装当前版 ChatGPT/Codex for macOS（bundle ID `com.openai.codex`）
- 已通过 ChatGPT 账户登录 Codex（API Key 按量计费模式不会返回套餐百分比）
- Xcode 16 或更新版本（仅构建时需要）

## 使用

1. 解压并打开 `Codex Quota.app`。
2. 打开 Codex；额度徽标会出现在左下角用户名右侧。
3. 点击徽标可自定义颜色；菜单栏中的 `%` 图标也可查看状态、刷新额度或退出徽标。

应用没有独立主窗口。只有徽标自身的小范围响应鼠标点击，用于打开取色器；不会拦截其余 Codex 界面或键盘事件。Codex 左侧边栏需要保持展开；如果未来 Codex 调整账户栏布局，徽标偏移量可能需要同步更新。

## 运行开发版

```bash
swift run CodexQuota
```

## 构建 `.app`

```bash
./scripts/build-app.sh
open "dist/Codex Quota.app"
```

构建产物为 `dist/Codex Quota.app` 和 `dist/Codex Quota.zip`。脚本会执行本机 ad-hoc 签名，适合本机运行；若要分发给其他用户，需要使用 Apple Developer 证书签名并公证。

## 测试

```bash
swift test
```

## 数据来源与隐私

应用启动本机 `codex app-server`，完成 JSONL 初始化后调用：

```json
{ "method": "account/rateLimits/read", "id": 3 }
```

Codex 返回 `usedPercent` 后，应用将剩余额度计算为 `100 - usedPercent`。如果主要和次要窗口同时存在，徽标显示较低的剩余值，以提示当前最紧张的限制。

应用不会读取 `~/.codex/auth.json`、不会扫描会话历史、不会修改 Codex 的 `app.asar`，也不会自行访问私有额度 HTTP 接口。登录与令牌刷新完全交给本机 Codex 处理。

## 许可证

本项目使用 [MIT License](LICENSE)。
