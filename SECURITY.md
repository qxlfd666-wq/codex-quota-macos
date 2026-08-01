# Security policy

## Supported versions

Security fixes are provided for the latest published release.

## Reporting a vulnerability

Please use [GitHub's private vulnerability reporting](https://github.com/qxlfd666-wq/codex-quota/security/advisories/new). Do not open a public issue for a vulnerability and do not include credentials, tokens, or private conversation data in a report.

Include the affected platform and version, impact, reproduction steps, and a minimal proof of concept when appropriate. You should receive an acknowledgement within seven days.

## Security boundaries

Codex Quota asks the local `codex app-server` for rate-limit data. It does not read `auth.json`, store a Codex login token, scan conversations, inject code into the official Codex client, or modify that client's files.

Current community binaries are not signed with Apple Developer ID or a commercial Windows code-signing certificate. Verify downloads against the matching SHA-256 files from the same GitHub Release. A signature or reputation warning alone is not proof of malware, but users should not bypass a warning for a file obtained from another source.
