using System.Drawing;
using System.Text.Json;

namespace CodexQuota.Windows.Services;

internal sealed class AppSettingsStore
{
    private readonly string _settingsPath;

    public AppSettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexQuota");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return AppSettings.Default;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath));
            return settings is null || !TryParseColor(settings.BadgeColor, out _)
                ? AppSettings.Default
                : settings;
        }
        catch (Exception)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var temporaryPath = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    public static bool TryParseColor(string value, out Color color)
    {
        color = Color.Empty;
        if (value.Length != 7 || value[0] != '#' ||
            !int.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return false;
        color = Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }

    public static string SerializeColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

internal sealed record AppSettings(string BadgeColor)
{
    public static AppSettings Default { get; } = new("#FF3B30");
}
