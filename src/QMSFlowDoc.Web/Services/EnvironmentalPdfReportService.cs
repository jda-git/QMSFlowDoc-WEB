using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QMSFlowDoc.Application.Services.Environmental;

namespace QMSFlowDoc.Web.Services;

public sealed class EnvironmentalPdfReportService
{
    public byte[] Create(EnvironmentalMonthlyReport report, string laboratoryName)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        var title = new XFont("Arial", 18, XFontStyleEx.Bold);
        var heading = new XFont("Arial", 11, XFontStyleEx.Bold);
        var body = new XFont("Arial", 9);
        var y = 48d;
        graphics.DrawString("Informe mensual de monitorización ambiental", title, XBrushes.DarkBlue, 42, y); y += 26;
        graphics.DrawString(laboratoryName, heading, XBrushes.Black, 42, y); y += 18;
        graphics.DrawString($"Periodo: {report.PeriodStart:dd/MM/yyyy HH:mm} - {report.PeriodEnd:dd/MM/yyyy HH:mm}", body, XBrushes.Black, 42, y); y += 28;
        graphics.DrawString("Fuente", heading, XBrushes.Black, 42, y);
        graphics.DrawString("Lecturas", heading, XBrushes.Black, 145, y);
        graphics.DrawString("T mín/máx/media", heading, XBrushes.Black, 220, y);
        graphics.DrawString("H mín/máx/media", heading, XBrushes.Black, 365, y);
        graphics.DrawString("Incidencias", heading, XBrushes.Black, 500, y); y += 16;
        foreach (var item in report.Statistics)
        {
            graphics.DrawString(item.Source.ToString(), body, XBrushes.Black, 42, y);
            graphics.DrawString(item.Readings.ToString(), body, XBrushes.Black, 145, y);
            graphics.DrawString(Format(item.Minimum, item.Maximum, item.Average), body, XBrushes.Black, 220, y);
            graphics.DrawString(Format(item.MinimumHumidity, item.MaximumHumidity, item.AverageHumidity), body, XBrushes.Black, 365, y);
            graphics.DrawString($"{item.OutOfRange} fuera rango / {item.DisconnectedSensors} NC", body, item.OutOfRange > 0 ? XBrushes.DarkRed : XBrushes.DarkGreen, 500, y); y += 18;
        }
        y += 12;
        DrawTrend(graphics, report.Readings.Where(r => r.Source != QMSFlowDoc.Domain.Entities.EnvironmentalSource.CENTRAL && r.TemperatureCelsius.HasValue).ToList(), 42, y, 510, 120, "Tendencia de temperatura (°C)", r => r.TemperatureCelsius!.Value, XColors.SteelBlue);
        y += 142;
        DrawTrend(graphics, report.Readings.Where(r => r.Source == QMSFlowDoc.Domain.Entities.EnvironmentalSource.ROOM && r.HumidityPercent.HasValue).ToList(), 42, y, 510, 90, "Tendencia de humedad ambiental (%)", r => r.HumidityPercent!.Value, XColors.Teal);
        y += 112;
        y += 20; graphics.DrawString("Incidencias detectadas", heading, XBrushes.DarkRed, 42, y); y += 16;
        foreach (var incident in report.Incidents.Take(18)) { graphics.DrawString($"{incident.RecordedAt:dd/MM HH:mm} - {incident.Source} - {(incident.SensorNotConnected ? "Sonda NC" : incident.TemperatureCelsius?.ToString("N1") + " °C")}", body, XBrushes.Black, 42, y); y += 13; }
        y += 12; graphics.DrawString($"Eventos CENTRAL: {report.CentralEvents.Count}", heading, XBrushes.Black, 42, y);
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }

    private static string Format(decimal? min, decimal? max, decimal? avg) => min is null ? "—" : $"{min:N1} / {max:N1} / {avg:N1}";

    private static void DrawTrend(XGraphics graphics, IReadOnlyList<QMSFlowDoc.Domain.Entities.EnvironmentalReading> readings, double x, double y, double width, double height, string title, Func<QMSFlowDoc.Domain.Entities.EnvironmentalReading, decimal> selector, XColor color)
    {
        graphics.DrawString(title, new XFont("Arial", 9, XFontStyleEx.Bold), XBrushes.Black, x, y);
        y += 8; graphics.DrawLine(XPens.Gray, x, y + height, x + width, y + height); graphics.DrawLine(XPens.Gray, x, y, x, y + height);
        if (readings.Count < 2) return;
        var values = readings.Select(selector).ToList(); var min = values.Min(); var max = values.Max(); var span = Math.Max(1m, max - min);
        var pen = new XPen(color, 1.2);
        for (var i = 1; i < readings.Count; i++)
        {
            var x1 = x + width * (i - 1) / (readings.Count - 1); var x2 = x + width * i / (readings.Count - 1);
            var y1 = y + height - (double)((values[i - 1] - min) / span) * height; var y2 = y + height - (double)((values[i] - min) / span) * height;
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }
        graphics.DrawString($"min {min:N1}  max {max:N1}", new XFont("Arial", 7), XBrushes.Gray, x + width - 80, y + 8);
    }
}
