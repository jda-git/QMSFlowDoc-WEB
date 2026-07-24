namespace QMSFlowDoc.Web.Services;

/// <summary>
/// Keeps the portable development defaults working without allowing the web
/// host and its maintenance services to resolve the same configured path
/// differently.
/// </summary>
public static class PortablePathResolver
{
    public static string Resolve(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        if (configuredPath.Contains(@"C:\Users\SERVIDOR", StringComparison.OrdinalIgnoreCase))
        {
            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var subPath = configuredPath.Replace(@"C:\Users\SERVIDOR\Documents\", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(@"C:\Users\SERVIDOR\", string.Empty, StringComparison.OrdinalIgnoreCase);
            return Path.Combine(myDocuments, "QMS", subPath);
        }

        return configuredPath;
    }
}
