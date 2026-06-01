namespace QMSFlowDoc.Shared.Validation;

/// <summary>
/// Password policy validator for ISO 15189 compliance
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 30;

    public static (bool IsValid, string? ErrorMessage) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "La contraseña es requerida.");
        
        if (password.Length < MinLength)
            return (false, $"La contraseña debe tener al menos {MinLength} caracteres.");
        
        if (!password.Any(char.IsUpper))
            return (false, "La contraseña debe contener al menos una letra mayúscula.");

        if (!password.Any(char.IsLower))
            return (false, "La contraseña debe contener al menos una letra minúscula.");
        
        if (!password.Any(char.IsDigit))
            return (false, "La contraseña debe contener al menos un número.");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            return (false, "La contraseña debe contener al menos un carácter especial.");

        return (true, null);
    }

    public static string GetPolicyDescription()
    {
        return $"Mínimo {MinLength} caracteres, al menos 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial.";
    }
}
