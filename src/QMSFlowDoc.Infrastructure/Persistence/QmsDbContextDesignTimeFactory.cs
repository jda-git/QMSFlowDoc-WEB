using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QMSFlowDoc.Infrastructure.Persistence;

/// <summary>
/// Context factory used exclusively by EF Core tooling. It keeps migrations independent
/// from the web host while following the portable data-directory convention of QMSFlowDoc.
/// </summary>
public sealed class QmsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<QmsDbContext>
{
    public QmsDbContext CreateDbContext(string[] args)
    {
        var dataDirectory = Environment.GetEnvironmentVariable("QMSFLOWDOC_DATA");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QMSFlowDoc");
        }

        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, "qmsflowdoc_web.db");
        var options = new DbContextOptionsBuilder<QmsDbContext>()
            .UseSqlite($"Data Source={databasePath}", sqlite => sqlite.MigrationsAssembly(typeof(QmsDbContext).Assembly.GetName().Name))
            .Options;

        return new QmsDbContext(options);
    }
}
