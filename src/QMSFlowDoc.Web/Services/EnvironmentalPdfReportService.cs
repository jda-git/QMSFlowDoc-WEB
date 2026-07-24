using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QMSFlowDoc.Application.Services.Environmental;
using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Web.Services;

public sealed class EnvironmentalPdfReportService
{
    private static readonly XFont Title = new("Arial", 17, XFontStyleEx.Bold);
    private static readonly XFont Heading = new("Arial", 10, XFontStyleEx.Bold);
    private static readonly XFont Body = new("Arial", 8);
    private static readonly XFont Axis = new("Arial", 6.5);

    public byte[] Create(EnvironmentalMonthlyReport report, string laboratoryName)
    {
        var document = new PdfDocument();
        var page = document.AddPage(); page.Size = PdfSharp.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        var y = DrawHeaderAndSummary(graphics, report, laboratoryName);

        var refrigerator = Measured(report, EnvironmentalSource.REFRIGERATOR, r => r.TemperatureCelsius);
        var freezer = Measured(report, EnvironmentalSource.FREEZER, r => r.TemperatureCelsius);
        var roomTemperature = Measured(report, EnvironmentalSource.ROOM, r => r.TemperatureCelsius);
        var roomHumidity = Measured(report, EnvironmentalSource.ROOM, r => r.HumidityPercent);

        DrawSingleTrend(graphics, refrigerator, 42, y, 510, 138, "Temperatura de nevera", "°C", XColors.RoyalBlue); y += 150;
        if (freezer.Count > 0) { DrawSingleTrend(graphics, freezer, 42, y, 510, 138, "Temperatura de congelador", "°C", XColors.MediumPurple); y += 150; }
        DrawCombinedTrend(graphics, roomTemperature, roomHumidity, 42, y, 510, 156, "Temperatura y humedad ambiental", "Temperatura (°C)", "Humedad (%)", XColors.DarkOrange, XColors.DeepSkyBlue);

        var incidentsPage = document.AddPage(); incidentsPage.Size = PdfSharp.PageSize.A4;
        using var incidentsGraphics = XGraphics.FromPdfPage(incidentsPage);
        DrawIncidents(incidentsGraphics, report, laboratoryName);
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }

    private static double DrawHeaderAndSummary(XGraphics graphics, EnvironmentalMonthlyReport report, string laboratoryName)
    {
        var y = 42d;
        graphics.DrawString("Informe de monitorización ambiental", Title, XBrushes.DarkBlue, 42, y); y += 22;
        graphics.DrawString(laboratoryName, Heading, XBrushes.Black, 42, y); y += 15;
        graphics.DrawString($"Periodo: {report.PeriodStart:dd/MM/yyyy HH:mm} - {report.PeriodEnd:dd/MM/yyyy HH:mm}", Body, XBrushes.Black, 42, y); y += 24;
        graphics.DrawString("Fuente", Heading, XBrushes.Black, 42, y); graphics.DrawString("Lecturas", Heading, XBrushes.Black, 135, y);
        graphics.DrawString("T min/max/media", Heading, XBrushes.Black, 210, y); graphics.DrawString("H min/max/media", Heading, XBrushes.Black, 350, y); graphics.DrawString("Incidencias", Heading, XBrushes.Black, 485, y); y += 15;
        foreach (var item in report.Statistics)
        {
            graphics.DrawString(item.Source.ToString(), Body, XBrushes.Black, 42, y); graphics.DrawString(item.Readings.ToString(), Body, XBrushes.Black, 135, y);
            graphics.DrawString(Format(item.Minimum, item.Maximum, item.Average), Body, XBrushes.Black, 210, y); graphics.DrawString(Format(item.MinimumHumidity, item.MaximumHumidity, item.AverageHumidity), Body, XBrushes.Black, 350, y);
            graphics.DrawString($"{item.OutOfRange} fuera rango / {item.DisconnectedSensors} NC", Body, item.OutOfRange > 0 ? XBrushes.DarkRed : XBrushes.DarkGreen, 485, y); y += 14;
        }
        return y + 16;
    }

