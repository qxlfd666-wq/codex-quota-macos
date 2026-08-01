# WinGet 待提交清单

此目录保存 Codex Quota v1.0.1 的 WinGet 候选清单，便于审查和复现。**只有清单被 [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) 接受后，用户才能通过 WinGet 安装。**

向上游提交 PR 前，应在 Windows 测试机上完成：

1. 再次确认两个 Release 下载地址和 SHA-256。
2. 对版本目录运行 `winget validate`。
3. 在 Windows Sandbox 测试 x64 安装、启动、命令别名、升级和卸载。
4. 条件允许时，在真实 Windows ARM64 设备上测试 ARM64 包。
5. 确认用户开启开机启动后，便携包卸载时的注册表启动项行为。

当前 Windows Release 仍是未签名 Beta。提交 WinGet 不能替代代码签名，也不保证 SmartScreen 或安全软件不会显示警告。
