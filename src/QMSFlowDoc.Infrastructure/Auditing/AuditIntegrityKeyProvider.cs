using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace QMSFlowDoc.Infrastructure.Auditing;

public interface IAuditIntegrityKeyProvider
{
    byte[] PrimaryKey { get; }
    IReadOnlyList<byte[]> VerificationKeys { get; }
}

/// <summary>Loads Base64 HMAC keys from a file kept outside the database and recovery sets.</summary>
public sealed class AuditIntegrityKeyProvider : IAuditIntegrityKeyProvider
{
    public byte[] PrimaryKey { get; }
    public IReadOnlyList<byte[]> VerificationKeys { get; }

    public AuditIntegrityKeyProvider(IConfiguration configuration)
    {
        var configuredPath = configuration["AuditIntegrity:KeyPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("Falta AuditIntegrity:KeyPath. Configure una clave HMAC externa antes de iniciar QMSFlowDoc.");
        }

        var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("No se encuentra el archivo de clave HMAC de auditoría.", path);
        }

        var keys = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => Convert.FromBase64String(line))
            .Where(key => key.Length >= 32)
            .ToList();
        if (keys.Count == 0)
        {
            throw new InvalidOperationException("El archivo de clave HMAC no contiene una clave Base64 de al menos 256 bits.");
        }

        PrimaryKey = keys[0];
        VerificationKeys = keys;
    }
}
