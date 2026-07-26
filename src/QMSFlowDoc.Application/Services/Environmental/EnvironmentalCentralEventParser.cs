using System.Globalization;
using System.Text.RegularExpressions;
using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Environmental;

/// <summary>Understands the CENTRAL events emitted by the new environmental controller.</summary>
public static partial class EnvironmentalCentralEventParser
{
    [GeneratedRegex(@"EXCURSION_BREVE_(?<target>NEVERA|CONGELADOR|FREEZER|HABITACION|AMBIENTE|HUMEDAD).*?durante\s+(?<minutes>[0-9]+(?:[\.,][0-9]+)?)\s*min", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BriefExcursionRegex();

    public static EnvironmentalBriefExcursion? ParseBriefExcursion(EnvironmentalReading reading)
    {
        if (reading.Source != EnvironmentalSource.CENTRAL || string.IsNullOrWhiteSpace(reading.Event)) return null;
        var match = BriefExcursionRegex().Match(reading.Event);
        if (!match.Success || !decimal.TryParse(match.Groups["minutes"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
            return null;

        var target = match.Groups["target"].Value.ToUpperInvariant();
        var isHumidity = target == "HUMEDAD";
        EnvironmentalSource? source = target switch
        {
            "NEVERA" => EnvironmentalSource.REFRIGERATOR,
            "CONGELADOR" or "FREEZER" => EnvironmentalSource.FREEZER,
            "HABITACION" or "AMBIENTE" => EnvironmentalSource.ROOM,
            _ => null
        };
        return new EnvironmentalBriefExcursion(reading.RecordedAt, TimeSpan.FromMinutes((double)minutes), source, isHumidity, reading.Event);
    }
}

public sealed record EnvironmentalBriefExcursion(DateTime EndedAt, TimeSpan Duration, EnvironmentalSource? Source, bool IsHumidity, string Description);