    private static List<EnvironmentalReading> Measured(EnvironmentalMonthlyReport report, EnvironmentalSource source, Func<EnvironmentalReading, decimal?> selector) => report.Readings.Where(r => r.Source == source && selector(r).HasValue).OrderBy(r => r.RecordedAt).ToList();
    private static string Format(decimal? min, decimal? max, decimal? avg) => min is null ? "-" : $"{min:N1} / {max:N1} / {avg:N1}";

    private static void DrawSingleTrend(XGraphics graphics, IReadOnlyList<EnvironmentalReading> readings, double x, double y, double width, double height, string title, string unit, XColor color)
    {
        graphics.DrawString(title, Heading, XBrushes.Black, x, y); y += 12;
        var plot = new XRect(x + 42, y, width - 52, height - 30);
        var values = readings.Select(r => r.TemperatureCelsius!.Value).ToList();
        var range = Range(values); DrawGridAndAxes(graphics, plot, range, unit, null, null);
        DrawSeries(graphics, readings, r => r.TemperatureCelsius!.Value, plot, range, color);
        DrawDateAxis(graphics, readings, plot);
    }

    private static void DrawCombinedTrend(XGraphics graphics, IReadOnlyList<EnvironmentalReading> temperatures, IReadOnlyList<EnvironmentalReading> humidity, double x, double y, double width, double height, string title, string leftUnit, string rightUnit, XColor temperatureColor, XColor humidityColor)
    {
        graphics.DrawString(title, Heading, XBrushes.Black, x, y); y += 12;
        var plot = new XRect(x + 42, y, width - 94, height - 30);
        var temperatureRange = Range(temperatures.Select(r => r.TemperatureCelsius!.Value)); var humidityRange = Range(humidity.Select(r => r.HumidityPercent!.Value));
        DrawGridAndAxes(graphics, plot, temperatureRange, leftUnit, humidityRange, rightUnit);
        DrawSeries(graphics, temperatures, r => r.TemperatureCelsius!.Value, plot, temperatureRange, temperatureColor);
        DrawSeries(graphics, humidity, r => r.HumidityPercent!.Value, plot, humidityRange, humidityColor);
        DrawDateAxis(graphics, temperatures.Concat(humidity).OrderBy(r => r.RecordedAt).ToList(), plot);
        graphics.DrawRectangle(new XSolidBrush(temperatureColor), x + 5, y + 2, 7, 7); graphics.DrawString("Temperatura", Axis, XBrushes.Black, x + 15, y + 8);
        graphics.DrawRectangle(new XSolidBrush(humidityColor), x + 82, y + 2, 7, 7); graphics.DrawString("Humedad", Axis, XBrushes.Black, x + 92, y + 8);
    }

    private static void DrawGridAndAxes(XGraphics graphics, XRect plot, (decimal Min, decimal Max) leftRange, string leftUnit, (decimal Min, decimal Max)? rightRange, string? rightUnit)
    {
        for (var index = 0; index < 5; index++)
        {
            var ratio = index / 4d; var yy = plot.Y + plot.Height * ratio; graphics.DrawLine(new XPen(XColors.LightGray, .35), plot.X, yy, plot.Right, yy);
            var left = leftRange.Max - (leftRange.Max - leftRange.Min) * (decimal)ratio; graphics.DrawString($"{left:N1} {leftUnit}", Axis, XBrushes.DimGray, new XRect(plot.X - 39, yy - 4, 36, 9), XStringFormats.TopRight);
            if (rightRange.HasValue) { var right = rightRange.Value.Max - (rightRange.Value.Max - rightRange.Value.Min) * (decimal)ratio; graphics.DrawString($"{right:N0} {rightUnit}", Axis, XBrushes.DimGray, new XRect(plot.Right + 3, yy - 4, 38, 9), XStringFormats.TopLeft); }
        }
        graphics.DrawLine(XPens.Gray, plot.X, plot.Y, plot.X, plot.Bottom); graphics.DrawLine(XPens.Gray, plot.X, plot.Bottom, plot.Right, plot.Bottom);
    }

