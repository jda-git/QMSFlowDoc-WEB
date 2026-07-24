using System;

namespace QMSFlowDoc.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // e.g., "CREATE", "UPDATE", "REVOKE"
    public string EntityType { get; set; } = string.Empty; // e.g., "StaffTraining", "Competency"
    public Guid? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? Reason { get; set; } // Required for updates/revocations
    public string? BeforeSnapshot { get; set; } // JSON
    public string? AfterSnapshot { get; set; } // JSON
    public string? IntegrityHash { get; set; } // SHA256 of event
    /// <summary>
    /// Version 1 hashes the original audit payload. Version 2 also binds the
    /// before/after snapshots to the audit event.
    /// </summary>
    public int IntegrityVersion { get; set; } = 2;
    public string? Result { get; set; } // OK/FAIL
    public string MachineName { get; set; } = Environment.MachineName;

    public static string GetNormalizedTimestampString(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
        return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string BuildPayload(string lastHash, AuditLog log)
    {
        var timestampStr = GetNormalizedTimestampString(log.Timestamp);
        return $"{lastHash}|{log.Id}|{timestampStr}|{log.UserId}|{log.UserName}|{log.Action}|{log.EntityType}|{log.EntityId}|{log.Details}|{log.Reason}|{log.Result}|{log.MachineName}";
    }

    public static string BuildPayloadV2(string lastHash, AuditLog log)
    {
        var timestampStr = GetNormalizedTimestampString(log.Timestamp);
        return $"v2|{lastHash}|{log.Id}|{timestampStr}|{log.UserId}|{log.UserName}|{log.Action}|{log.EntityType}|{log.EntityId}|{log.Details}|{log.Reason}|{log.Result}|{log.MachineName}|{log.BeforeSnapshot}|{log.AfterSnapshot}";
    }
}
