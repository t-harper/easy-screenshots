using System.Text.Json;

namespace EasyScreenshots.Core;

public sealed record AppSettings(bool CloseToTray = true, bool StartAtLogin = false, int DragExportRetentionDays = 30);

public static class SettingsStore
{
    public static AppSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
            return settings.DragExportRetentionDays is < 0 or > 3650 ? settings with { DragExportRetentionDays = 30 } : settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public static void Save(string path, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.DragExportRetentionDays is < 0 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(settings), "Drag export retention must be between 0 and 3650 days.");
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
