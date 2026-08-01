# Contributing to Codex Quota

Thanks for helping make Codex Quota more reliable and easier to install.

## Before opening a pull request

1. Open or find an issue for behavior changes that may affect the overlay, data source, privacy, or distribution.
2. Keep macOS and Windows behavior aligned where practical.
3. Do not add code that reads login tokens, scans conversations, patches the Codex app, or calls undocumented private quota endpoints.
4. Never commit credentials, signing material, diagnostic output containing private paths, or packaged builds outside the existing release workflow.

## Local checks

For macOS changes:

```bash
swift test
swift build
```

For Windows changes, run on Windows with the .NET 8 SDK:

```powershell
dotnet test Windows/CodexQuota.Core.Tests/CodexQuota.Core.Tests.csproj
dotnet test Windows/CodexQuota.Windows.Tests/CodexQuota.Windows.Tests.csproj
dotnet build Windows/CodexQuota.Windows/CodexQuota.Windows.csproj
```

Documentation-only changes should still pass `git diff --check`, and all links and asset paths should be verified.

## Pull requests

- Explain the user problem and the behavior change.
- Include screenshots for visible UI changes.
- Include tests for parsing, layout, persistence, or discovery changes where feasible.
- Keep generated build output out of commits.
- State which platform and Codex desktop version you tested.

By contributing, you agree that your contribution is licensed under the repository's [MIT License](LICENSE).
