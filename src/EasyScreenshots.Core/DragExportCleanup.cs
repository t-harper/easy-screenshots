using System.Globalization;
using System.Text.RegularExpressions;

namespace EasyScreenshots.Core;

public readonly record struct CleanupResult(int DeletedCount, int FailedCount);

/// <summary>Removes only expired files bearing this application's generated drag-export name.</summary>
public static class DragExportCleanup
{
    private static readonly Regex ExportName = new(@"\AScreenshot-(\d{8}-\d{6})-[0-9a-fA-F]{32}\.png\z", RegexOptions.CultureInvariant);

    public static CleanupResult Run(string directory, int retentionDays, DateTimeOffset now)
    {
        if (retentionDays is < 0 or > 3650) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (retentionDays == 0) return default;
        string[] files;
        try
        {
            var root = new DirectoryInfo(Path.GetFullPath(directory));
            // Check lexical ancestors as well as the export folder itself: a normal
            // child directory inside a junction otherwise looks safe in isolation.
            for (DirectoryInfo? ancestor = root; ancestor is not null; ancestor = ancestor.Parent)
                if ((File.GetAttributes(ancestor.FullName) & FileAttributes.ReparsePoint) != 0) return default;
            var attributes = File.GetAttributes(root.FullName);
            if ((attributes & FileAttributes.Directory) == 0) return default;
            files = Directory.GetFiles(root.FullName, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return default; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(0, 1); }

        var cutoff = now.AddDays(-retentionDays).UtcDateTime;
        var deleted = 0; var failed = 0;
        foreach (var path in files)
        {
            var match = ExportName.Match(Path.GetFileName(path));
            if (!match.Success || !DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) continue;
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                if (File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { /* Already removed elsewhere. */ }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed++; }
        }
        return new(deleted, failed);
    }
}
