using Microsoft.Win32;

namespace CodexQuota.Windows.Services;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexQuota";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value &&
                   IsCommandForExecutable(value, CurrentExecutablePath);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(
                ValueName,
                $"\"{CurrentExecutablePath}\" --background-start",
                RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static void MigrateLegacyEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is not string command ||
            !NeedsBackgroundStartMigration(command, CurrentExecutablePath))
            return;

        key.SetValue(
            ValueName,
            $"\"{CurrentExecutablePath}\" --background-start",
            RegistryValueKind.String);
    }

    internal static bool NeedsBackgroundStartMigration(string? command, string executablePath) =>
        IsCommandForExecutable(command, executablePath) &&
        command?.Contains("--background-start", StringComparison.OrdinalIgnoreCase) == false;

    internal static bool IsCommandForExecutable(string? command, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(executablePath))
            return false;

        var trimmed = command.Trim();
        string configuredPath;
        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote < 0)
                return false;
            configuredPath = trimmed[1..closingQuote];
        }
        else
        {
            configuredPath = trimmed.Split(' ', 2)[0];
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(configuredPath),
                Path.GetFullPath(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string CurrentExecutablePath =>
        Environment.ProcessPath ?? Application.ExecutablePath;
}
