using QMSFlowDoc.Application.Services.Environmental;
using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Tests.Compliance;

public class EnvironmentalLogParserTests
{
    [Fact]
    public void Parser_ExpandsRefrigeratorLineAndPreservesCentralEvent()
    {
        const string content = "06/07/2026;00:00:03;NEVERA;4.9;NC\n06/07/2026;00:00:07;HABITACION;18.2;58\n06/07/2026;00:01:00;CENTRAL;ALARMA_RECIBIDA_NEVERA";
        var readings = EnvironmentalLogParser.Parse(content, "batch", "tester");
        Assert.Equal(4, readings.Count);
        Assert.Contains(readings, r => r.Source == EnvironmentalSource.REFRIGERATOR && r.TemperatureCelsius == 4.9m);
        Assert.Contains(readings, r => r.Source == EnvironmentalSource.FREEZER && r.SensorNotConnected);
        Assert.Contains(readings, r => r.Source == EnvironmentalSource.ROOM && r.HumidityPercent == 58m);
        Assert.Contains(readings, r => r.Source == EnvironmentalSource.CENTRAL && r.Event == "ALARMA_RECIBIDA_NEVERA");
    }

    [Fact]
    public void RangeEvaluator_FlagsConfiguredExcursions()
    {
        var refrigerator = new EnvironmentalReading { Source = EnvironmentalSource.REFRIGERATOR, TemperatureCelsius = 9m };
        var room = new EnvironmentalReading { Source = EnvironmentalSource.ROOM, TemperatureCelsius = 22m, HumidityPercent = 61m };
        Assert.True(EnvironmentalRangeEvaluator.IsOutOfRange(refrigerator, new EnvironmentalRanges()));
        Assert.True(EnvironmentalRangeEvaluator.IsOutOfRange(room, new EnvironmentalRanges()));
    }
}
