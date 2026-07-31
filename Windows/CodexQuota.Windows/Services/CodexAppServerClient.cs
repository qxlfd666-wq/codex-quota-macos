using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodexQuota.Core;

namespace CodexQuota.Windows.Services;

internal sealed class CodexAppServerClient
{
    private const int InitializeRequestId = 1;
    private const int AccountRequestId = 2;
    private const int RateLimitsRequestId = 3;

    public async Task<QuotaSnapshot> FetchSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var command = LocateCodexCommand();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        using var process = new Process { StartInfo = CreateStartInfo(command) };
        try
        {
            if (!process.Start())
                throw new CodexClientException("无法启动本机 Codex。");
        }
        catch (Exception exception) when (exception is not CodexClientException)
        {
            throw new CodexClientException($"无法启动本机 Codex：{exception.Message}", exception);
        }

        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            var initializeRequest =
                $"{{\"method\":\"initialize\",\"id\":{InitializeRequestId},\"params\":{{\"clientInfo\":{{\"name\":\"codex_quota_windows\",\"title\":\"Codex Quota\",\"version\":\"1.0.0\"}}}}}}\n";
            await process.StandardInput.WriteAsync(initializeRequest.AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);
            await WaitForSuccessfulResponseAsync(
                process.StandardOutput,
                InitializeRequestId,
                "Codex 初始化失败",
                timeout.Token);

            var request =
                "{\"method\":\"initialized\",\"params\":{}}\n" +
                $"{{\"method\":\"account/read\",\"id\":{AccountRequestId},\"params\":{{\"refreshToken\":false}}}}\n" +
                $"{{\"method\":\"account/rateLimits/read\",\"id\":{RateLimitsRequestId},\"params\":{{}}}}\n";
            await process.StandardInput.WriteAsync(request.AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);

            JsonElement? accountResult = null;
            JsonElement? rateLimitsResult = null;
            while (!timeout.IsCancellationRequested && rateLimitsResult is null)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null)
                    break;

                using var document = TryParse(line);
                if (document is null || document.RootElement.ValueKind != JsonValueKind.Object ||
                    !TryReadId(document.RootElement, out var id))
                    continue;

                if (document.RootElement.TryGetProperty("error", out var error) && id == RateLimitsRequestId)
                {
                    var message = error.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : null;
                    throw new CodexClientException($"Codex 无法读取额度：{message ?? "未知错误"}");
                }

                if (!document.RootElement.TryGetProperty("result", out var result) ||
                    result.ValueKind != JsonValueKind.Object)
                    continue;
                if (id == AccountRequestId)
                    accountResult = result.Clone();
                else if (id == RateLimitsRequestId)
                    rateLimitsResult = result.Clone();
            }

            if (rateLimitsResult is not { } rateLimits)
            {
                var diagnostic = process.HasExited ? await stderrTask : string.Empty;
                diagnostic = diagnostic.Trim();
                throw new CodexClientException(diagnostic.Length > 0
                    ? $"Codex 未返回额度：{diagnostic[..Math.Min(diagnostic.Length, 240)]}"
                    : "Codex 返回了无法识别的额度数据。");
            }

            return CodexQuotaParser.Parse(accountResult ?? EmptyObject(), rateLimits);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CodexClientException("读取额度超时，请检查网络后重试。");
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The short-lived app-server may already have exited.
            }

            try
            {
                using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(exitTimeout.Token);
            }
            catch
            {
                // Do not hold up the tray app if process cleanup races with exit.
            }

            try
            {
                _ = await stderrTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // stderr is diagnostic-only and may close after the process handle.
            }
        }
    }

    private static async Task WaitForSuccessfulResponseAsync(
        StreamReader output,
        int expectedId,
        string errorPrefix,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await output.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new CodexClientException($"{errorPrefix}：Codex 提前退出。");

            using var document = TryParse(line);
            if (document is null || document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadId(document.RootElement, out var id) || id != expectedId)
                continue;

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.ValueKind == JsonValueKind.Object &&
                              error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;
                throw new CodexClientException($"{errorPrefix}：{message ?? "未知错误"}");
            }

            if (document.RootElement.TryGetProperty("result", out _))
                return;

            throw new CodexClientException($"{errorPrefix}：Codex 返回了无法识别的数据。");
        }
    }

    private static ProcessStartInfo CreateStartInfo(CodexCommand command)
    {
        ProcessStartInfo startInfo;
        if (command.Path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            command.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo = new ProcessStartInfo(commandProcessor)
            {
                Arguments = $"/d /s /c \"\"{command.Path}\" app-server\""
            };
        }
        else
        {
            startInfo = new ProcessStartInfo(command.Path);
            startInfo.ArgumentList.Add("app-server");
        }

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardInputEncoding = new UTF8Encoding(false);
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        return startInfo;
    }

    private static CodexCommand LocateCodexCommand()
    {
        var candidates = new List<string>();
        AddIfPresent(candidates, Environment.GetEnvironmentVariable("CODEX_QUOTA_CODEX_PATH"));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        // The Codex desktop app extracts its bundled helper here on Windows.
        candidates.Add(Path.Combine(local, "OpenAI", "Codex", "bin", "codex.exe"));
        AddRunningDesktopAppCandidates(candidates);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Add(Path.Combine(directory.Trim('"'), "codex.exe"));
            candidates.Add(Path.Combine(directory.Trim('"'), "codex.cmd"));
        }

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.AddRange(new[]
        {
            Path.Combine(roaming, "npm", "codex.cmd"),
            Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
            Path.Combine(local, "Programs", "Codex", "resources", "codex.exe"),
            Path.Combine(local, "Programs", "Codex", "codex.exe"),
            Path.Combine(local, "Programs", "ChatGPT", "resources", "codex.exe"),
            Path.Combine(home, ".local", "bin", "codex.exe")
        });

        var selected = candidates
            .Select(Environment.ExpandEnvironmentVariables)
            .FirstOrDefault(File.Exists);
        return selected is not null
            ? new CodexCommand(Path.GetFullPath(Environment.ExpandEnvironmentVariables(selected)))
            : throw new CodexClientException(
                "未找到 Codex。请先打开并登录 ChatGPT/Codex，或安装 Codex CLI；也可以设置 CODEX_QUOTA_CODEX_PATH。");
    }

    private static void AddIfPresent(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value.Trim().Trim('"'));
    }

    private static void AddRunningDesktopAppCandidates(List<string> candidates)
    {
        foreach (var processName in new[] { "ChatGPT", "Codex" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var executable = process.MainModule?.FileName;
                        var directory = executable is null ? null : Path.GetDirectoryName(executable);
                        if (directory is null)
                            continue;

                        candidates.Add(Path.Combine(directory, "codex.exe"));
                        candidates.Add(Path.Combine(directory, "resources", "codex.exe"));
                        candidates.Add(Path.Combine(directory, "app", "resources", "codex.exe"));
                    }
                    catch
                    {
                        // Store package ACLs can hide the executable from other processes.
                    }
                }
            }
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

    private sealed record CodexCommand(string Path);
}

internal sealed class CodexClientException : Exception
{
    public CodexClientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
