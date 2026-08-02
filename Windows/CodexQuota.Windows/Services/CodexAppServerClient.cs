using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexQuota.Core;

namespace CodexQuota.Windows.Services;

internal sealed class CodexAppServerClient
{
    private const int InitializeRequestId = 1;
    private const int AccountRequestId = 2;
    private const int RateLimitsRequestId = 3;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan InitializeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RateLimitsTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(2);
    private const int ProbeOutputCharacterLimit = 8 * 1024;
    private static readonly string ClientVersion = GetClientVersion();
    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SecretKeyPattern = new(
        @"\bsk-[A-Za-z0-9_-]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex JwtPattern = new(
        @"\b[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b",
        RegexOptions.CultureInvariant);
    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly IReadOnlyList<string>? _candidatePaths;

    internal CodexAppServerClient(IReadOnlyList<string>? candidatePaths = null)
    {
        _candidatePaths = candidatePaths;
    }

    public string LastDiagnostic { get; private set; } = "尚未读取额度。";

    public async Task<QuotaSnapshot> FetchSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await Task.Run(
                () => _candidatePaths is null
                    ? LocateCodexCommands()
                    : ResolveExistingCandidates(
                        _candidatePaths.Select(path => new CodexCommand(path, "测试候选"))),
                cancellationToken)
            .ConfigureAwait(false);
        var attempts = new List<string>();
        if (candidates.Count == 0)
            throw Failure(
                "未找到 Codex 命令行组件。请先打开并登录最新版 Codex，或重新安装官方客户端。",
                null,
                attempts);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var probe = await ProbeAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (!probe.IsCodexCli)
            {
                attempts.Add(DescribeAttempt(candidate, probe.Failure ?? "不是 codex-cli"));
                continue;
            }

