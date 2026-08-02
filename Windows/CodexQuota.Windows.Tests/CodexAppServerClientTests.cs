using CodexQuota.Windows.Services;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class CodexAppServerClientTests
{
    [Fact]
    public async Task FallsBackFromAnOldHelperAndReadsQuotaThroughACompatibleHelper()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Codex Quota Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        var scriptPath = Path.Combine(testDirectory, "fake-codex.ps1");
        var oldCommandPath = Path.Combine(testDirectory, "old-codex.cmd");
        var compatibleCommandPath = Path.Combine(testDirectory, "compatible-codex.cmd");

        try
        {
            await File.WriteAllTextAsync(scriptPath, FakeCodexScript);
            await File.WriteAllTextAsync(
                oldCommandPath,
                FakeCommand("incompatible"));
            await File.WriteAllTextAsync(
                compatibleCommandPath,
                FakeCommand("compatible"));

            var client = new CodexAppServerClient(new[] { oldCommandPath, compatibleCommandPath });
            try
            {
                var snapshot = await client.FetchSnapshotAsync();

                Assert.Equal(42, snapshot.RemainingPercent);
                Assert.Equal("Codex Plus", snapshot.PlanName);
                Assert.Contains("codex-cli test-compatible", client.LastDiagnostic);
                Assert.Contains("额度读取成功", client.LastDiagnostic);
                Assert.Contains("额度接口不兼容", client.LastDiagnostic);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{exception}\n\nClient diagnostic:\n{client.LastDiagnostic}");
            }
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReadsAccountWhenRateLimitsResponseArrivesFirst()
    {
        var testDirectory = CreateTestDirectory();
        var commandPath = Path.Combine(testDirectory, "out-of-order-codex.cmd");

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "fake-codex.ps1"),
                FakeCodexScript);
            await File.WriteAllTextAsync(commandPath, FakeCommand("out-of-order"));

            var client = new CodexAppServerClient(new[] { commandPath });
            var snapshot = await client.FetchSnapshotAsync();

            Assert.Equal(42, snapshot.RemainingPercent);
            Assert.Equal("test@example.com", snapshot.Email);
            Assert.Equal("Codex Plus", snapshot.PlanName);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ClosesStandardInputAndLetsAppServerExitGracefully()
    {
        var testDirectory = CreateTestDirectory();
        var commandPath = Path.Combine(testDirectory, "graceful-codex.cmd");
        var exitMarkerPath = Path.Combine(testDirectory, "graceful-exit.txt");

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "fake-codex.ps1"),
                FakeCodexScript);
            await File.WriteAllTextAsync(commandPath, FakeCommand("graceful-exit"));

            var client = new CodexAppServerClient(new[] { commandPath });
            var snapshot = await client.FetchSnapshotAsync();

            Assert.Equal(42, snapshot.RemainingPercent);
            Assert.True(File.Exists(exitMarkerPath), "app-server did not observe EOF and exit cleanly");
            Assert.Equal("clean", await File.ReadAllTextAsync(exitMarkerPath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "Codex Quota Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FakeCommand(string flavor) =>
        $"@echo off\r\n" +
        $"if /I \"%~1\"==\"--version\" (\r\n  echo codex-cli test-{flavor}\r\n  exit /b 0\r\n)\r\n" +
        "if /I not \"%~1\"==\"app-server\" exit /b 2\r\n" +
        $"powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%~dp0fake-codex.ps1\" {flavor}\r\n";

    private const string FakeCodexScript = """
        param([string]$Flavor)
        $ErrorActionPreference = 'Stop'
        $pendingAccount = $false

        while (($line = [Console]::In.ReadLine()) -ne $null) {
          $request = $line | ConvertFrom-Json
          switch ($request.method) {
            'initialize' {
              [Console]::Out.WriteLine('{"id":1,"result":{}}')
            }
            'account/read' {
              if ($Flavor -eq 'out-of-order') {
                $pendingAccount = $true
              } else {
                [Console]::Out.WriteLine('{"id":2,"result":{"account":{"type":"chatgpt","email":"test@example.com","planType":"plus"}}}')
              }
            }
            'account/rateLimits/read' {
              if ($Flavor -eq 'incompatible') {
                [Console]::Out.WriteLine('{"id":3,"error":{"code":-32601,"message":"method not found"}}')
              } elseif ($request.PSObject.Properties.Name -contains 'params') {
                [Console]::Out.WriteLine('{"id":3,"error":{"message":"params must be omitted"}}')
              } elseif ($Flavor -eq 'out-of-order') {
                [Console]::Out.WriteLine('{"id":3,"result":{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":58}}}}}')
                if ($pendingAccount) {
                  [Console]::Out.WriteLine('{"id":2,"result":{"account":{"type":"chatgpt","email":"test@example.com","planType":"plus"}}}')
                  $pendingAccount = $false
                }
              } else {
                [Console]::Out.WriteLine('{"id":3,"result":{"rateLimitsByLimitId":{"codex":{"planType":"plus","primary":{"usedPercent":58}}}}}')
              }
            }
          }
          [Console]::Out.Flush()
        }

        if ($Flavor -eq 'graceful-exit') {
          Start-Sleep -Milliseconds 250
          [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'graceful-exit.txt'), 'clean')
        }
        """;
}
