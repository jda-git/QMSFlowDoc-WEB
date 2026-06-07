using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.Services;

namespace QMSFlowDoc.Web.Services;

public static class PendingRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string PendingRestorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "QMSFlowDoc",
            "pending_restore.json");

    public static string LastRestoreStatusPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "QMSFlowDoc",
            "last_restore_status.json");

    public static async Task ScheduleRestoreAsync(PendingRestoreRequest request, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(PendingRestorePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(request, JsonOptions);
        await File.WriteAllTextAsync(PendingRestorePath, json, ct);
    }

    public static async Task ApplyPendingRestoreAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        if (!File.Exists(PendingRestorePath))
        {
            return;
        }

        PendingRestoreRequest? request = null;
        string? safetyDbPath = null;
        string? safetyDocumentPath = null;
        var appliedAt = DateTime.Now;

        try
        {
            var json = await File.ReadAllTextAsync(PendingRestorePath, ct);
            request = JsonSerializer.Deserialize<PendingRestoreRequest>(json, JsonOptions)
                ?? throw new InvalidOperationException("La solicitud de restauracion pendiente no es valida.");

            var targetDatabasePath = GetSqliteDatabasePath(configuration);
            if (!string.Equals(Path.GetFullPath(targetDatabasePath), Path.GetFullPath(request.TargetDatabasePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La base de datos activa no coincide con la ruta validada al preparar la restauracion.");
            }

            await VerifyPendingRestoreAsync(request, ct);

            var safetySuffix = appliedAt.ToString("yyyy-MM-dd_HH-mm-ss");
            safetyDbPath = Path.Combine(
                Path.GetDirectoryName(targetDatabasePath) ?? AppContext.BaseDirectory,
                $"{Path.GetFileNameWithoutExtension(targetDatabasePath)}_before_restore_{safetySuffix}{Path.GetExtension(targetDatabasePath)}");

            Directory.CreateDirectory(Path.GetDirectoryName(targetDatabasePath) ?? AppContext.BaseDirectory);
            await DeleteIfExistsAsync($"{targetDatabasePath}-wal", ct);
            await DeleteIfExistsAsync($"{targetDatabasePath}-shm", ct);

            if (File.Exists(targetDatabasePath))
            {
                await CopyFileWithRetriesAsync(targetDatabasePath, safetyDbPath, ct);
            }

            await CopyFileWithRetriesAsync(request.DatabaseBackupPath, targetDatabasePath, ct);

            if (!string.IsNullOrWhiteSpace(request.DocumentBackupPath) &&
                !string.IsNullOrWhiteSpace(request.TargetDocumentPath))
            {
                var targetDocumentPath = request.TargetDocumentPath;
                var parent = Path.GetDirectoryName(targetDocumentPath) ?? AppContext.BaseDirectory;
                safetyDocumentPath = Path.Combine(parent, $"{Path.GetFileName(targetDocumentPath)}_documents_before_restore_{safetySuffix}");
                RestoreDocumentRepository(request.DocumentBackupPath, targetDocumentPath, safetyDocumentPath);
            }

            await WriteRestoreStatusAsync(new RestoreStatus
            {
                Status = "OK",
                AppliedAt = appliedAt,
                RequestedAt = request.RequestedAt,
                RequestedBy = request.RequestedBy,
                DatabaseBackupPath = request.DatabaseBackupPath,
                DocumentBackupPath = request.DocumentBackupPath,
                TargetDatabasePath = request.TargetDatabasePath,
                TargetDocumentPath = request.TargetDocumentPath,
                SafetyDatabasePath = safetyDbPath,
                SafetyDocumentPath = safetyDocumentPath
            }, ct);

            ArchivePendingRequest("applied", appliedAt);
        }
        catch (Exception ex)
        {
            await TryRollbackAsync(request, safetyDbPath, safetyDocumentPath, ct);
            await WriteRestoreStatusAsync(new RestoreStatus
            {
                Status = "FAILED",
                AppliedAt = appliedAt,
                RequestedAt = request?.RequestedAt,
                RequestedBy = request?.RequestedBy,
                DatabaseBackupPath = request?.DatabaseBackupPath,
                DocumentBackupPath = request?.DocumentBackupPath,
                TargetDatabasePath = request?.TargetDatabasePath,
                TargetDocumentPath = request?.TargetDocumentPath,
                SafetyDatabasePath = safetyDbPath,
                SafetyDocumentPath = safetyDocumentPath,
                ErrorMessage = ex.Message
            }, ct);

            ArchivePendingRequest("failed", appliedAt);
            Console.Error.WriteLine($"QMSFlowDoc pending restore failed: {ex}");
        }
    }

    public static string GetSqliteDatabasePath(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se ha encontrado la cadena de conexion DefaultConnection.");

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La base de datos SQLite configurada no apunta a un archivo restaurable.");
        }

        return Path.IsPathRooted(builder.DataSource)
            ? builder.DataSource
            : Path.GetFullPath(builder.DataSource);
    }

    public static async Task VerifyPendingRestoreAsync(PendingRestoreRequest request, CancellationToken ct = default)
    {
        if (!File.Exists(request.DatabaseBackupPath))
        {
            throw new FileNotFoundException("No se encuentra la copia de base de datos seleccionada.", request.DatabaseBackupPath);
        }

        if (!string.IsNullOrWhiteSpace(request.DatabaseSha256))
        {
            var actualSha = await ComputeSha256Async(request.DatabaseBackupPath, ct);
            if (!string.Equals(actualSha, request.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La integridad SHA-256 de la base de datos seleccionada no coincide con el manifiesto.");
            }
        }

        await VerifySqliteIntegrityWithRetriesAsync(request.DatabaseBackupPath, ct);

        if (!string.IsNullOrWhiteSpace(request.DocumentBackupPath))
        {
            if (!Directory.Exists(request.DocumentBackupPath))
            {
                throw new DirectoryNotFoundException($"No se encuentra la copia documental seleccionada: {request.DocumentBackupPath}");
            }

            if (!string.IsNullOrWhiteSpace(request.DocumentManifestPath) && File.Exists(request.DocumentManifestPath))
            {
                var json = await File.ReadAllTextAsync(request.DocumentManifestPath, ct);
                var manifest = JsonSerializer.Deserialize<List<FileBackupManifestEntry>>(json, JsonOptions) ?? new();
                if (!await FileBackupManifestService.VerifyManifestAsync(request.DocumentBackupPath, manifest, ct))
                {
                    throw new InvalidOperationException("La copia documental seleccionada no coincide con su manifiesto de integridad.");
                }
            }
        }
    }

    private static async Task TryRollbackAsync(PendingRestoreRequest? request, string? safetyDbPath, string? safetyDocumentPath, CancellationToken ct)
    {
        if (request == null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(safetyDbPath) && File.Exists(safetyDbPath))
            {
                await DeleteIfExistsAsync($"{request.TargetDatabasePath}-wal", ct);
                await DeleteIfExistsAsync($"{request.TargetDatabasePath}-shm", ct);
                await CopyFileWithRetriesAsync(safetyDbPath, request.TargetDatabasePath, ct);
            }

            if (!string.IsNullOrWhiteSpace(safetyDocumentPath) &&
                Directory.Exists(safetyDocumentPath) &&
                !string.IsNullOrWhiteSpace(request.TargetDocumentPath))
            {
                RestoreDocumentSafetySnapshot(safetyDocumentPath, request.TargetDocumentPath);
            }
        }
        catch
        {
            // The failed restore status keeps the safety paths for manual recovery.
        }
    }

    private static async Task DeleteIfExistsAsync(string path, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(150 * attempt, ct);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                await Task.Delay(150 * attempt, ct);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite: true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
        }
    }

    private static void RestoreDocumentRepository(string backupRootPath, string targetRootPath, string safetyRootPath)
    {
        var backupRoot = new DirectoryInfo(backupRootPath);
        if (!backupRoot.Exists)
        {
            return;
        }

        Directory.CreateDirectory(targetRootPath);
        Directory.CreateDirectory(safetyRootPath);

        foreach (var sourceDir in backupRoot.GetDirectories())
        {
            if (ShouldSkipRootRestoreItem(sourceDir.Name))
            {
                continue;
            }

            var targetDir = Path.Combine(targetRootPath, sourceDir.Name);
            var safetyDir = Path.Combine(safetyRootPath, sourceDir.Name);
            if (Directory.Exists(targetDir))
            {
                if (Directory.Exists(safetyDir))
                {
                    Directory.Delete(safetyDir, recursive: true);
                }

                Directory.Move(targetDir, safetyDir);
            }

            CopyDirectory(sourceDir.FullName, targetDir);
        }

        foreach (var sourceFile in backupRoot.GetFiles())
        {
            if (ShouldSkipRootRestoreItem(sourceFile.Name))
            {
                continue;
            }

            var targetFile = Path.Combine(targetRootPath, sourceFile.Name);
            var safetyFile = Path.Combine(safetyRootPath, sourceFile.Name);
            if (File.Exists(targetFile))
            {
                File.Move(targetFile, safetyFile, overwrite: true);
            }

            sourceFile.CopyTo(targetFile, overwrite: true);
        }
    }

    private static void RestoreDocumentSafetySnapshot(string safetyRootPath, string targetRootPath)
    {
        var safetyRoot = new DirectoryInfo(safetyRootPath);
        if (!safetyRoot.Exists)
        {
            return;
        }

        Directory.CreateDirectory(targetRootPath);

        foreach (var safetyDir in safetyRoot.GetDirectories())
        {
            var targetDir = Path.Combine(targetRootPath, safetyDir.Name);
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
            }

            Directory.Move(safetyDir.FullName, targetDir);
        }

        foreach (var safetyFile in safetyRoot.GetFiles())
        {
            var targetFile = Path.Combine(targetRootPath, safetyFile.Name);
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            File.Move(safetyFile.FullName, targetFile);
        }
    }

    private static bool ShouldSkipRootRestoreItem(string name)
    {
        return name.Equals("Base_datos", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DataProtection-Keys", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Temp", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 81920,
                    useAsync: true);
                var hash = await sha.ComputeHashAsync(stream, ct);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch (IOException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
            catch (UnauthorizedAccessException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }

        await using var finalStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920,
            useAsync: true);
        var finalHash = await sha.ComputeHashAsync(finalStream, ct);
        return Convert.ToHexString(finalHash).ToLowerInvariant();
    }

    private static async Task VerifySqliteIntegrityWithRetriesAsync(string databasePath, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                var builder = new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Private
                };

                await using var conn = new SqliteConnection(builder.ToString());
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = (await cmd.ExecuteScalarAsync(ct))?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("La copia SQLite seleccionada no supera PRAGMA integrity_check.");
                }

                return;
            }
            catch (SqliteException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
            catch (IOException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
            catch (UnauthorizedAccessException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }

        throw new IOException($"No se pudo abrir la copia SQLite para verificarla: {databasePath}");
    }

    private static async Task CopyFileWithRetriesAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                await using var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 81920,
                    useAsync: true);

                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await source.CopyToAsync(destination, ct);
                return;
            }
            catch (IOException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
            catch (UnauthorizedAccessException) when (attempt < 6)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static async Task WriteRestoreStatusAsync(RestoreStatus status, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(LastRestoreStatusPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(status, JsonOptions);
        await File.WriteAllTextAsync(LastRestoreStatusPath, json, ct);
    }

    private static void ArchivePendingRequest(string outcome, DateTime timestamp)
    {
        if (!File.Exists(PendingRestorePath))
        {
            return;
        }

        var archivePath = Path.Combine(
            Path.GetDirectoryName(PendingRestorePath) ?? AppContext.BaseDirectory,
            $"pending_restore_{outcome}_{timestamp:yyyy-MM-dd_HH-mm-ss}.json");

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(PendingRestorePath, archivePath);
    }
}

public class PendingRestoreRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public string RequestedBy { get; set; } = string.Empty;
    public string DatabaseBackupPath { get; set; } = string.Empty;
    public string? DatabaseSha256 { get; set; }
    public string? DocumentBackupPath { get; set; }
    public string? DocumentManifestPath { get; set; }
    public string TargetDatabasePath { get; set; } = string.Empty;
    public string? TargetDocumentPath { get; set; }
}

public class RestoreStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public DateTime? RequestedAt { get; set; }
    public string? RequestedBy { get; set; }
    public string? DatabaseBackupPath { get; set; }
    public string? DocumentBackupPath { get; set; }
    public string? TargetDatabasePath { get; set; }
    public string? TargetDocumentPath { get; set; }
    public string? SafetyDatabasePath { get; set; }
    public string? SafetyDocumentPath { get; set; }
    public string? ErrorMessage { get; set; }
}
