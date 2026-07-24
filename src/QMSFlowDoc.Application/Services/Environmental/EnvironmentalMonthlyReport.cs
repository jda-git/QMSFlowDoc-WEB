using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Environmental;

public sealed record EnvironmentalSourceStatistics(
    EnvironmentalSource Source, int Readings, int OutOfRange, int DisconnectedSensors,
    decimal? Minimum, decimal? Maximum, decimal? Average, decimal? MinimumHumidity,
    decimal? MaximumHumidity, decimal? AverageHumidity);

public sealed record EnvironmentalMonthlyReport(
    DateTime PeriodStart, DateTime PeriodEnd, IReadOnlyList<EnvironmentalSourceStatistics> Statistics,
    IReadOnlyList<EnvironmentalReading> Incidents, IReadOnlyList<EnvironmentalReading> CentralEvents);

public static class EnvironmentalMonthlyReportBuilder
{
    public static EnvironmentalMonthlyReport Build(IEnumerable<EnvironmentalReading> readings)
    {
        var all = readings.OrderBy(r => r.RecordedAt).ToList();
        if (all.Count == 0) throw new InvalidOperationException("No hay lecturas para generar el informe.");
        var measured = all.Where(r => r.Source != EnvironmentalSource.CENTRAL).GroupBy(r => r.Source)
            .Select(group => new EnvironmentalSourceStatistics(
                group.Key, group.Count(), group.Count(r => r.IsOutOfRange), group.Count(r => r.SensorNotConnected),
                Min(group.Select(r => r.TemperatureCelsius)), Max(group.Select(r => r.TemperatureCelsius)), Average(group.Select(r => r.TemperatureCelsius)),
                Min(group.Select(r => r.HumidityPercent)), Max(group.Select(r => r.HumidityPercent)), Average(group.Select(r => r.HumidityPercent))))
            .ToList();
        return new EnvironmentalMonthlyReport(all.First().RecordedAt, all.Last().RecordedAt, measured,
            all.Where(r => r.IsOutOfRange).ToList(), all.Where(r => r.Source == EnvironmentalSource.CENTRAL).ToList());
    }

    private static decimal? Min(IEnumerable<decimal?> values) => values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty().Min();
    private static decimal? Max(IEnumerable<decimal?> values) => values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty().Max();
    private static decimal? Average(IEnumerable<decimal?> values) { var v = values.Where(x => x.HasValue).Select(x => x!.Value).ToList(); return v.Count == 0 ? null : v.Average(); }
}
