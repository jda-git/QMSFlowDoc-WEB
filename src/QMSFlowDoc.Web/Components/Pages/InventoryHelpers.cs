using Microsoft.JSInterop;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Web.Components.Pages;

/// <summary>
/// Static helper methods extracted to a pure C# file to avoid Razor parser issues.
/// The Razor parser misinterprets HTML tags embedded inside C# string interpolations
/// as actual component markup when they appear inside .razor files.
/// </summary>
public static class InventoryHelpers
{
    // ── JS Helpers ────────────────────────────────────────────────────────────

    public static async Task<bool> ConfirmAsync(IJSRuntime js, string message)
        => await js.InvokeAsync<bool>("confirm", message);

    public static async Task PrintAsync(IJSRuntime js, string html)
        => await js.InvokeVoidAsync("openPrintWindow", html);

    public static async Task DownloadCsvAsync(IJSRuntime js, string csv, string filename)
        => await js.InvokeVoidAsync("downloadFile", csv, filename, "text/csv");

    // ── HTML Builder Helpers ───────────────────────────────────────────────────

    public static string WrapPrintHtml(string title, string tableHtml, string subtitle = "")
    {
        var css =
            "* { box-sizing: border-box; }" +
            "body { font-family: Arial, Helvetica, sans-serif; font-size: 10pt; color: #1a1a1a; margin: 0; padding: 20px; }" +
            "h1 { font-size: 14pt; margin: 0 0 2px; }" +
            "h2 { font-size: 11pt; color: #555; margin: 0 0 12px; }" +
            ".meta { font-size: 8pt; color: #777; margin-bottom: 16px; }" +
            "table { width: 100%; border-collapse: collapse; margin-top: 8px; }" +
            "th { background: #1a4731; color: white; padding: 5px 8px; text-align: left; font-size: 8.5pt; }" +
            "td { padding: 4px 8px; font-size: 9pt; border-bottom: 1px solid #e5e7eb; }" +
            "tr:nth-child(even) { background: #f9fafb; }" +
            ".lot-row td { background: #f0fdf4; padding-left: 24px; font-size: 8.5pt; color: #374151; }" +
            ".badge-ok { color: #15803d; font-weight: 600; }" +
            ".badge-warn { color: #b45309; font-weight: 600; }" +
            ".badge-danger { color: #dc2626; font-weight: 600; }" +
            ".badge-low { color: #dc2626; font-weight: 700; }" +
            ".text-right { text-align: right; }" +
            "@media print { body { padding: 0; } @page { margin: 15mm 12mm; } }";

        var encodedTitle = System.Net.WebUtility.HtmlEncode(title);
        var encodedSubtitle = System.Net.WebUtility.HtmlEncode(subtitle);

        return string.Concat(
            "<!DOCTYPE html>",
            "<html lang=\"es\">",
            "<head>",
            "<meta charset=\"utf-8\" />",
            "<title>", encodedTitle, "</title>",
            "<style>", css, "</style>",
            "</head>",
            "<body>",
            "<h1>QMSFlowDoc &mdash; ", encodedTitle, "</h1>",
            "<h2>", encodedSubtitle, "</h2>",
            "<div class=\"meta\">Generado: ", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), " | Sistema: QMSFlowDoc ISO 15189</div>",
            tableHtml,
            "</body>",
            "</html>"
        );
    }

    // ── 1. Full Reagent List ───────────────────────────────────────────────────

    public static string BuildFullReagentListHtml(List<ReagentListDto> reagentsList)
    {
        var sb = new System.Text.StringBuilder();
        var headers = new[] { "Código", "Nombre", "Fluorescencia", "Tipo", "Proveedor", "Lote", "Uds. Disp.", "Caducidad" };
        sb.Append("<table><thead><tr>");
        foreach (var h in headers) sb.Append("<th>").Append(h).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        var sorted = reagentsList.OrderBy(r => r.InternalCode ?? r.Name).ToList();
        foreach (var r in sorted)
        {
            var activeLots = r.AvailableLots.OrderBy(l => l.ExpiryDate).ToList();
            if (!activeLots.Any())
            {
                sb.Append("<tr>")
                  .Append("<td>").Append(r.InternalCode ?? "N/A").Append("</td>")
                  .Append("<td>").Append(Enc(r.Name)).Append("</td>")
                  .Append("<td>").Append(Enc(r.Fluorescence)).Append("</td>")
                  .Append("<td>").Append(Enc(r.ReagentType)).Append("</td>")
                  .Append("<td>").Append(Enc(r.SupplierName) ?? "—").Append("</td>")
                  .Append("<td colspan='3' style='color:#aaa'>Sin stock</td>")
                  .Append("</tr>");
                continue;
            }
            bool first = true;
            foreach (var lot in activeLots)
            {
                var expDate = lot.ExpiryDate ?? DateTime.MaxValue;
                var expClass = expDate < DateTime.UtcNow ? "badge-danger" :
                               expDate < DateTime.UtcNow.AddDays(60) ? "badge-warn" : "badge-ok";
                var expStr = lot.ExpiryDate.HasValue ? lot.ExpiryDate.Value.ToString("dd/MM/yyyy") : "N/A";
                sb.Append("<tr").Append(first ? "" : " class='lot-row'").Append(">");
                if (first)
                {
                    sb.Append("<td>").Append(Enc(r.InternalCode) ?? "N/A").Append("</td>")
                      .Append("<td><strong>").Append(Enc(r.Name)).Append("</strong></td>")
                      .Append("<td>").Append(Enc(r.Fluorescence)).Append("</td>")
                      .Append("<td>").Append(Enc(r.ReagentType)).Append("</td>")
                      .Append("<td>").Append(Enc(r.SupplierName) ?? "—").Append("</td>");
                    first = false;
                }
                else
                {
                    sb.Append("<td></td><td></td><td></td><td></td><td></td>");
                }
                sb.Append("<td>").Append(Enc(lot.LotNumber)).Append("</td>")
                  .Append("<td class='text-right'>").Append(lot.Qty.ToString("N0")).Append("</td>")
                  .Append("<td class='").Append(expClass).Append("'>").Append(expStr).Append("</td>")
                  .Append("</tr>");
            }
        }
        sb.Append("</tbody></table>");
        var subtitle = $"{sorted.Count} reactivos — {sorted.Sum(r => r.AvailableLots.Sum(l => l.Qty)):N0} unidades en stock total";
        return WrapPrintHtml("Listado Completo de Reactivos", sb.ToString(), subtitle);
    }

    public static string BuildFullReagentListCsv(List<ReagentListDto> reagentsList)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Código;Nombre;Fluorescencia;Tipo;Proveedor;Lote;Uds. Disponibles;Caducidad");
        foreach (var r in reagentsList.OrderBy(r => r.InternalCode ?? r.Name))
        {
            foreach (var lot in r.AvailableLots.OrderBy(l => l.ExpiryDate))
            {
                var expStr = lot.ExpiryDate.HasValue ? lot.ExpiryDate.Value.ToString("dd/MM/yyyy") : "N/A";
                sb.AppendLine($"\"{r.InternalCode ?? ""}\";{EscCsv(r.Name)};{EscCsv(r.Fluorescence)};{EscCsv(r.ReagentType)};{EscCsv(r.SupplierName)};{EscCsv(lot.LotNumber)};{lot.Qty:N0};{expStr}");
            }
            if (!r.AvailableLots.Any())
                sb.AppendLine($"\"{r.InternalCode ?? ""}\";{EscCsv(r.Name)};{EscCsv(r.Fluorescence)};{EscCsv(r.ReagentType)};{EscCsv(r.SupplierName)};;;(sin stock)");
        }
        return sb.ToString();
    }

    // ── 2. Movements (Entries / Exits) ────────────────────────────────────────

    public static string BuildMovementsHtml(List<InventoryMovementDto> data, string title, string subtitle)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table><thead><tr>");
        foreach (var h in new[] { "Fecha", "Reactivo", "Fluorescencia", "Lote", "Tipo", "Cantidad", "Motivo" })
            sb.Append("<th>").Append(h).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var m in data.OrderByDescending(x => x.MovedAt))
        {
            var isIn = m.AdjustmentType is "IN" or "RETURN";
            var qty = isIn ? $"+{m.Qty:N0}" : $"-{Math.Abs(m.Qty):N0}";
            var qClass = isIn ? "badge-ok" : "badge-danger";
            sb.Append("<tr>")
              .Append("<td>").Append(m.MovedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")).Append("</td>")
              .Append("<td>").Append(Enc(m.ReagentName)).Append("</td>")
              .Append("<td>").Append(Enc(m.Fluorescence)).Append("</td>")
              .Append("<td>").Append(Enc(m.LotNumber) ?? "—").Append("</td>")
              .Append("<td>").Append(m.AdjustmentType).Append("</td>")
              .Append("<td class='").Append(qClass).Append(" text-right'>").Append(qty).Append("</td>")
              .Append("<td>").Append(Enc(m.Reason)).Append("</td>")
              .Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return WrapPrintHtml(title, sb.ToString(), subtitle);
    }

    public static string BuildMovementsCsv(List<InventoryMovementDto> data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Fecha;Reactivo;Fluorescencia;Lote;Tipo Movimiento;Cantidad;Motivo");
        foreach (var m in data.OrderByDescending(x => x.MovedAt))
        {
            var qty = m.AdjustmentType is "IN" or "RETURN" ? m.Qty : -Math.Abs(m.Qty);
            sb.AppendLine($"{m.MovedAt.ToLocalTime():dd/MM/yyyy HH:mm};{EscCsv(m.ReagentName)};{EscCsv(m.Fluorescence)};{EscCsv(m.LotNumber)};{m.AdjustmentType};{qty:N2};{EscCsv(m.Reason)}");
        }
        return sb.ToString();
    }

    // ── 3. Order List ──────────────────────────────────────────────────────────

    public static string BuildOrderListHtml(List<ReagentListDto> toOrder)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table><thead><tr>");
        foreach (var h in new[] { "Código", "Nombre", "Fluorescencia", "Tipo", "Proveedor", "Stock Actual", "Stock Objetivo", "Uds. a Pedir" })
            sb.Append("<th>").Append(h).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var r in toOrder)
        {
            var udsAPedir = Math.Max(0m, r.TargetStock - r.TotalStock);
            var stockClass = r.TotalStock <= 0 ? "badge-danger" : "badge-warn";
            sb.Append("<tr>")
              .Append("<td>").Append(Enc(r.InternalCode) ?? "N/A").Append("</td>")
              .Append("<td><strong>").Append(Enc(r.Name)).Append("</strong></td>")
              .Append("<td>").Append(Enc(r.Fluorescence)).Append("</td>")
              .Append("<td>").Append(Enc(r.ReagentType)).Append("</td>")
              .Append("<td>").Append(Enc(r.SupplierName) ?? "—").Append("</td>")
              .Append("<td class='").Append(stockClass).Append(" text-right'>").Append(r.TotalStock.ToString("N0")).Append("</td>")
              .Append("<td class='text-right'>").Append(r.TargetStock.ToString("N0")).Append("</td>")
              .Append("<td class='badge-low text-right'>").Append(udsAPedir.ToString("N0")).Append("</td>")
              .Append("</tr>");
        }
        sb.Append("</tbody></table>");
        var totalUnits = toOrder.Sum(r => Math.Max(0m, r.TargetStock - r.TotalStock));
        var subtitle = $"{toOrder.Count} reactivos por reponer — {totalUnits:N0} unidades totales a pedir";
        return WrapPrintHtml("Listado de Pedidos", sb.ToString(), subtitle);
    }

    public static string BuildOrderListCsv(List<ReagentListDto> toOrder)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Código;Nombre;Fluorescencia;Tipo;Proveedor;Stock Actual;Stock Objetivo;Uds. a Pedir");
        foreach (var r in toOrder)
        {
            var udsAPedir = Math.Max(0m, r.TargetStock - r.TotalStock);
            sb.AppendLine($"\"{r.InternalCode ?? ""}\";{EscCsv(r.Name)};{EscCsv(r.Fluorescence)};{EscCsv(r.ReagentType)};{EscCsv(r.SupplierName)};{r.TotalStock:N0};{r.TargetStock:N0};{udsAPedir:N0}");
        }
        return sb.ToString();
    }

    // ── CSV / HTML escape helpers ──────────────────────────────────────────────

    public static string EscCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string? Enc(string? value)
        => string.IsNullOrEmpty(value) ? value : System.Net.WebUtility.HtmlEncode(value);

    // ── Barcode Scanner Helpers ───────────────────────────────────────────────

    public class BarcodeParseResult
    {
        public bool Success { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static BarcodeParseResult ParseBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return new BarcodeParseResult { Success = false, ErrorMessage = "Código de barras vacío" };
        }

        var normalized = barcode.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "").Trim();

        // 1. BD (Becton Dickinson) GS1 format check
        if (normalized.StartsWith("01") && normalized.Length >= 27)
        {
            // Standard BD layout check: 01 (GTIN, 14 chars) + 17 (Expiry, 6 chars) + 10 (Lot, variable)
            bool isStandardBD = normalized.Substring(16, 2) == "17" && normalized.Substring(24, 2) == "10";
            if (isStandardBD)
            {
                string reference = normalized.Substring(9, 6);
                string expiryStr = normalized.Substring(18, 6);
                string lot = normalized.Substring(26);

                if (TryParseExpiryDate(expiryStr, out DateTime expiryDate))
                {
                    return new BarcodeParseResult
                    {
                        Success = true,
                        Reference = reference,
                        Lot = lot,
                        ExpirationDate = expiryDate
                    };
                }
                else
                {
                    return new BarcodeParseResult
                    {
                        Success = false,
                        ErrorMessage = $"Fecha de caducidad incorrecta: {expiryStr}"
                    };
                }
            }

            // Fallback for general GS1 layout sequence: 01 + 17 + 10 anywhere
            string gtin = normalized.Substring(2, 14);
            string referenceFallback = normalized.Substring(9, 6);
            string remaining = normalized.Substring(16);

            if (remaining.StartsWith("17") && remaining.Length >= 8)
            {
                string expiryStr = remaining.Substring(2, 6);
                if (TryParseExpiryDate(expiryStr, out DateTime parsedExp))
                {
                    string rest = remaining.Substring(8);
                    if (rest.StartsWith("10") && rest.Length > 2)
                    {
                        string lot = rest.Substring(2);
                        return new BarcodeParseResult
                        {
                            Success = true,
                            Reference = referenceFallback,
                            Lot = lot,
                            ExpirationDate = parsedExp
                        };
                    }
                }
            }
        }

        // 2. BioLegend split format: whitespace separated, exactly 2 parts
        var parts = normalized.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return new BarcodeParseResult
            {
                Success = true,
                Reference = parts[0],
                Lot = parts[1],
                ExpirationDate = null
            };
        }

        return new BarcodeParseResult
        {
            Success = false,
            ErrorMessage = "Código de barras no reconocido. Debe ser BD o BioLegend."
        };
    }

    public static bool IsReferenceMatch(string barcodeRef, string reagentRef)
    {
        return NormalizeReference(barcodeRef) == NormalizeReference(reagentRef);
    }

    private static string NormalizeReference(string refStr)
    {
        if (string.IsNullOrEmpty(refStr)) return string.Empty;
        return refStr.Replace("-", "").Replace(" ", "").Trim().TrimStart('0').ToLowerInvariant();
    }

    private static bool TryParseExpiryDate(string yyMMdd, out DateTime date)
    {
        date = default;
        if (yyMMdd.Length != 6) return false;

        try
        {
            int yy = int.Parse(yyMMdd.Substring(0, 2));
            int mm = int.Parse(yyMMdd.Substring(2, 2));
            int dd = int.Parse(yyMMdd.Substring(4, 2));

            int year = 2000 + yy;
            date = new DateTime(year, mm, dd);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
