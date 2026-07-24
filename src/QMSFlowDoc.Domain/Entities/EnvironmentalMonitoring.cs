namespace QMSFlowDoc.Domain.Entities;

public enum EnvironmentalSource
{
    REFRIGERATOR,
    FREEZER,
    ROOM,
    CENTRAL
}

/// <summary>Immutable reading imported from the hourly environmental logger (ISO 15189:2022 §6.3).</summary>
public class EnvironmentalReading
{
    public Guid Id { get; set; }
    public DateTime RecordedAt { get; set; }
    public EnvironmentalSource Source { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public decimal? HumidityPercent { get; set; }
    public bool SensorNotConnected { get; set; }
    public bool IsOutOfRange { get; set; }
    public string? RawLine { get; set; }
    public string? Event { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string ImportedByName { get; set; } = string.Empty;
    public string ImportBatchId { get; set; } = string.Empty;
}

public static class EnvironmentalDefaults
{
    public const decimal RefrigeratorMin = 2m;
    public const decimal RefrigeratorMax = 8m;
    public const decimal FreezerMin = -25m;
    public const decimal FreezerMax = -15m;
    public const decimal RoomTemperatureMin = 18m;
    public const decimal RoomTemperatureMax = 25m;
    public const decimal RoomHumidityMin = 40m;
    public const decimal RoomHumidityMax = 60m;
}

public sealed record EnvironmentalImportSummary(
    string BatchId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int ImportedReadings,
    int CentralEvents,
    int Duplicates,
    int OutOfRangeReadings,
    int DisconnectedSensors);
