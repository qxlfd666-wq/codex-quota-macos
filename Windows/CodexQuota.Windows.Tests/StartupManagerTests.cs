using CodexQuota.Windows.Services;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class StartupManagerTests
{
    [Fact]
    public void StartupEntryMustPointToCurrentPortableExecutable()
    {
        var current = Path.Combine(Path.GetTempPath(), "Codex Quota", "CodexQuota.exe");

        Assert.True(StartupManager.IsCommandForExecutable($"\"{current}\"", current));
        Assert.True(StartupManager.IsCommandForExecutable($"\"{current}\" --background-start", current));
        Assert.False(StartupManager.IsCommandForExecutable(
            $"\"{Path.Combine(Path.GetTempPath(), "Old", "CodexQuota.exe")}\"",
            current));
        Assert.False(StartupManager.IsCommandForExecutable("\"unterminated", current));
    }

    [Fact]
    public void LegacyStartupEntryIsMigratedOnlyForCurrentExecutable()
    {
        var current = Path.Combine(Path.GetTempPath(), "Codex Quota", "CodexQuota.exe");

        Assert.True(StartupManager.NeedsBackgroundStartMigration($"\"{current}\"", current));
        Assert.False(StartupManager.NeedsBackgroundStartMigration(
            $"\"{current}\" --background-start",
            current));
        Assert.False(StartupManager.NeedsBackgroundStartMigration(
            $"\"{Path.Combine(Path.GetTempPath(), "Old", "CodexQuota.exe")}\"",
            current));
    }
}
