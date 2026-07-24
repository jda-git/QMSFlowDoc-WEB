namespace QMSFlowDoc.Web.Services;

/// <summary>Applies Windows EFS to a verified recovery set before it is retained.</summary>
public static class RecoverySetEncryptionService
{
    public static void EncryptWithEfs(string recoverySetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("El cifrado de copias requiere un proveedor de cifrado configurado para esta plataforma.");
        }

        foreach (var file in Directory.EnumerateFiles(recoverySetPath, "*", SearchOption.AllDirectories))
        {
            File.Encrypt(file);
        }
    }
}