            var command = candidate with { Version = probe.Version };
            try
            {
                var snapshot = await FetchFromCommandAsync(command, cancellationToken).ConfigureAwait(false);
                LastDiagnostic = BuildDiagnostic("额度读取成功", command, attempts);
                return snapshot;
            }
            catch (CodexCandidateException exception)
            {
                attempts.Add(DescribeAttempt(command, exception.Message));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var message = exception is CodexClientException or QuotaUnavailableException
                    ? exception.Message
                    : $"Codex 额度读取失败：{exception.Message}";
                throw Failure(message, command, attempts, exception);
            }
        }

        throw Failure(
            "没有找到可用的 Codex 命令行组件。请升级或重新安装官方 Codex 后重试。",
            null,
            attempts);
    }

    private async Task<QuotaSnapshot> FetchFromCommandAsync(
        CodexCommand command,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(command, "app-server") };
        try
        {
            if (!process.Start())
                throw new CodexCandidateException("无法启动 helper");
        }
        catch (Exception exception) when (exception is not CodexCandidateException)
        {
            throw new CodexCandidateException($"无法启动 helper：{Sanitize(exception.Message)}", exception);
        }

        var stderrTask = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            using (var initializeTimeout = CreateTimeout(cancellationToken, InitializeTimeout))
            {
                try
                {
                    await WriteLineAsync(
                        process.StandardInput,
                        CodexAppServerMessages.Initialize(
                            InitializeRequestId,
                            "codex_quota_windows",
                            "Codex Quota",
                            ClientVersion),
                        initializeTimeout.Token).ConfigureAwait(false);
                    await WaitForSuccessfulResponseAsync(
                        process.StandardOutput,
                        InitializeRequestId,
                        initializeTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new CodexCandidateException("app-server 初始化超时");
                }
                catch (CodexClientException exception)
                {
                    throw new CodexCandidateException(exception.Message, exception);
                }
            }

            using var requestTimeout = CreateTimeout(cancellationToken, RateLimitsTimeout);
            try
            {
                var request = string.Join(
                    '\n',
                    CodexAppServerMessages.Initialized(),
                    CodexAppServerMessages.AccountRead(AccountRequestId),
                    CodexAppServerMessages.RateLimitsRead(RateLimitsRequestId));
                await process.StandardInput.WriteAsync((request + "\n").AsMemory(), requestTimeout.Token)
                    .ConfigureAwait(false);
                await process.StandardInput.FlushAsync(requestTimeout.Token).ConfigureAwait(false);

                JsonElement? accountResult = null;
                var accountResponseReceived = false;
                JsonElement? rateLimitsResult = null;
                while (!accountResponseReceived || rateLimitsResult is null)
                {
                    requestTimeout.Token.ThrowIfCancellationRequested();
                    var line = await process.StandardOutput.ReadLineAsync(requestTimeout.Token)
                        .ConfigureAwait(false);
                    if (line is null)
                        break;

                    using var document = TryParse(line);
                    if (document is null || document.RootElement.ValueKind != JsonValueKind.Object ||
                        !TryReadId(document.RootElement, out var id))
                        continue;

                    if (document.RootElement.TryGetProperty("error", out var error))
                    {
                        if (id == AccountRequestId)
                        {
                            accountResponseReceived = true;
                            continue;
                        }

                        if (id == RateLimitsRequestId)
                        {
                            var message = ReadErrorMessage(error) ?? "未知错误";
                            if (IsProtocolCompatibilityError(error))
                                throw new CodexCandidateException($"额度接口不兼容：{message}");
                            throw new CodexClientException($"Codex 无法读取额度：{message}");
                        }
                        continue;
                    }

                    if (!document.RootElement.TryGetProperty("result", out var result))
                        continue;
                    if (id == RateLimitsRequestId && result.ValueKind != JsonValueKind.Object)
                        throw new CodexCandidateException("额度接口返回格式不兼容");
                    if (id == AccountRequestId)
                    {
                        accountResponseReceived = true;
                        if (result.ValueKind == JsonValueKind.Object)
                            accountResult = result.Clone();
                    }
                    else if (id == RateLimitsRequestId)
                    {
                        if (result.ValueKind == JsonValueKind.Object)
                            rateLimitsResult = result.Clone();
                    }
                }

                if (rateLimitsResult is not { } rateLimits)
                {
                    if (process.HasExited)
                        throw new CodexCandidateException(
                            $"app-server 在返回额度前退出（代码 {process.ExitCode}）");
                    throw new CodexClientException("Codex 返回了无法识别的额度数据。");
                }

                return CodexQuotaParser.Parse(accountResult ?? EmptyObject(), rateLimits);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CodexClientException("读取额度超时，请检查网络和 Codex 登录状态后重试。");
            }
        }
        finally
        {
            await StopProcessAsync(process, stderrTask).ConfigureAwait(false);
        }
    }

    private static async Task WriteLineAsync(
        StreamWriter input,
        string message,
        CancellationToken cancellationToken)
    {
        await input.WriteAsync((message + "\n").AsMemory(), cancellationToken).ConfigureAwait(false);
        await input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForSuccessfulResponseAsync(
        StreamReader output,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await output.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                throw new CodexClientException("Codex 初始化失败：app-server 提前退出。");

            using var document = TryParse(line);
            if (document is null || document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadId(document.RootElement, out var id) || id != expectedId)
                continue;

            if (document.RootElement.TryGetProperty("error", out var error))
                throw new CodexClientException(
                    $"Codex 初始化失败：{ReadErrorMessage(error) ?? "未知错误"}");

            if (document.RootElement.TryGetProperty("result", out _))
                return;

            throw new CodexClientException("Codex 初始化失败：返回了无法识别的数据。");
        }
    }

    private static async Task<CodexProbe> ProbeAsync(
        CodexCommand command,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(command, "--version") };
        try
        {
            if (!process.Start())
                return new CodexProbe(false, null, "无法启动版本检查");
        }
        catch (Exception exception)
        {
            return new CodexProbe(false, null, $"无法启动：{Sanitize(exception.Message)}");
        }

        var stdoutTask = ReadBoundedTextAsync(process.StandardOutput);
        var stderrTask = ReadBoundedTextAsync(process.StandardError);
        using var timeout = CreateTimeout(cancellationToken, ProbeTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await DrainProbeAsync(process, stdoutTask, stderrTask).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                throw;
            return new CodexProbe(false, null, "版本检查超时");
        }

        var output = ((await stdoutTask.ConfigureAwait(false)) + "\n" +
                      (await stderrTask.ConfigureAwait(false)))
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("codex-cli", StringComparison.OrdinalIgnoreCase));
        return process.ExitCode == 0 && output is not null
            ? new CodexProbe(true, Sanitize(output), null)
            : new CodexProbe(false, null, $"不是 codex-cli（退出代码 {process.ExitCode}）");
    }

    private static ProcessStartInfo CreateStartInfo(CodexCommand command, string argument)
    {
        ProcessStartInfo startInfo;
        if (command.Path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            command.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo = new ProcessStartInfo(commandProcessor)
            {
                Arguments = $"/d /s /c \"\"{command.Path}\" {argument}\""
            };
        }
        else
        {
            startInfo = new ProcessStartInfo(command.Path);
            startInfo.ArgumentList.Add(argument);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(home))
            startInfo.WorkingDirectory = home;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardInputEncoding = new UTF8Encoding(false);
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        return startInfo;
    }

    private static IReadOnlyList<CodexCommand> LocateCodexCommands()
    {
        var candidates = new List<CodexCommand>();
        AddCandidate(
            candidates,
            Environment.GetEnvironmentVariable("CODEX_QUOTA_CODEX_PATH"),
            "CODEX_QUOTA_CODEX_PATH");

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AddRunningDesktopAppCandidates(candidates, "Codex");

        var desktopBin = Path.Combine(local, "OpenAI", "Codex", "bin");
        AddCandidate(candidates, Path.Combine(desktopBin, "codex.exe"), "Codex 桌面 helper");
        AddNestedBinCandidates(candidates, desktopBin, "Codex 桌面 helper");

        AddMsixCacheCandidates(candidates, local);
        AddRunningDesktopAppCandidates(candidates, "ChatGPT");

        AddCandidate(
            candidates,
            Path.Combine(home, ".codex", "packages", "standalone", "current", "bin", "codex.exe"),
            "Codex standalone");
        AddCandidate(
            candidates,
            Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
            "Codex 安装目录");
        AddCandidate(
            candidates,
            Path.Combine(local, "Programs", "Codex", "resources", "codex.exe"),
            "Codex 安装目录");
        AddCandidate(
            candidates,
            Path.Combine(local, "Programs", "ChatGPT", "resources", "codex.exe"),
            "ChatGPT 安装目录");
        AddCandidate(candidates, Path.Combine(roaming, "npm", "codex.cmd"), "npm Codex CLI");
        AddCandidate(candidates, Path.Combine(home, ".local", "bin", "codex.exe"), "用户 Codex CLI");

        var environmentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in environmentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            AddCandidate(candidates, Path.Combine(directory.Trim().Trim('"'), "codex.exe"), "PATH");
            AddCandidate(candidates, Path.Combine(directory.Trim().Trim('"'), "codex.cmd"), "PATH");
        }

        return ResolveExistingCandidates(candidates);
    }

    private static IReadOnlyList<CodexCommand> ResolveExistingCandidates(
        IEnumerable<CodexCommand> candidates)
    {
        var existing = new List<CodexCommand>();
        foreach (var candidate in candidates)
        {
            try
            {
                var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate.Path));
                if (File.Exists(path))
                    existing.Add(candidate with { Path = path });
            }
            catch
            {
                // Ignore malformed environment or PATH entries and keep checking safe candidates.
            }
        }

        return existing
            .DistinctBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddRunningDesktopAppCandidates(
        List<CodexCommand> candidates,
        string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var executable = process.MainModule?.FileName;
                    if (executable is null)
                        continue;
                    foreach (var helper in CodexExecutableDiscovery.DesktopHelperCandidates(executable))
                        AddCandidate(candidates, helper, $"正在运行的 {processName}");
                }
                catch
                {
                    // Store package ACLs can hide the executable from other processes.
                }
            }
        }
    }

    private static void AddNestedBinCandidates(
        List<CodexCommand> candidates,
        string binDirectory,
        string source)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(binDirectory)
                         .OrderByDescending(Directory.GetLastWriteTimeUtc))
                AddCandidate(candidates, Path.Combine(directory, "codex.exe"), source);
        }
        catch
        {
            // The directory may not exist or may be protected by Store package ACLs.
        }
    }

    private static void AddMsixCacheCandidates(List<CodexCommand> candidates, string local)
    {
        var packagesDirectory = Path.Combine(local, "Packages");
        try
        {
            foreach (var packageDirectory in Directory.EnumerateDirectories(
                         packagesDirectory,
                         "OpenAI.Codex_*",
                         SearchOption.TopDirectoryOnly))
            {
                var binDirectory = Path.Combine(
                    packageDirectory,
                    "LocalCache",
                    "Local",
                    "OpenAI",
                    "Codex",
                    "bin");
                AddCandidate(candidates, Path.Combine(binDirectory, "codex.exe"), "Codex MSIX helper");
                AddNestedBinCandidates(candidates, binDirectory, "Codex MSIX helper");
            }
        }
        catch
        {
            // The package cache may be absent or protected.
        }
    }

    private static void AddCandidate(List<CodexCommand> candidates, string? path, string source)
    {
        if (!string.IsNullOrWhiteSpace(path))
            candidates.Add(new CodexCommand(path.Trim().Trim('"'), source));
    }

    private CodexClientException Failure(
        string message,
        CodexCommand? command,
        IReadOnlyCollection<string> attempts,
        Exception? innerException = null)
    {
        LastDiagnostic = BuildDiagnostic(message, command, attempts);
        return new CodexClientException(message, LastDiagnostic, innerException);
    }

    private static string BuildDiagnostic(
        string status,
        CodexCommand? command,
        IReadOnlyCollection<string> attempts)
    {
        var lines = new List<string>
        {
            $"Codex Quota Windows {ClientVersion}",
            $"时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            $"系统：{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})",
            $"进程架构：{RuntimeInformation.ProcessArchitecture}",
            $"状态：{Sanitize(status)}"
        };
        if (command is not null)
        {
            lines.Add($"Helper：{command.Source} · {command.Version ?? "版本未知"}");
            lines.Add($"路径：{Sanitize(command.Path)}");
        }

        if (attempts.Count > 0)
        {
            lines.Add("候选检查：");
            lines.AddRange(attempts.Take(8).Select(attempt => $"- {attempt}"));
        }

        lines.Add("诊断信息不主动记录登录令牌或额度响应；发送前请再检查一遍。");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeAttempt(CodexCommand command, string failure) =>
        $"{command.Source} · {Sanitize(command.Path)} · {Sanitize(failure)}";

    private static string RedactPath(string path)
    {
        var replacements = new[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
                (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")
            }
            .Where(item => !string.IsNullOrWhiteSpace(item.Item1))
            .OrderByDescending(item => item.Item1.Length);
        var redacted = path;
        foreach (var (folder, variable) in replacements)
            redacted = redacted.Replace(folder, variable, StringComparison.OrdinalIgnoreCase);
        return redacted;
    }

    private static string Sanitize(string text)
    {
        var value = RedactPath(text).Replace('\r', ' ').Replace('\n', ' ').Trim();
        value = BearerPattern.Replace(value, "Bearer [已隐藏]");
        value = SecretKeyPattern.Replace(value, "[密钥已隐藏]");
        value = JwtPattern.Replace(value, "[JWT 已隐藏]");
        value = EmailPattern.Replace(value, "[邮箱已隐藏]");
        return value.Length <= 240 ? value : value[..239] + "…";
    }

    private static string? ReadErrorMessage(JsonElement error)
    {
        if (error.ValueKind != JsonValueKind.Object ||
            !error.TryGetProperty("message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.String)
            return null;
        var message = messageElement.GetString();
        return string.IsNullOrWhiteSpace(message) ? null : Sanitize(message);
    }

    private static bool IsProtocolCompatibilityError(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("code", out var codeElement))
        {
            var code = 0;
            var hasCode = codeElement.ValueKind == JsonValueKind.Number
                ? codeElement.TryGetInt32(out code)
                : codeElement.ValueKind == JsonValueKind.String &&
                  int.TryParse(codeElement.GetString(), out code);
            if (hasCode && code is -32600 or -32601 or -32602)
                return true;
        }

        var message = ReadErrorMessage(error);
        return message is not null &&
               (message.Contains("method not found", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unknown method", StringComparison.OrdinalIgnoreCase));
    }

    private static CancellationTokenSource CreateTimeout(
        CancellationToken cancellationToken,
        TimeSpan duration)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        return timeout;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort cleanup for a rejected executable candidate.
        }
    }

    private static async Task StopProcessAsync(Process process, Task stderrTask)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch
        {
            // The short-lived app-server may already have closed its input pipe.
        }

        var exitedGracefully = false;
        try
        {
            using var gracefulExit = new CancellationTokenSource(GracefulExitTimeout);
            await process.WaitForExitAsync(gracefulExit.Token).ConfigureAwait(false);
            exitedGracefully = true;
        }
        catch
        {
            // Give EOF a chance to stop the helper before falling back to a hard kill.
        }

        if (!exitedGracefully)
        {
            TryKill(process);
            try
            {
                await process.WaitForExitAsync()
                    .WaitAsync(GracefulExitTimeout)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Do not hold up the tray app if process cleanup races with exit.
            }
        }

        try
        {
            await stderrTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch
        {
            // stderr is streamed to a null sink and never retained because it may be sensitive.
        }
    }

    private static async Task<string> ReadBoundedTextAsync(StreamReader reader)
    {
        var output = new StringBuilder(ProbeOutputCharacterLimit);
        var buffer = new char[1024];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (count == 0)
                return output.ToString();

            var remaining = ProbeOutputCharacterLimit - output.Length;
            if (remaining > 0)
                output.Append(buffer, 0, Math.Min(count, remaining));
        }
    }

    private static async Task DrainProbeAsync(
        Process process,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            await process.WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
        }
        catch
        {
            // A rejected executable must not delay the next candidate.
        }

        try
        {
            _ = await Task.WhenAll(stdoutTask, stderrTask)
                .WaitAsync(TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
        }
        catch
        {
            // Output is intentionally discarded.
        }
    }

    private static JsonDocument? TryParse(string line)
    {
        try { return JsonDocument.Parse(line); }
        catch (JsonException) { return null; }
    }

    private static bool TryReadId(JsonElement response, out int id)
    {
        id = default;
        if (!response.TryGetProperty("id", out var value))
            return false;
        return value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt32(out id)
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out id);
    }

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new { });

    private static string GetClientVersion()
    {
        var version = typeof(CodexAppServerClient).Assembly.GetName().Version;
        return version is null ? "1.2.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }

    private sealed record CodexCommand(string Path, string Source, string? Version = null);
    private sealed record CodexProbe(bool IsCodexCli, string? Version, string? Failure);
}

internal sealed class CodexCandidateException : Exception
{
    public CodexCandidateException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

internal sealed class CodexClientException : Exception
{
    public CodexClientException(
        string message,
        string? diagnostic = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }

    public string? Diagnostic { get; }
}
