using QMSFlowDoc.Application.Services.Environmental;
using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Tests.Compliance;

public class EnvironmentalMonthlyReportTests
{
    [Fact]
    public void BriefExcursionFromCentral_IsIncludedAsOneEpisodeAndItsDeclaredDuration()
    {
        const string content = "02/06/2026;09:00:03;NEVERA;6.9;NC\n" +
                               "02/06/2026;09:14:30;CENTRAL;NEVERA: EXCURSION_BREVE_NEVERA max 7.8 C durante 14 min\n" +
                               "02/06/2026;10:00:03;NEVERA;5.1;NC";

        var readings = EnvironmentalLogParser.Parse(content, "batch", "tester");
        var report = EnvironmentalMonthlyReportBuilder.Build(readings);
        var refrigerator = Assert.Single(report.Statistics, s => s.Source == EnvironmentalSource.REFRIGERATOR);

        Assert.Equal(TimeSpan.FromMinutes(14), refrigerator.TotalOutOfRange);
        Assert.Equal(1, refrigerator.OutOfRangeEpisodes);
        Assert.NotNull(refrigerator.MeanKineticTemperature);
        Assert.Single(report.CentralEvents);
        Assert.NotNull(EnvironmentalCentralEventParser.ParseBriefExcursion(report.CentralEvents[0]));
    }

    [Fact]
    public void MeasuredLongExcursion_UsesIntervalUntilTheNextReading_AndKeepsNotifications()
    {
        const string content = "18/06/2026;21:00:03;NEVERA;4.8;-20.1\n" +
                               "18/06/2026;22:00:03;NEVERA;10.6;-20.2\n" +
                               "18/06/2026;22:21:44;CENTRAL;ALARMA_RECIBIDA_NEVERA\n" +
                               "18/06/2026;22:21:45;CENTRAL;CORREO_ENVIADO\n" +
                               "18/06/2026;22:22:03;CENTRAL;TELEGRAM_ENVIADO\n" +
                               "18/06/2026;23:00:03;NEVERA;4.9;-20.0";

        var readings = EnvironmentalLogParser.Parse(content, "batch", "tester");
        var report = EnvironmentalMonthlyReportBuilder.Build(readings);
        var refrigerator = Assert.Single(report.Statistics, s => s.Source == EnvironmentalSource.REFRIGERATOR);

        Assert.Equal(TimeSpan.FromHours(1), refrigerator.TotalOutOfRange);
        Assert.Equal(1, refrigerator.OutOfRangeEpisodes);
        Assert.Equal(3, report.CentralEvents.Count);
        Assert.Contains(report.CentralEvents, r => r.Event == "CORREO_ENVIADO");
        Assert.Contains(report.CentralEvents, r => r.Event == "TELEGRAM_ENVIADO");
    }
}
