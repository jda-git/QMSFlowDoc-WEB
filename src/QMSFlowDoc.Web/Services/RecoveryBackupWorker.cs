using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Web.Services;

/// <summary>
/// Runs the configured daily recovery points while the web application is
/// running as a server service. The set itself is always verified before it is
/// published by RecoverySetService.
/// </summary>
public sealed class RecoveryBackupWorker : BackgroundService
{
    private readonly IRecoveryBackupCoordinator _coordinator;
    private readonly ILogger<RecoveryBackupWorker> _logger;
    private readonly HashSet<string> _completedScheduleKeys = new(StringComparer.Ordinal);

    public RecoveryBackupWorker(IRecoveryBackupCoordinator coordinator, ILogger<RecoveryBackupWorker> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            await RunIfDueAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunIfDueAsync(CancellationToken ct)
    {
        try
        {
            var settings = ServerSettingsService.Load();
            if (!settings.BackupEnabled || string.IsNullOrWhiteSpace(settings.BackupPath))
            {
                return;
            }

            var now = DateTime.Now;
            var scheduledTimes = new[] { settings.BackupTime1 }
                .Concat(settings.BackupTime2Enabled ? new[] { settings.BackupTime2 } : Array.Empty<string>())
                .Where(time => TimeOnly.TryParseExact(time, "HH:mm", out _));

            foreach (var scheduledTime in scheduledTimes)
            {
                var time = TimeOnly.ParseExact(scheduledTime, "HH:mm");
                if (now.Hour != time.Hour || now.Minute != time.Minute)
                {
                    continue;
                }

                var scheduleKey = $"{now:yyyyMMdd}-{scheduledTime}";
                if (_completedScheduleKeys.Contains(scheduleKey))
                {
                    continue;
                }

                await _coordinator.CreateAsync(settings, ct);
                _completedScheduleKeys.Add(scheduleKey);
                TrimScheduleHistory(now);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled recovery backup failed");
        }
    }

    private void TrimScheduleHistory(DateTime now)
    {
        var datePrefix = now.AddDays(-2).ToString("yyyyMMdd");
        _completedScheduleKeys.RemoveWhere(key => string.CompareOrdinal(key[..8], datePrefix) < 0);
    }
}
