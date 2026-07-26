using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Environmental;

public sealed record EnvironmentalSourceStatistics(
    string Parameter,
    EnvironmentalSource Source,
    string Unit,
    int Readings,
    decimal? Minimum,
    decimal? Maximum,
    decimal? Average,
    TimeSpan TotalOutOfRange,
    int OutOfRangeEpisodes,
    decimal? MeanKineticTemperature,
    int DisconnectedSensors);

public sealed record EnvironmentalMonthlyReport(
    DateTime PeriodStart, DateTime PeriodEnd, IReadOnlyList<EnvironmentalSourceStatistics> Statistics,
    IReadOnlyList<EnvironmentalReading> Incidents, IReadOnlyList<EnvironmentalReading> CentralEvents,
    IReadOnlyList<EnvironmentalReading> Readings);

public static class EnvironmentalMonthlyReportBuilder
{
    private const double GasConstantKjPerMolKelvin = 0.00831446261815324d;
    private const double ActivationEnergyKjPerMol = 83.144d;

    public static EnvironmentalMonthlyReport Build(IEnumerable<EnvironmentalReading> readings, EnvironmentalRanges? ranges = null)
    {
        var all = readings.OrderBy(r => r.RecordedAt).ToList();
        if (all.Count == 0) throw new InvalidOperationException("No hay lecturas para generar el informe.");
        ranges ??= new EnvironmentalRanges();
        var centralEvents = all.Where(r => r.Source == EnvironmentalSource.CENTRAL).ToList();
        var measured = new List<EnvironmentalSourceStatistics>
        {
            BuildTemperature("Temperatura nevera", EnvironmentalSource.REFRIGERATOR, all, ranges.RefrigeratorMin, ranges.RefrigeratorMax, centralEvents, true),
            BuildTemperature("Temperatura congelador", EnvironmentalSource.FREEZER, all, ranges.FreezerMin, ranges.FreezerMax, centralEvents, true),
            BuildTemperature("Temperatura ambiente", EnvironmentalSource.ROOM, all, ranges.RoomTemperatureMin, ranges.RoomTemperatureMax, centralEvents, false),
            BuildHumidity(all, ranges.RoomHumidityMin, ranges.RoomHumidityMax, centralEvents)
        };
        return new EnvironmentalMonthlyReport(all.First().RecordedAt, all.Last().RecordedAt, measured,
            all.Where(r => r.IsOutOfRange).ToList(), centralEvents, all);
    }

    private static EnvironmentalSourceStatistics BuildTemperature(
        string parameter, EnvironmentalSource source, IReadOnlyList<EnvironmentalReading> all, decimal min, decimal max,
        IReadOnlyList<EnvironmentalReading> centralEvents, bool includeMkt)
    {
        var readings = all.Where(r => r.Source == source && r.TemperatureCelsius.HasValue && !r.SensorNotConnected).OrderBy(r => r.RecordedAt).ToList();
        var values = readings.Select(r => r.TemperatureCelsius!.Value).ToList();
        var intervals = BuildMeasuredExcursionIntervals(readings, r => Outside(r.TemperatureCelsius!.Value, min, max));
        intervals.AddRange(BriefExcursionIntervals(centralEvents, source));
        var merged = Merge(intervals);
        return new EnvironmentalSourceStatistics(parameter, source, "°C", readings.Count, Min(values), Max(values), Average(values),
            Duration(merged), merged.Count, includeMkt ? MeanKineticTemperature(readings) : null,
            all.Count(r => r.Source == source && r.SensorNotConnected));
    }

    private static EnvironmentalSourceStatistics BuildHumidity(
        IReadOnlyList<EnvironmentalReading> all, decimal min, decimal max, IReadOnlyList<EnvironmentalReading> centralEvents)
    {
        var readings = all.Where(r => r.Source == EnvironmentalSource.ROOM && r.HumidityPercent.HasValue).OrderBy(r => r.RecordedAt).ToList();
        var values = readings.Select(r => r.HumidityPercent!.Value).ToList();
        var intervals = BuildMeasuredExcursionIntervals(readings, r => Outside(r.HumidityPercent!.Value, min, max));
        intervals.AddRange(BriefExcursionIntervals(centralEvents, null, humidity: true));
        var merged = Merge(intervals);
        return new EnvironmentalSourceStatistics("Humedad ambiente", EnvironmentalSource.ROOM, "%", readings.Count, Min(values), Max(values), Average(values),
            Duration(merged), merged.Count, null, 0);
    }

