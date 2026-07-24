using QMSFlowDoc.Shared.Services;

if (args.Length == 0 || args[0] is "--help" or "-h" or "/?")
{
    ShowUsage();
    return 1;
}

try
{
    var command = args[0].ToLowerInvariant();
    var options = ParseOptions(args.Skip(1));
    var integrityKey = LoadIntegrityKey();
    if (!options.TryGetValue("set", out var recoverySetPath))
    {
        throw new ArgumentException("Debe indicar --set <ruta-del-conjunto-de-recuperacion>.");
    }

    if (command == "verify")
    {
        var verification = await RecoverySetService.VerifyAsync(recoverySetPath, integrityKey);
        if (!verification.IsValid)
        {
            Console.Error.WriteLine($"CONJUNTO NO VÁLIDO: {verification.Error}");
            return 2;
        }

        Console.WriteLine($"CONJUNTO VÁLIDO: {verification.Manifest!.RecoverySetId}");
        Console.WriteLine($"Base de datos: {verification.Manifest.DatabaseSizeBytes:N0} bytes");
        Console.WriteLine($"Documentos: {verification.Manifest.Documents.Count}");
        return 0;
    }

    if (command == "restore")
    {
        if (!options.TryGetValue("database", out var databasePath) ||
            !options.TryGetValue("documents", out var documentsPath) ||
            !options.TryGetValue("operator", out var operatorName) ||
            !options.TryGetValue("confirm", out var confirmation) ||
            !string.Equals(confirmation, "RESTORE", StringComparison.Ordinal))
        {
            throw new ArgumentException("Para restaurar debe indicar --database, --documents, --operator y --confirm RESTORE.");
        }

        Console.WriteLine("Antes de continuar, detenga la aplicación web para evitar ficheros bloqueados.");
        var restore = await RecoverySetService.RestoreAsync(recoverySetPath, databasePath, documentsPath, operatorName, integrityKey);
        if (!restore.Succeeded)
        {
            Console.Error.WriteLine($"RESTAURACIÓN FALLIDA: {restore.Error}");
            Console.Error.WriteLine($"Informe: {restore.ReportPath}");
            return 3;
        }

        Console.WriteLine("RESTAURACIÓN COMPLETADA.");
        Console.WriteLine($"Informe: {restore.ReportPath}");
        Console.WriteLine($"Base de datos anterior: {restore.SafetyDatabasePath ?? "no existía"}");
        Console.WriteLine($"Documentos anteriores: {restore.SafetyDocumentPath ?? "no existían"}");
        return 0;
    }

    throw new ArgumentException($"Comando no reconocido: {command}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> arguments)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using var enumerator = arguments.GetEnumerator();
    while (enumerator.MoveNext())
    {
        var key = enumerator.Current;
        if (!key.StartsWith("--", StringComparison.Ordinal) || !enumerator.MoveNext())
        {
            throw new ArgumentException($"Opción no válida: {key}");
        }
        options[key[2..]] = enumerator.Current;
    }
    return options;
}

static void ShowUsage()
{
    Console.WriteLine("QMSFlowDoc.RecoveryTool");
    Console.WriteLine("  verify --set <conjunto>");
    Console.WriteLine("  restore --set <conjunto> --database <qmsflowdoc.db> --documents <repositorio-documental> --operator <nombre> --confirm RESTORE");
}

static byte[]? LoadIntegrityKey()
{
    var keyPath = Environment.GetEnvironmentVariable("AuditIntegrity__KeyPath");
    if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath)) return null;
    var encoded = File.ReadLines(keyPath)
        .Select(line => line.Trim())
        .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
    return string.IsNullOrWhiteSpace(encoded) ? null : Convert.FromBase64String(encoded);
}
