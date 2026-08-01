namespace CodexQuota.Core;

public static class CodexExecutableDiscovery
{
    public static IReadOnlyList<string> DesktopHelperCandidates(string desktopExecutable)
    {
        if (string.IsNullOrWhiteSpace(desktopExecutable))
            return Array.Empty<string>();

        var directory = Path.GetDirectoryName(desktopExecutable);
        if (string.IsNullOrWhiteSpace(directory))
            return Array.Empty<string>();

        return new[]
            {
                Path.Combine(directory, "resources", "codex.exe"),
                Path.Combine(directory, "app", "resources", "codex.exe")
            }
            .Where(path => !string.Equals(path, desktopExecutable, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
