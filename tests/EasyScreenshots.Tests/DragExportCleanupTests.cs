using EasyScreenshots.Core;
using System.Diagnostics;
using Xunit;

namespace EasyScreenshots.Tests;

public sealed class DragExportCleanupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EasyScreenshots-cleanup-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private string MakeFile(string name, DateTimeOffset modified)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "test"); File.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        return path;
    }
    private static string Generated(string id) => $"Screenshot-20261001-120000-{id.PadLeft(32, '0')}.png";

    [Fact] public void DeletesOnlyExpiredGeneratedTopLevelFiles()
    {
        var expired = MakeFile(Generated("1"), _now.AddDays(-31));
        var recent = MakeFile(Generated("2"), _now.AddDays(-2));
        var manual = MakeFile("Screenshot-manually-saved.png", _now.AddDays(-50));
        var malformed = MakeFile("Screenshot-20261001-120000-XYZ.png", _now.AddDays(-50));
        var nested = Path.Combine(_folder, "nested"); Directory.CreateDirectory(nested);
        var child = Path.Combine(nested, Generated("3")); File.WriteAllText(child, "test"); File.SetLastWriteTimeUtc(child, _now.AddDays(-50).UtcDateTime);
        Assert.Equal(new CleanupResult(1, 0), DragExportCleanup.Run(_folder, 30, _now));
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent)); Assert.True(File.Exists(manual)); Assert.True(File.Exists(malformed)); Assert.True(File.Exists(child));
    }

    [Fact] public void RetentionBoundaryIsStrictAndZeroDisablesDeletion()
    {
        var boundary = MakeFile(Generated("1"), _now.AddDays(-30));
        var expired = MakeFile(Generated("2"), _now.AddDays(-31));
        Assert.Equal(new CleanupResult(0, 0), DragExportCleanup.Run(_folder, 0, _now));
        Assert.True(File.Exists(expired));
        Assert.Equal(new CleanupResult(1, 0), DragExportCleanup.Run(_folder, 30, _now));
        Assert.True(File.Exists(boundary));
    }

    [Fact] public void MissingFolderIsNoOp() => Assert.Equal(new CleanupResult(0, 0), DragExportCleanup.Run(_folder, 30, _now));

    [Theory]
    [InlineData(-1)]
    [InlineData(3651)]
    public void InvalidRetentionIsRejected(int retention)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DragExportCleanup.Run(_folder, retention, _now));
        Assert.Throws<ArgumentOutOfRangeException>(() => SettingsStore.Save(Path.Combine(_folder, "settings.json"), new AppSettings(DragExportRetentionDays: retention)));
    }

    [Fact] public void LockedExpiredFileCountsFailureWithoutStoppingOtherCleanup()
    {
        var locked = MakeFile(Generated("1"), _now.AddDays(-40));
        var other = MakeFile(Generated("2"), _now.AddDays(-40));
        using (var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(new CleanupResult(1, 1), DragExportCleanup.Run(_folder, 30, _now));
            Assert.False(File.Exists(other));
        }
        Assert.True(File.Exists(locked));
    }

    [Fact] public void OldSettingsDefaultRetentionAndInvalidLoadedRetentionIsNormalized()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, "{\"CloseToTray\":false,\"StartAtLogin\":true}");
        Assert.Equal(new AppSettings(false, true, 30), SettingsStore.Load(path));
        File.WriteAllText(path, "{\"CloseToTray\":false,\"DragExportRetentionDays\":-9}");
        Assert.Equal(30, SettingsStore.Load(path).DragExportRetentionDays);
        SettingsStore.Save(path, new AppSettings(DragExportRetentionDays: 0));
        Assert.Equal(0, SettingsStore.Load(path).DragExportRetentionDays);
    }

    [Fact] public void ParentJunctionCannotRedirectCleanupIntoAnotherDirectory()
    {
        Directory.CreateDirectory(_folder);
        var target = Path.Combine(_folder, "target");
        var exports = Path.Combine(target, "DragExports");
        var link = Path.Combine(_folder, "linked-parent");
        Directory.CreateDirectory(exports);
        var protectedFile = Path.Combine(exports, Generated("abc"));
        File.WriteAllText(protectedFile, "preserve");
        File.SetLastWriteTimeUtc(protectedFile, _now.AddDays(-50).UtcDateTime);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("/d"); start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J");
                start.ArgumentList.Add(link); start.ArgumentList.Add(target);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
                Assert.True(process.ExitCode == 0, output + error);
            }
            else Directory.CreateSymbolicLink(link, target);
            Assert.Equal(new CleanupResult(0, 0), DragExportCleanup.Run(Path.Combine(link, "DragExports"), 30, _now));
            Assert.True(File.Exists(protectedFile));
            Assert.Equal("preserve", File.ReadAllText(protectedFile));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
