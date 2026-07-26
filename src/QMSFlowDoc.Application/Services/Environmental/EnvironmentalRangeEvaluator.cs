using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Environmental;

public sealed record EnvironmentalRanges(
    decimal RefrigeratorMin = EnvironmentalDefaults.RefrigeratorMin,
    decimal RefrigeratorMax = EnvironmentalDefaults.RefrigeratorMax,
    decimal FreezerMin = EnvironmentalDefaults.FreezerMin,
    decimal FreezerMax = EnvironmentalDefaults.FreezerMax,
    decimal RoomTemperatureMin = EnvironmentalDefaults.RoomTemperatureMin,
    decimal RoomTemperatureMax = EnvironmentalDefaults.RoomTemperatureMax,
    decimal RoomHumidityMin = EnvironmentalDefaults.RoomHumidityMin,
    decimal RoomHumidityMax = EnvironmentalDefaults.RoomHumidityMax);

public static class EnvironmentalRangeEvaluator
{
    public static bool IsOutOfRange(EnvironmentalReading reading, EnvironmentalRanges ranges)
    {
        // NC means the probe is disconnected. It is an equipment/sensor incident,
        // not a temperature result, so it must never be classified as out of range.
        if (reading.SensorNotConnected) return false;

        return reading.Source switch
        {
            EnvironmentalSource.REFRIGERATOR => Outside(reading.TemperatureCelsius, ranges.RefrigeratorMin, ranges.RefrigeratorMax),
            EnvironmentalSource.FREEZER => Outside(reading.TemperatureCelsius, ranges.FreezerMin, ranges.FreezerMax),
            EnvironmentalSource.ROOM => Outside(reading.TemperatureCelsius, ranges.RoomTemperatureMin, ranges.RoomTemperatureMax)
                || Outside(reading.HumidityPercent, ranges.RoomHumidityMin, ranges.RoomHumidityMax),
            _ => false
        };
    }

    private static bool Outside(decimal? value, decimal min, decimal max) => !value.HasValue || value.Value < min || value.Value > max;
}
