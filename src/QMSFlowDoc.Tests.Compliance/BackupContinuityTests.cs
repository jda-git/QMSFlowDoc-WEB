using QMSFlowDoc.Shared.Services;

namespace QMSFlowDoc.Tests.Compliance;

public class BackupContinuityTests
{
    [Fact]
    public async Task FileBackupManifest_RecordsEveryFileWithStableHash()
    {
        var root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs", "sub"));
            await File.WriteAllTextAsync(Path.Combine(root, "docs", "procedure.txt"), "approved procedure");
            await File.WriteAllTextAsync(Path.Combine(root, "docs", "sub", "record.txt"), "quality record");

            var entries = await FileBackupManifestService.BuildManifestAsync(Path.Combine(root, "docs"));
            var manifestPath = await FileBackupManifestService.WriteManifestAsync(Path.Combine(root, "docs"), entries);

            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Sha256)));
            Assert.True(File.Exists(manifestPath));
            Assert.True(await FileBackupManifestService.VerifyManifestAsync(Path.Combine(root, "docs"), entries));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileBackupManifest_DetectsTamperedFile()
    {
        var root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(root);
            var file = Path.Combine(root, "controlled-document.txt");
            await File.WriteAllTextAsync(file, "original");

            var entries = await FileBackupManifestService.BuildManifestAsync(root);
            await File.WriteAllTextAsync(file, "changed after backup");

            Assert.False(await FileBackupManifestService.VerifyManifestAsync(root, entries));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "qms-backup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
