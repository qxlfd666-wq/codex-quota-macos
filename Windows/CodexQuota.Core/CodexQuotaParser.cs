using System.Globalization;
using System.Text.Json;

namespace CodexQuota.Core;

public static class CodexQuotaParser
{
    public static QuotaSnapshot Parse(
        JsonElement accountResult,
        JsonElement rateLimitsResult,
        DateTimeOffset? fetchedAt = null)
    {
        var account = TryGetObject(accountResult, "account");
        var email = GetString(account, "email");

        JsonElement? bucket = null;
        var buckets = TryGetObject(rateLimitsResult, "rateLimitsByLimitId");
        if (buckets is { } bucketsValue)
            bucket = TryGetObject(bucketsValue, "codex");
        bucket ??= TryGetObject(rateLimitsResult, "rateLimits");

        if (bucket is not { } bucketValue)
            throw new QuotaUnavailableException();

        var primary = ParseWindow(bucketValue, "primary");
        var secondary = ParseWindow(bucketValue, "secondary");
        if (primary is null && secondary is null)
            throw new QuotaUnavailableException();

        var used = Math.Max(primary?.UsedPercent ?? 0, secondary?.UsedPercent ?? 0);
        used = Math.Clamp(used, 0, 100);
        var rawPlan = GetString(bucketValue, "planType") ?? GetString(account, "planType");

        return new QuotaSnapshot(
            DisplayName(email),
            email,
            PlanName(rawPlan),
            100 - used,
            used,
            primary,
            secondary,
            fetchedAt ?? DateTimeOffset.Now);
    }

    private static QuotaWindow? ParseWindow(JsonElement parent, string propertyName)
    {
        var value = TryGetObject(parent, propertyName);
        if (value is not { } window || !TryGetInt(window, "usedPercent", out var used))
            return null;

        int? duration = TryGetInt(window, "windowDurationMins", out var minutes)
            ? minutes
            : null;
        var resetsAt = TryGetDouble(window, "resetsAt", out var timestamp)
            ? ParseUnixTimestamp(timestamp)
            : null;
        return new QuotaWindow(Math.Clamp(used, 0, 100), duration, resetsAt);
    }

    private static DateTimeOffset? ParseUnixTimestamp(double timestamp)
    {
        if (!double.IsFinite(timestamp) ||
            timestamp < DateTimeOffset.MinValue.ToUnixTimeSeconds() ||
            timestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            return null;

        return DateTimeOffset.FromUnixTimeSeconds((long)timestamp);
    }

    private static JsonElement? TryGetObject(JsonElement? parent, string propertyName)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty(propertyName, out var result) ||
            result.ValueKind != JsonValueKind.Object)
            return null;
        return result;
    }

    private static string? GetString(JsonElement? parent, string propertyName)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty(propertyName, out var result) ||
            result.ValueKind != JsonValueKind.String)
            return null;
        var text = result.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryGetInt(JsonElement parent, string propertyName, out int value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out var result))
            return false;
        if (result.ValueKind == JsonValueKind.Number)
            return result.TryGetInt32(out value);
        return result.ValueKind == JsonValueKind.String &&
               int.TryParse(result.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDouble(JsonElement parent, string propertyName, out double value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out var result))
            return false;
        if (result.ValueKind == JsonValueKind.Number)
            return result.TryGetDouble(out value);
        return result.ValueKind == JsonValueKind.String &&
               double.TryParse(result.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string DisplayName(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var local = email.Split('@', 2)[0];
            var words = local.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
                return string.Join(' ', words);
        }
        return Environment.UserName is { Length: > 0 } name ? name : "Codex 用户";
    }

    private static string PlanName(string? rawPlan)
    {
        if (string.IsNullOrWhiteSpace(rawPlan))
            return "Codex 套餐";
        return rawPlan.ToLowerInvariant() switch
        {
            "free" => "Codex Free",
            "plus" => "Codex Plus",
            "pro" => "Codex Pro",
            "team" => "Codex Team",
            "business" => "Codex Business",
            "enterprise" => "Codex Enterprise",
            "edu" => "Codex Edu",
            _ => $"Codex {rawPlan}"
        };
    }
}
