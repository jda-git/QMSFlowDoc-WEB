using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Environmental;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;

namespace QMSFlowDoc.Infrastructure.Services.Environmental;

public sealed class EnvironmentalImportService(QmsDbContext dbContext)
{
    public async Task<EnvironmentalRanges> GetRangesAsync(CancellationToken ct = default)
    {
        var values = await dbContext.SystemSettings
            .Where(s => s.Key.StartsWith("Environmental."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        decimal Read(string key, decimal fallback) => values.TryGetValue(key, out var value) && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        return new EnvironmentalRanges(
            Read("Environmental.Refrigerator.Min", EnvironmentalDefaults.RefrigeratorMin), Read("Environmental.Refrigerator.Max", EnvironmentalDefaults.RefrigeratorMax),
            Read("Environmental.Freezer.Min", EnvironmentalDefaults.FreezerMin), Read("Environmental.Freezer.Max", EnvironmentalDefaults.FreezerMax),
            Read("Environmental.RoomTemperature.Min", EnvironmentalDefaults.RoomTemperatureMin), Read("Environmental.RoomTemperature.Max", EnvironmentalDefaults.RoomTemperatureMax),
            Read("Environmental.RoomHumidity.Min", EnvironmentalDefaults.RoomHumidityMin), Read("Environmental.RoomHumidity.Max", EnvironmentalDefaults.RoomHumidityMax));
    }

    public async Task<EnvironmentalImportSummary> ImportAsync(string content, string importedByName, EnvironmentalRanges ranges, CancellationToken ct = default)
    {
        var batchId = Guid.NewGuid().ToString("N");
        var parsed = EnvironmentalLogParser.Parse(content, batchId, importedByName);
        if (parsed.Count == 0) throw new InvalidOperationException("El fichero no contiene registros ambientales válidos.");

        var timestamps = parsed.Select(r => r.RecordedAt).Distinct().ToList();
        var existing = await dbContext.EnvironmentalReadings
            .Where(r => timestamps.Contains(r.RecordedAt))
            .Select(r => new { r.RecordedAt, r.Source })
            .ToListAsync(ct);
        var known = existing.Select(r => (r.RecordedAt, r.Source)).ToHashSet();
        var accepted = parsed.Where(r => known.Add((r.RecordedAt, r.Source))).ToList();
        foreach (var reading in accepted) reading.IsOutOfRange = EnvironmentalRangeEvaluator.IsOutOfRange(reading, ranges);
        dbContext.EnvironmentalReadings.AddRange(accepted);
        await dbContext.SaveChangesAsync(ct);
        return new EnvironmentalImportSummary(batchId, parsed.Min(r => r.RecordedAt), parsed.Max(r => r.RecordedAt), accepted.Count(r => r.Source != EnvironmentalSource.CENTRAL), accepted.Count(r => r.Source == EnvironmentalSource.CENTRAL), parsed.Count - accepted.Count, accepted.Count(r => r.IsOutOfRange), accepted.Count(r => r.SensorNotConnected));
    }
}
