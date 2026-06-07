using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Shared.Services;
using System.Text.Json;

namespace QMSFlowDoc.Domain.Entities;

/// <summary>
/// Server-side settings stored in serversettings.json on the server machine.
/// Used by both ServerConfigurator and BackupService.
/// </summary>
public class ServerSettings
{
    // ── SQL Server ──
    public string SqlServerInstance { get; set; } = @".\SQLEXPRESS";
    public string DatabaseName { get; set; } = "QMSFlowDoc";
    public bool UseIntegratedSecurity { get; set; } = true;
    public string? SqlUsername { get; set; }
    public string? SqlPassword { get; set; }

    // ── Document Repository ──
    public string DocumentRepositoryPath { get; set; } = string.Empty;

    // ── Backup ──
    public string BackupPath { get; set; } = string.Empty;
    public bool BackupEnabled { get; set; } = true;

    /// <summary>First daily backup time (HH:mm format).</summary>
    public string BackupTime1 { get; set; } = "02:00";

    /// <summary>Enable a second daily backup.</summary>
    public bool BackupTime2Enabled { get; set; } = false;

    /// <summary>Second daily backup time (HH:mm format).</summary>
    public string BackupTime2 { get; set; } = "14:00";

    /// <summary>Number of days to retain backups.</summary>
    public int BackupRetentionDays { get; set; } = 30;

    /// <summary>True = full copy of document folder. False = incremental (future).</summary>
    public bool BackupDocumentsFull { get; set; } = true;

    /// <summary>Minimum number of backup copies to retain regardless of age. Prevents accidental deletion of all backups.</summary>
    public int BackupMinimumCopies { get; set; } = 3;

    // ── Logging ──
    public string LogPath { get; set; } = string.Empty;

    /// <summary>Builds SQL Server connection string from these settings.</summary>
    public string BuildConnectionString()
    {
        var server = SqlServerInstance;
        var builder = new System.Text.StringBuilder();
        builder.Append($"Server={server};Database={DatabaseName};");
        if (UseIntegratedSecurity)
        {
            builder.Append("Trusted_Connection=True;");
        }
        else
        {
            var decryptedPwd = CredentialProtection.DecryptPassword(SqlPassword ?? string.Empty);
            builder.Append($"ApplicationUser Id={SqlUsername};Password={decryptedPwd};");
        }
        builder.Append("TrustServerCertificate=True;MultipleActiveResultSets=True;");
        return builder.ToString();
    }
}

/// <summary>
/// Service to load/save ServerSettings from serversettings.json.
/// </summary>
public static class ServerSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Default path for server settings file.</summary>
    public static string DefaultSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "QMSFlowDoc", "serversettings.json");

    public static ServerSettings Load(string? path = null)
    {
        var filePath = path ?? DefaultSettingsPath;
        if (!File.Exists(filePath))
            return new ServerSettings();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ServerSettings>(json, JsonOptions) ?? new ServerSettings();
        }
        catch
        {
            return new ServerSettings();
        }
    }

    public static void Save(ServerSettings settings, string? path = null)
    {
        var filePath = path ?? DefaultSettingsPath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Encrypt SQL password before saving if not already encrypted
        if (!string.IsNullOrEmpty(settings.SqlPassword) &&
            !CredentialProtection.IsEncrypted(settings.SqlPassword))
        {
            try
            {
                settings.SqlPassword = CredentialProtection.EncryptPassword(settings.SqlPassword);
            }
            catch
            {
                // If DPAPI fails (e.g., non-Windows), save as-is
            }
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}
