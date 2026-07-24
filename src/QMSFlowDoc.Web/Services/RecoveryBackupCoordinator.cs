using System.Text.Json;
using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.Services;
using QMSFlowDoc.Infrastructure.Auditing;

namespace QMSFlowDoc.Web.Services;

public interface IRecoveryBackupCoordinator
{
    Task<RecoveryBackupRunResult> CreateAsync(ServerSettings settings, CancellationToken ct = default);
}

public sealed class RecoveryBackupRunResult
{
    public string RecoverySetPath { get; init; } = string.Empty;
    public DateTime CompletedAtUtc { get; init; }
}

/// <summary>
/// Serializes creation of complete recovery sets from the running application.
/// Restoration remains deliberately outside the web host in RecoveryTool.
/// </summary>
public sealed class RecoveryBackupCoordinator : IRecoveryBackupCoordinator
{
    private static readonly SemaphoreSlim BackupLock = new(1, 1);
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecoveryBackupCoordinator> _logger;
    private readonly IAuditIntegrityKeyProvider _keyProvider;

    public RecoveryBackupCoordinator(IConfiguration configuration, ILogger<RecoveryBackupCoordinator> logger, IAuditIntegrityKeyProvider keyProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _keyProvider = keyProvider;
    }

    public async Task<RecoveryBackupRunResult> CreateAsync(ServerSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.BackupPath))
        {
            throw new InvalidOperationException("Debe configurarse una ruta de copias de seguridad.");
        }

        var documentRepositoryPath = ResolveDocumentRepositoryPath(settings);
        if (!Directory.Exists(documentRepositoryPath))
        {
            throw new DirectoryNotFoundException($"No se encuentra el repositorio documental configurado: {documentRepositoryPath}");
        }

        var backupPath = Path.GetFullPath(settings.BackupPath);
        if (IsSameOrDescendantPath(backupPath, documentRepositoryPath))
        {
            throw new InvalidOperationException("La ruta de copias debe estar fuera del repositorio documental para evitar que una copia se incluya a sí misma.");
        }

        var recoveryRootPath = Path.Combine(backupPath, "RecoverySets");
        var databasePath = PendingRestoreService.GetSqliteDatabasePath(_configuration);
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            DefaultTimeout = 10,
            ForeignKeys = true,
            Pooling = true
        }.ToString();

        await BackupLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Creating complete recovery set in {RecoveryRootPath}", recoveryRootPath);
            var recoverySetPath = await RecoverySetService.CreateAsync(
                sourceConnectionString,
                documentRepositoryPath,
                recoveryRootPath,
                _keyProvider.PrimaryKey,
                ct);

            var result = new RecoveryBackupRunResult
            {
                RecoverySetPath = recoverySetPath,
                CompletedAtUtc = DateTime.UtcNow
            };

            var deletedSetCount = await RecoverySetRetentionService.ApplyAsync(
                recoveryRootPath,
                settings.BackupRetentionDays,
                settings.BackupMinimumCopies,
                ct);
            await WriteStatusAsync(backupPath, result, null, ct);
            if (deletedSetCount > 0)
            {
                _logger.LogInformation("Deleted {DeletedSetCount} expired recovery set(s)", deletedSetCount);
            }
            _logger.LogInformation("Recovery set verified successfully at {RecoverySetPath}", recoverySetPath);
            return result;
        }
        catch (Exception ex)
        {
            try
            {
                await WriteStatusAsync(backupPath, null, ex.Message, ct);
            }
            catch (Exception statusException)
            {
                _logger.LogError(statusException, "Unable to write recovery backup failure status");
            }

            _logger.LogError(ex, "Complete recovery backup failed");
            throw;
        }
        finally
        {
            BackupLock.Release();
        }
    }

    private string ResolveDocumentRepositoryPath(ServerSettings settings)
    {
        var configuredPath = _configuration["DocumentStorage:RootPath"];
        var path = !string.IsNullOrWhiteSpace(configuredPath)
            ? PortablePathResolver.Resolve(configuredPath)
            : PortablePathResolver.Resolve(settings.DocumentRepositoryPath);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("No se ha configurado el repositorio documental.");
        }

        return Path.GetFullPath(path);
    }

    private static bool IsSameOrDescendantPath(string candidatePath, string parentPath)
    {
        var relative = Path.GetRelativePath(parentPath, candidatePath);
        return string.Equals(relative, ".", StringComparison.Ordinal) ||
            (!relative.Equals("..", StringComparison.Ordinal) &&
             !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !Path.IsPathRooted(relative));
    }

    private static async Task WriteStatusAsync(
        string backupPath,
        RecoveryBackupRunResult? result,
        string? error,
        CancellationToken ct)
    {
        Directory.CreateDirectory(backupPath);
        var statusPath = Path.Combine(backupPath, "last_recovery_backup_status.json");
        var status = new
        {
            CompletedAtUtc = result?.CompletedAtUtc ?? DateTime.UtcNow,
            Status = result is null ? "FAILED" : "OK",
            RecoverySetPath = result?.RecoverySetPath,
            Error = error
        };
        await File.WriteAllTextAsync(
            statusPath,
            JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }
}
