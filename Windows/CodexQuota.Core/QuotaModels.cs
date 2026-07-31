namespace CodexQuota.Core;

public sealed record QuotaWindow(
    int UsedPercent,
    int? WindowDurationMinutes,
    DateTimeOffset? ResetsAt)
{
    public int RemainingPercent => 100 - Math.Clamp(UsedPercent, 0, 100);
}

public sealed record QuotaSnapshot(
    string DisplayName,
    string? Email,
    string PlanName,
    int RemainingPercent,
    int UsedPercent,
    QuotaWindow? Primary,
    QuotaWindow? Secondary,
    DateTimeOffset FetchedAt);

public sealed class QuotaUnavailableException : Exception
{
    public QuotaUnavailableException()
        : base("当前登录方式没有可显示的套餐额度。请使用 ChatGPT 账户登录 Codex。")
    {
    }
}
