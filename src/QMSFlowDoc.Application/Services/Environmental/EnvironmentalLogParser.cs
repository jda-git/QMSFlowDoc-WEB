using System.Globalization;
using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Environmental;

/// <summary>Parses the hourly logger format: date;time;source;value[;value/event].</summary>
public static class EnvironmentalLogParser
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public static IReadOnlyList<EnvironmentalReading> Parse(string content, string batchId, string importedByName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);

        var readings = new List<EnvironmentalReading>();
        foreach (var rawLine in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = rawLine.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || !DateTime.TryParseExact($"{parts[0]} {parts[1]}", "dd/MM/yyyy HH:mm:ss", SpanishCulture, DateTimeStyles.None, out var recordedAt))
                continue;

            var sourceToken = parts[2].ToUpperInvariant();
            var source = sourceToken switch
            {
                "NEVERA" => EnvironmentalSource.REFRIGERATOR,
                "CONGELADOR" => EnvironmentalSource.FREEZER,
                "HABITACION" or "HABITACIÓN" => EnvironmentalSource.ROOM,
                "CENTRAL" => EnvironmentalSource.CENTRAL,
                _ => (EnvironmentalSource?)null
            };
            if (!source.HasValue) continue;

            var reading = new EnvironmentalReading
            {
                Id = Guid.NewGuid(), RecordedAt = recordedAt, Source = source.Value,
                RawLine = rawLine, ImportedAt = DateTime.UtcNow, ImportedByName = importedByName, ImportBatchId = batchId
            };

            if (source == EnvironmentalSource.CENTRAL)
            {
                reading.Event = parts.Length > 3 ? string.Join(';', parts.Skip(3)) : null;
            }
            else if (source == EnvironmentalSource.ROOM)
            {
                reading.TemperatureCelsius = ParseDecimal(parts.ElementAtOrDefault(3));
                reading.HumidityPercent = ParseDecimal(parts.ElementAtOrDefault(4));
            }
            else if (source == EnvironmentalSource.REFRIGERATOR)
            {
                reading.TemperatureCelsius = ParseDecimal(parts.ElementAtOrDefault(3));
                readings.Add(reading);
                var freezerValue = parts.ElementAtOrDefault(4);
                readings.Add(new EnvironmentalReading
                {
                    Id = Guid.NewGuid(), RecordedAt = recordedAt, Source = EnvironmentalSource.FREEZER,
                    TemperatureCelsius = ParseDecimal(freezerValue),
                    SensorNotConnected = string.Equals(freezerValue, "NC", StringComparison.OrdinalIgnoreCase),
                    RawLine = rawLine, ImportedAt = reading.ImportedAt, ImportedByName = importedByName, ImportBatchId = batchId
                });
                continue;
            }
            else
            {
                reading.TemperatureCelsius = ParseDecimal(parts.ElementAtOrDefault(3));
            }
            readings.Add(reading);
        }
        return readings;
    }

    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Number, SpanishCulture, out var parsed) ? parsed : null;
}