    private static List<EnvironmentalExcursionInterval> BuildMeasuredExcursionIntervals(
        IReadOnlyList<EnvironmentalReading> readings, Func<EnvironmentalReading, bool> isOutside)
    {
        if (readings.Count == 0) return new();
        var fallback = TypicalInterval(readings);
        var intervals = new List<EnvironmentalExcursionInterval>();
        for (var index = 0; index < readings.Count; index++)
        {
            if (!isOutside(readings[index])) continue;
            var end = index + 1 < readings.Count ? readings[index + 1].RecordedAt : readings[index].RecordedAt.Add(fallback);
            if (end > readings[index].RecordedAt) intervals.Add(new(readings[index].RecordedAt, end));
        }
        return intervals;
    }

    private static TimeSpan TypicalInterval(IReadOnlyList<EnvironmentalReading> readings)
    {
        var spans = readings.Zip(readings.Skip(1), (left, right) => right.RecordedAt - left.RecordedAt)
            .Where(span => span > TimeSpan.Zero && span <= TimeSpan.FromHours(6)).OrderBy(span => span).ToList();
        return spans.Count == 0 ? TimeSpan.FromHours(1) : spans[spans.Count / 2];
    }

    private static List<EnvironmentalExcursionInterval> BriefExcursionIntervals(
        IReadOnlyList<EnvironmentalReading> events, EnvironmentalSource? source, bool humidity = false)
    {
        return events.Select(EnvironmentalCentralEventParser.ParseBriefExcursion)
            .Where(e => e is not null && (humidity ? e!.IsHumidity : e!.Source == source))
            .Select(e => new EnvironmentalExcursionInterval(e!.EndedAt - e.Duration, e.EndedAt))
            .ToList();
    }

    private static List<EnvironmentalExcursionInterval> Merge(IEnumerable<EnvironmentalExcursionInterval> intervals)
    {
        var ordered = intervals.Where(i => i.End > i.Start).OrderBy(i => i.Start).ToList();
        if (ordered.Count == 0) return new();
        var merged = new List<EnvironmentalExcursionInterval> { ordered[0] };
        foreach (var interval in ordered.Skip(1))
        {
            var last = merged[^1];
            if (interval.Start <= last.End)
                merged[^1] = new EnvironmentalExcursionInterval(last.Start, interval.End > last.End ? interval.End : last.End);
            else
                merged.Add(interval);
        }
        return merged;
    }

    private static TimeSpan Duration(IEnumerable<EnvironmentalExcursionInterval> intervals) =>
        TimeSpan.FromTicks(intervals.Sum(interval => (interval.End - interval.Start).Ticks));

    private static decimal? MeanKineticTemperature(IReadOnlyList<EnvironmentalReading> readings)
    {
        if (readings.Count == 0) return null;
        var fallback = TypicalInterval(readings);
        var weightedExponentialSum = 0d;
        var totalSeconds = 0d;
        for (var index = 0; index < readings.Count; index++)
        {
            var duration = index + 1 < readings.Count ? readings[index + 1].RecordedAt - readings[index].RecordedAt : fallback;
            if (duration <= TimeSpan.Zero) continue;
            var kelvin = (double)readings[index].TemperatureCelsius!.Value + 273.15d;
            weightedExponentialSum += Math.Exp(-ActivationEnergyKjPerMol / (GasConstantKjPerMolKelvin * kelvin)) * duration.TotalSeconds;
            totalSeconds += duration.TotalSeconds;
        }
        if (totalSeconds <= 0d || weightedExponentialSum <= 0d) return null;
        var mktKelvin = -ActivationEnergyKjPerMol / (GasConstantKjPerMolKelvin * Math.Log(weightedExponentialSum / totalSeconds));
        return decimal.Round((decimal)(mktKelvin - 273.15d), 2, MidpointRounding.AwayFromZero);
    }

    private static bool Outside(decimal value, decimal min, decimal max) => value < min || value > max;
    private static decimal? Min(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : values.Min();
    private static decimal? Max(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : values.Max();
    private static decimal? Average(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : decimal.Round(values.Average(), 2, MidpointRounding.AwayFromZero);

    private sealed record EnvironmentalExcursionInterval(DateTime Start, DateTime End);
}
