using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.Services;

/// <summary>
/// Creates, verifies and restores a complete recovery point made of a SQLite
/// snapshot and its managed document repository. This code deliberately has no
/// dependency on the web host, EF Core or Identity so it can also be used by the
/// standalone recovery executable.
/// </summary>
public static class RecoverySetService
{
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ExcludedDocumentRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Base_datos",
        "DataProtection-Keys",
        "Temp",
        "RecoverySets"
    };

    public static async Task<string> CreateAsync(
        string sourceConnectionString,
        string documentRepositoryPath,
        string recoveryRootPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConnectionString);
        EnsureExistingDirectory(documentRepositoryPath, nameof(documentRepositoryPath));
        Directory.CreateDirectory(recoveryRootPath);

        var setId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var stagingPath = Path.Combine(recoveryRootPath, $".in-progress-{setId}");
        var finalPath = Path.Combine(recoveryRootPath, $"{timestamp}_{setId}");

        if (Directory.Exists(stagingPath) || Directory.Exists(finalPath))
        {
            throw new IOException("Ya existe un conjunto de recuperación con el identificador generado.");
        }

        Directory.CreateDirectory(stagingPath);
        try
        {
            var databasePath = Path.Combine(stagingPath, "database.db");
            await CreateSqliteSnapshotAsync(sourceConnectionString, databasePath, ct);
            await VerifySqliteIntegrityAsync(databasePath, ct);

            var documentsPath = Path.Combine(stagingPath, "documents");
            var documents = await CopyDocumentsAsync(documentRepositoryPath, documentsPath, ct);

            var manifest = new RecoverySetManifest
            {
                RecoverySetId = setId,
                CreatedAtUtc = DateTime.UtcNow,
                DatabaseSha256 = await ComputeSha256Async(databasePath, ct),
                DatabaseSizeBytes = new FileInfo(databasePath).Length,
                DatabaseIntegrityPassed = true,
                Documents = documents,
                DocumentManifestSha256 = ComputeDocumentManifestHash(documents),
                Status = "OK"
            };

            await WriteManifestAsync(stagingPath, manifest, ct);
            var verification = await VerifyAsync(stagingPath, ct);
            if (!verification.IsValid)
            {
                throw new InvalidOperationException($"El conjunto de recuperación creado no supera la verificación: {verification.Error}");
            }

            Directory.Move(stagingPath, finalPath);
            return finalPath;
        }
        catch
        {
            TryDeleteDirectory(stagingPath);
            throw;
        }
    }

    public static async Task<RecoverySetVerificationResult> VerifyAsync(string recoverySetPath, CancellationToken ct = default)
    {
        try
        {
            var manifest = await ReadManifestAsync(recoverySetPath, ct);
            if (!string.Equals(manifest.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("El conjunto de recuperación no está marcado como correcto.", manifest);
            }

            var databasePath = SafePath(recoverySetPath, manifest.DatabaseFileName);
            if (!File.Exists(databasePath))
            {
                return Invalid("No se encuentra la copia de la base de datos.", manifest);
            }

            if (!string.Equals(await ComputeSha256Async(databasePath, ct), manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("El hash de la base de datos no coincide con el manifiesto.", manifest);
            }

            await VerifySqliteIntegrityAsync(databasePath, ct);

            var documentsPath = SafePath(recoverySetPath, manifest.DocumentDirectoryName);
            if (!Directory.Exists(documentsPath))
            {
                return Invalid("No se encuentra la copia del repositorio documental.", manifest);
            }

            if (!string.Equals(ComputeDocumentManifestHash(manifest.Documents), manifest.DocumentManifestSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("El manifiesto documental ha sido alterado.", manifest);
            }

            foreach (var document in manifest.Documents)
            {
                var documentPath = SafePath(documentsPath, document.RelativePath);
                if (!File.Exists(documentPath))
                {
                    return Invalid($"Falta el documento '{document.RelativePath}'.", manifest);
                }

                var info = new FileInfo(documentPath);
                if (info.Length != document.SizeBytes ||
                    !string.Equals(await ComputeSha256Async(documentPath, ct), document.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Invalid($"El documento '{document.RelativePath}' no coincide con el manifiesto.", manifest);
                }
            }

            return new RecoverySetVerificationResult { IsValid = true, Manifest = manifest };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or JsonException or InvalidOperationException)
        {
            return Invalid(ex.Message);
        }
    }

    public static async Task<RecoveryRestoreResult> RestoreAsync(
        string recoverySetPath,
        string targetDatabasePath,
        string targetDocumentRepositoryPath,
        string operatorName,
        CancellationToken ct = default)
    {
        var result = new RecoveryRestoreResult();
        var verification = await VerifyAsync(recoverySetPath, ct);
        if (!verification.IsValid || verification.Manifest == null)
        {
            result.Error = verification.Error ?? "El conjunto de recuperación no es válido.";
            return result;
        }

        var manifest = verification.Manifest;
        var targetDatabaseFullPath = Path.GetFullPath(targetDatabasePath);
        var targetDocumentsFullPath = Path.GetFullPath(targetDocumentRepositoryPath);
        var databaseDirectory = Path.GetDirectoryName(targetDatabaseFullPath)
            ?? throw new InvalidOperationException("La ruta de la base de datos no tiene directorio padre.");
        var documentsParent = Path.GetDirectoryName(targetDocumentsFullPath)
            ?? throw new InvalidOperationException("La ruta documental no tiene directorio padre.");
        Directory.CreateDirectory(databaseDirectory);
        Directory.CreateDirectory(documentsParent);

        var restoreId = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var stagedDatabasePath = Path.Combine(databaseDirectory, $".{Path.GetFileName(targetDatabaseFullPath)}.restore-{restoreId}");
        var stagedDocumentPath = Path.Combine(documentsParent, $".{Path.GetFileName(targetDocumentsFullPath)}.restore-{restoreId}");
        var safetyDatabasePath = Path.Combine(databaseDirectory, $"{Path.GetFileNameWithoutExtension(targetDatabaseFullPath)}_before_restore_{restoreId}{Path.GetExtension(targetDatabaseFullPath)}");
        var safetyDocumentPath = Path.Combine(documentsParent, $"{Path.GetFileName(targetDocumentsFullPath)}_before_restore_{restoreId}");

        try
        {
            var sourceDatabasePath = SafePath(recoverySetPath, manifest.DatabaseFileName);
            var sourceDocumentsPath = SafePath(recoverySetPath, manifest.DocumentDirectoryName);
            await CopyFileAsync(sourceDatabasePath, stagedDatabasePath, ct);
            await VerifySqliteIntegrityAsync(stagedDatabasePath, ct);
            await CopyDirectoryAsync(sourceDocumentsPath, stagedDocumentPath, ct);
            await VerifyDocumentDirectoryAsync(stagedDocumentPath, manifest.Documents, ct);

            MoveFileIfExists(targetDatabaseFullPath, safetyDatabasePath);
            MoveFileIfExists(targetDatabaseFullPath + "-wal", safetyDatabasePath + "-wal");
            MoveFileIfExists(targetDatabaseFullPath + "-shm", safetyDatabasePath + "-shm");
            if (Directory.Exists(targetDocumentsFullPath))
            {
                Directory.Move(targetDocumentsFullPath, safetyDocumentPath);
            }

            File.Move(stagedDatabasePath, targetDatabaseFullPath);
            Directory.Move(stagedDocumentPath, targetDocumentsFullPath);
            await VerifySqliteIntegrityAsync(targetDatabaseFullPath, ct);
            await VerifyDocumentDirectoryAsync(targetDocumentsFullPath, manifest.Documents, ct);

            result.Succeeded = true;
            result.SafetyDatabasePath = File.Exists(safetyDatabasePath) ? safetyDatabasePath : null;
            result.SafetyDocumentPath = Directory.Exists(safetyDocumentPath) ? safetyDocumentPath : null;
            result.ReportPath = await WriteRestoreReportAsync(targetDatabaseFullPath, new
            {
                Status = "OK",
                RestoredAtUtc = DateTime.UtcNow,
                Operator = operatorName,
                RecoverySetPath = Path.GetFullPath(recoverySetPath),
                manifest.RecoverySetId,
                TargetDatabasePath = targetDatabaseFullPath,
                TargetDocumentRepositoryPath = targetDocumentsFullPath,
                result.SafetyDatabasePath,
                result.SafetyDocumentPath
            }, ct);
            return result;
        }
        catch (Exception ex)
        {
            TryRollback(targetDatabaseFullPath, targetDocumentsFullPath, safetyDatabasePath, safetyDocumentPath);
            result.Error = ex.Message;
            result.ReportPath = await WriteRestoreReportAsync(targetDatabaseFullPath, new
            {
                Status = "FAILED",
                RestoredAtUtc = DateTime.UtcNow,
                Operator = operatorName,
                RecoverySetPath = Path.GetFullPath(recoverySetPath),
                manifest.RecoverySetId,
                Error = ex.Message,
                SafetyDatabasePath = File.Exists(safetyDatabasePath) ? safetyDatabasePath : null,
                SafetyDocumentPath = Directory.Exists(safetyDocumentPath) ? safetyDocumentPath : null
            }, ct);
            return result;
        }
        finally
        {
            TryDeleteFile(stagedDatabasePath);
            TryDeleteDirectory(stagedDocumentPath);
        }
    }

    private static async Task CreateSqliteSnapshotAsync(string sourceConnectionString, string destinationPath, CancellationToken ct)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder(sourceConnectionString)
        {
            Pooling = false
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Pooling = false
        };
        await using var source = new SqliteConnection(sourceBuilder.ToString());
        await source.OpenAsync(ct);
        await using var destination = new SqliteConnection(destinationBuilder.ToString());
        await destination.OpenAsync(ct);
        source.BackupDatabase(destination);

        await using var checkpoint = destination.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await checkpoint.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<RecoverySetFileEntry>> CopyDocumentsAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        var files = new List<RecoverySetFileEntry>();
        await CopyDirectoryAsync(sourcePath, destinationPath, ct, files, sourcePath);
        return files.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToList();
    }

    private static async Task CopyDirectoryAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken ct,
        List<RecoverySetFileEntry>? manifestEntries = null,
        string? manifestRoot = null)
    {
        Directory.CreateDirectory(destinationPath);
        var directory = new DirectoryInfo(sourcePath);
        foreach (var file in directory.EnumerateFiles())
        {
            ct.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(destinationPath, file.Name);
            await CopyFileAsync(file.FullName, targetFile, ct);
            if (manifestEntries != null && manifestRoot != null)
            {
                manifestEntries.Add(new RecoverySetFileEntry
                {
                    RelativePath = Path.GetRelativePath(manifestRoot, file.FullName).Replace('\\', '/'),
                    SizeBytes = new FileInfo(targetFile).Length,
                    Sha256 = await ComputeSha256Async(targetFile, ct)
                });
            }
        }

        foreach (var subdirectory in directory.EnumerateDirectories())
        {
            if (subdirectory.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                (string.Equals(sourcePath, manifestRoot, StringComparison.OrdinalIgnoreCase) && ExcludedDocumentRootDirectories.Contains(subdirectory.Name)))
            {
                continue;
            }

            await CopyDirectoryAsync(subdirectory.FullName, Path.Combine(destinationPath, subdirectory.Name), ct, manifestEntries, manifestRoot);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new IOException("Ruta de destino no válida."));
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await source.CopyToAsync(destination, ct);
        await destination.FlushAsync(ct);
    }

    private static async Task VerifyDocumentDirectoryAsync(string rootPath, IReadOnlyCollection<RecoverySetFileEntry> documents, CancellationToken ct)
    {
        foreach (var document in documents)
        {
            var path = SafePath(rootPath, document.RelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Falta el documento restaurado '{document.RelativePath}'.", path);
            }

            var info = new FileInfo(path);
            if (info.Length != document.SizeBytes ||
                !string.Equals(await ComputeSha256Async(path, ct), document.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"El documento restaurado '{document.RelativePath}' no es íntegro.");
            }
        }
    }

    private static async Task VerifySqliteIntegrityAsync(string databasePath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var value = (await command.ExecuteScalarAsync(ct))?.ToString();
        if (!string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La base de datos SQLite no supera PRAGMA integrity_check.");
        }
    }

    private static async Task<RecoverySetManifest> ReadManifestAsync(string recoverySetPath, CancellationToken ct)
    {
        var manifestPath = SafePath(recoverySetPath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("No se encuentra manifest.json en el conjunto de recuperación.", manifestPath);
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        return JsonSerializer.Deserialize<RecoverySetManifest>(json, JsonOptions)
            ?? throw new JsonException("El manifiesto de recuperación no contiene datos válidos.");
    }

    private static async Task WriteManifestAsync(string recoverySetPath, RecoverySetManifest manifest, CancellationToken ct)
    {
        var path = Path.Combine(recoverySetPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeDocumentManifestHash(IReadOnlyCollection<RecoverySetFileEntry> documents)
    {
        var payload = string.Join('\n', documents
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .Select(entry => $"{entry.RelativePath}|{entry.SizeBytes}|{entry.Sha256}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string SafePath(string rootPath, string relativePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El manifiesto contiene una ruta fuera del conjunto de recuperación.");
        }
        return candidate;
    }

    private static async Task<string> WriteRestoreReportAsync(string targetDatabasePath, object report, CancellationToken ct)
    {
        var directory = Path.Combine(Path.GetDirectoryName(targetDatabasePath) ?? AppContext.BaseDirectory, "RecoveryReports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"restore_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, JsonOptions), ct);
        return path;
    }

    private static void TryRollback(string targetDatabasePath, string targetDocumentsPath, string safetyDatabasePath, string safetyDocumentsPath)
    {
        try
        {
            if (File.Exists(safetyDatabasePath))
            {
                TryDeleteFile(targetDatabasePath);
                File.Move(safetyDatabasePath, targetDatabasePath);
            }
            if (File.Exists(safetyDatabasePath + "-wal"))
            {
                TryDeleteFile(targetDatabasePath + "-wal");
                File.Move(safetyDatabasePath + "-wal", targetDatabasePath + "-wal");
            }
            if (File.Exists(safetyDatabasePath + "-shm"))
            {
                TryDeleteFile(targetDatabasePath + "-shm");
                File.Move(safetyDatabasePath + "-shm", targetDatabasePath + "-shm");
            }
            if (Directory.Exists(safetyDocumentsPath))
            {
                TryDeleteDirectory(targetDocumentsPath);
                Directory.Move(safetyDocumentsPath, targetDocumentsPath);
            }
        }
        catch
        {
            // The safety copies are deliberately retained for manual recovery.
        }
    }

    private static void MoveFileIfExists(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private static void EnsureExistingDirectory(string path, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"No se encuentra el directorio '{argumentName}': {path}");
        }
    }

    private static RecoverySetVerificationResult Invalid(string error, RecoverySetManifest? manifest = null) =>
        new() { IsValid = false, Error = error, Manifest = manifest };

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch { }
    }
}