    private static void DrawSeries(XGraphics graphics, IReadOnlyList<EnvironmentalReading> readings, Func<EnvironmentalReading, decimal> selector, XRect plot, (decimal Min, decimal Max) range, XColor color)
    {
        if (readings.Count < 2) return;
        var start = readings.First().RecordedAt; var end = readings.Last().RecordedAt; var seconds = Math.Max(1, (end - start).TotalSeconds); var pen = new XPen(color, 1.05);
        XPoint? previous = null;
        foreach (var reading in readings)
        {
            var point = new XPoint(plot.X + (reading.RecordedAt - start).TotalSeconds / seconds * plot.Width, plot.Bottom - (double)((selector(reading) - range.Min) / (range.Max - range.Min)) * plot.Height);
            if (previous.HasValue) graphics.DrawLine(pen, previous.Value, point); previous = point;
        }
    }

    private static void DrawDateAxis(XGraphics graphics, IReadOnlyList<EnvironmentalReading> readings, XRect plot)
    {
        if (readings.Count == 0) return;
        var start = readings.First().RecordedAt.Date; var end = readings.Last().RecordedAt.Date; var totalDays = Math.Max(1, (end - start).Days); var span = Math.Max(1, (readings.Last().RecordedAt - readings.First().RecordedAt).TotalSeconds);
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var relative = Math.Clamp((date - readings.First().RecordedAt).TotalSeconds / span, 0d, 1d); var xx = plot.X + relative * plot.Width; var label = date == start || date == end || (date - start).Days % 7 == 0;
            graphics.DrawLine(XPens.Gray, xx, plot.Bottom, xx, plot.Bottom + (label ? 4 : 2));
            if (label) graphics.DrawString(date.ToString("dd/MM"), Axis, XBrushes.DimGray, new XRect(xx - 14, plot.Bottom + 5, 28, 9), XStringFormats.TopCenter);
        }
    }

    private static (decimal Min, decimal Max) Range(IEnumerable<decimal> input)
    {
        var values = input.ToList(); if (values.Count == 0) return (0, 1); var min = values.Min(); var max = values.Max(); var padding = Math.Max(.5m, (max - min) * .1m); return (min - padding, max + padding);
    }

    private static void DrawIncidents(XGraphics graphics, EnvironmentalMonthlyReport report, string laboratoryName)
    {
        var y = 45d; graphics.DrawString("Incidencias y eventos ambientales", Title, XBrushes.DarkBlue, 42, y); y += 22; graphics.DrawString(laboratoryName, Heading, XBrushes.Black, 42, y); y += 22;
        graphics.DrawString("Incidencias detectadas", Heading, XBrushes.DarkRed, 42, y); y += 16;
        foreach (var incident in report.Incidents.Concat(report.Readings.Where(r => r.SensorNotConnected)).OrderBy(r => r.RecordedAt).Take(46)) { graphics.DrawString($"{incident.RecordedAt:dd/MM/yyyy HH:mm} - {incident.Source} - {(incident.SensorNotConnected ? "Sonda NC" : $"{incident.TemperatureCelsius:N1} °C fuera de rango")}", Body, XBrushes.Black, 42, y); y += 12; }
        if (!report.Incidents.Any() && !report.Readings.Any(r => r.SensorNotConnected)) graphics.DrawString("No se han detectado incidencias de temperatura. Las sondas NC se registran por separado.", Body, XBrushes.DarkGreen, 42, y);
        y += 22; graphics.DrawString($"Eventos CENTRAL: {report.CentralEvents.Count}", Heading, XBrushes.Black, 42, y);
    }
}
