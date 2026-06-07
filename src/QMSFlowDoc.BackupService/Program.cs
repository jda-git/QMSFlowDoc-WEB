using QMSFlowDoc.BackupService;
using QMSFlowDoc.BackupService.Services;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "QMSFlowDoc Backup Service";
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<SqlBackupService>();
        services.AddSingleton<FileBackupService>();
        services.AddSingleton<RetentionService>();
        services.AddSingleton<SqlRestoreService>();
        services.AddSingleton<BackupManifestService>();
        services.AddHostedService<Worker>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.AddEventLog(settings =>
        {
            settings.SourceName = "QMSFlowDoc Backup";
            settings.LogName = "Application";
        });
    });

var host = builder.Build();
await host.RunAsync();
