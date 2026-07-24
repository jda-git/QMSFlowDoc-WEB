using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;

namespace QMSFlowDoc.Infrastructure.Auditing;

public sealed class AuditChainVerificationResult
{
    public bool IsValid { get; init; }
    public int VerifiedEntries { get; init; }
    public int UnprotectedLegacyEntries { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Independently recomputes the append-only audit hash chain. Version 1 audit
/// rows remain verifiable; version 2 additionally protects their snapshots.
/// </summary>
public static class AuditChainIntegrityService
{
    public static async Task<AuditChainVerificationResult> VerifyAsync(
        QmsDbContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var logs = await context.AuditLogs
            .AsNoTracking()
            .OrderBy(log => log.Timestamp)
            .ThenBy(log => log.Id)
            .ToListAsync(ct);

        var previousHash = string.Empty;
        var verifiedEntries = 0;
        var unprotectedLegacyEntries = 0;
        var chainStarted = false;

        foreach (var log in logs)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(log.IntegrityHash))
            {
                unprotectedLegacyEntries++;
                if (chainStarted)
                {
                    return Invalid("Se ha detectado un registro de auditoría sin hash dentro de una cadena ya iniciada.", verifiedEntries, unprotectedLegacyEntries);
                }

                continue;
            }

            chainStarted = true;
            var payload = log.IntegrityVersion >= 2
                ? AuditLog.BuildPayloadV2(previousHash, log)
                : AuditLog.BuildPayload(previousHash, log);
            var expectedHash = ComputeSha256(payload);
            if (!string.Equals(expectedHash, log.IntegrityHash, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid($"La cadena de auditoría no coincide en el registro {log.Id}.", verifiedEntries, unprotectedLegacyEntries);
            }

            previousHash = log.IntegrityHash;
            verifiedEntries++;
        }

        return new AuditChainVerificationResult
        {
            IsValid = true,
            VerifiedEntries = verifiedEntries,
            UnprotectedLegacyEntries = unprotectedLegacyEntries
        };
    }

    private static AuditChainVerificationResult Invalid(string error, int verifiedEntries, int unprotectedLegacyEntries) => new()
    {
        IsValid = false,
        Error = error,
        VerifiedEntries = verifiedEntries,
        UnprotectedLegacyEntries = unprotectedLegacyEntries
    };

    private static string ComputeSha256(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
