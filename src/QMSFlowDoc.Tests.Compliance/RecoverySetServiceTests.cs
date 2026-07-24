using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.Services;
using System.Text.Json;

namespace QMSFlowDoc.Tests.Compliance;

public class RecoverySetServiceTests
{
    [Fact]
    public async Task RecoverySet_CreatesAndRestoresDatabaseAndDocumentsTogether()
    {
        var root = CreateTempDirectory();
        try
        {
            var sourceDatabase = Path.Combine(root, "source", "qms.db");
            var sourceDocuments = Path.Combine(root, "source-documents");
            Directory.CreateDirectory(sourceDocuments);
            await CreateDatabaseAsync(sourceDatabase, "approved-record");
            await File.WriteAllTextAsync(Path.Combine(sourceDocuments, "procedure.pdf"), "controlled PDF content");

            var recoverySet = await RecoverySetService.CreateAsync(
                $"Data Source={sourceDatabase}",
                sourceDocuments,
                Path.Combine(root, "backups"));

            var verification = await RecoverySetService.VerifyAsync(recoverySet);
            Assert.True(verification.IsValid, verification.Error);
            Assert.Single(verification.Manifest!.Documents);

            var targetDatabase = Path.Combine(root, "target", "qms.db");
            var targetDocuments = Path.Combine(root, "target-documents");
            Directory.CreateDirectory(targetDocuments);
            await CreateDatabaseAsync(targetDatabase, "obsolete-record");
            await File.WriteAllTextAsync(Path.Combine(targetDocuments, "obsolete.pdf"), "obsolete");

            var restore = await RecoverySetService.RestoreAsync(recoverySet, targetDatabase, targetDocuments, "test-operator");

            Assert.True(restore.Succeeded, restore.Error);
            Assert.Equal("approved-record", await ReadDatabaseValueAsync(targetDatabase));
            Assert.Equal("controlled PDF content", await File.ReadAllTextAsync(Path.Combine(targetDocuments, "procedure.pdf")));
            Assert.False(File.Exists(Path.Combine(targetDocuments, "obsolete.pdf")));
            Assert.True(File.Exists(restore.SafetyDatabasePath));
            Assert.True(Directory.Exists(restore.SafetyDocumentPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RecoverySet_RejectsTamperedDocument()
    {
        var root = CreateTempDirectory();
        try
        {
            var sourceDatabase = Path.Combine(root, "source", "qms.db");
            var sourceDocuments = Path.Combine(root, "source-documents");
            Directory.CreateDirectory(sourceDocuments);
            await CreateDatabaseAsync(sourceDatabase, "approved-record");
            await File.WriteAllTextAsync(Path.Combine(sourceDocuments, "procedure.pdf"), "original");

            var recoverySet = await RecoverySetService.CreateAsync(
                $"Data Source={sourceDatabase}",
                sourceDocuments,
                Path.Combine(root, "backups"));
            await File.WriteAllTextAsync(Path.Combine(recoverySet, "documents", "procedure.pdf"), "tampered");

            var verification = await RecoverySetService.VerifyAsync(recoverySet);

            Assert.False(verification.IsValid);
            Assert.Contains("no coincide", verification.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Retention_OnlyDeletesPublishedSetsBeyondMinimumCopies()
    {
        var root = CreateTempDirectory();
        try
        {
            var sourceDatabase = Path.Combine(root, "source", "qms.db");
            var sourceDocuments = Path.Combine(root, "source-documents");
            var backupRoot = Path.Combine(root, "backups");
            Directory.CreateDirectory(sourceDocuments);
            await CreateDatabaseAsync(sourceDatabase, "approved-record");
            await File.WriteAllTextAsync(Path.Combine(sourceDocuments, "procedure.pdf"), "content");

            var sets = new List<string>();
            for (var index = 0; index < 4; index++)
            {
                sets.Add(await RecoverySetService.CreateAsync($"Data Source={sourceDatabase}", sourceDocuments, backupRoot));
            }

            foreach (var set in sets)
            {
                var manifestPath = Path.Combine(set, RecoverySetService.ManifestFileName);
                var manifest = JsonSerializer.Deserialize<RecoverySetManifest>(await File.ReadAllTextAsync(manifestPath))!;
                manifest.CreatedAtUtc = DateTime.UtcNow.AddDays(-10);
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
            }

            Directory.CreateDirectory(Path.Combine(backupRoot, ".in-progress-operator-review"));
            var deleted = await RecoverySetRetentionService.ApplyAsync(backupRoot, retentionDays: 1, minimumCopies: 3);

            Assert.Equal(1, deleted);
            Assert.Equal(3, Directory.EnumerateDirectories(backupRoot).Count(path => !Path.GetFileName(path).StartsWith('.')));
            Assert.True(Directory.Exists(Path.Combine(backupRoot, ".in-progress-operator-review")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CreateDatabaseAsync(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Records (Value TEXT NOT NULL); DELETE FROM Records; INSERT INTO Records (Value) VALUES ($value);";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadDatabaseValueAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Records LIMIT 1;";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "qms-recovery-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
