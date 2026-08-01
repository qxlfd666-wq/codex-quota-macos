using System.Text.Json;
using CodexQuota.Core;
using Xunit;

namespace CodexQuota.Core.Tests;

public sealed class CodexQuotaParserTests
{
    [Fact]
    public void RateLimitsRequestOmitsParamsForSchemaCompatibility()
    {
        using var request = JsonDocument.Parse(CodexAppServerMessages.RateLimitsRead(3));

        Assert.Equal("account/rateLimits/read", request.RootElement.GetProperty("method").GetString());
        Assert.Equal(3, request.RootElement.GetProperty("id").GetInt32());
        Assert.False(request.RootElement.TryGetProperty("params", out _));
    }

    [Fact]
    public void DesktopHelperCandidatesNeverReturnTheGuiExecutable()
    {
        var appDirectory = Path.Combine(Path.GetTempPath(), "CodexDesktop", "app");
        var guiExecutable = Path.Combine(appDirectory, "Codex.exe");

        var candidates = CodexExecutableDiscovery.DesktopHelperCandidates(guiExecutable);

        Assert.DoesNotContain(
            candidates,
            candidate => string.Equals(candidate, guiExecutable, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Path.Combine(appDirectory, "resources", "codex.exe"), candidates[0]);
    }

    [Fact]
    public void UsesCodexBucketAndStrictestWindow()
    {
        using var account = JsonDocument.Parse("""
            {"account":{"type":"chatgpt","email":"lin.test@example.com","planType":"plus"}}
            """);
        using var limits = JsonDocument.Parse("""
            {
              "rateLimits":{"planType":"free","primary":{"usedPercent":99}},
              "rateLimitsByLimitId":{
                "codex_other":{"primary":{"usedPercent":100}},
                "codex":{
                  "planType":"pro",
                  "primary":{"usedPercent":35,"windowDurationMins":300,"resetsAt":1800000000},
                  "secondary":{"usedPercent":82,"windowDurationMins":10080,"resetsAt":1900000000}
                }
              }
            }
            """);

        var snapshot = CodexQuotaParser.Parse(
            account.RootElement,
            limits.RootElement,
            DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        Assert.Equal("lin test", snapshot.DisplayName);
        Assert.Equal("lin.test@example.com", snapshot.Email);
        Assert.Equal("Codex Pro", snapshot.PlanName);
        Assert.Equal(82, snapshot.UsedPercent);
        Assert.Equal(18, snapshot.RemainingPercent);
        Assert.Equal(65, snapshot.Primary?.RemainingPercent);
        Assert.Equal(18, snapshot.Secondary?.RemainingPercent);
    }

    [Fact]
    public void FallsBackToLegacyBucketAndAcceptsNumericStrings()
    {
        using var account = JsonDocument.Parse("{}");
        using var limits = JsonDocument.Parse("""
            {"rateLimits":{"planType":"pro","primary":{"usedPercent":"73"}}}
            """);

        var snapshot = CodexQuotaParser.Parse(account.RootElement, limits.RootElement);

        Assert.Equal(27, snapshot.RemainingPercent);
        Assert.Equal("Codex Pro", snapshot.PlanName);
    }

    [Fact]
    public void RejectsMissingQuotaWindow()
    {
        using var account = JsonDocument.Parse("{\"account\":{\"type\":\"apiKey\"}}");
        using var limits = JsonDocument.Parse("{\"rateLimits\":{\"limitId\":\"codex\"}}");

        Assert.Throws<QuotaUnavailableException>(() =>
            CodexQuotaParser.Parse(account.RootElement, limits.RootElement));
    }
}
