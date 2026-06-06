using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Application.Services.Documents;
using QMSFlowDoc.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace QMSFlowDoc.Web.Controllers
{
    [Authorize]
    [Route("api/documents")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IPdfWatermarkService _watermarkService;

        public DocumentsController(IDocumentService documentService, IPdfWatermarkService watermarkService)
        {
            _documentService = documentService;
            _watermarkService = watermarkService;
        }

        /// <summary>
        /// Obtiene el documento PDF para vista en pantalla (Marca de agua: CONTROLADO)
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id}/view")]
        public async Task<IActionResult> ViewDocument(Guid id)
        {
            try
            {
                var doc = await _documentService.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound("Documento no encontrado.");

                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrent);
                if (currentVersion == null) return NotFound("El documento no tiene versión actual activa.");

                var fileBytes = await _documentService.GetFileContentAsync(id);
                if (fileBytes == null) return NotFound("Archivo no encontrado en el almacenamiento.");

                // Marca de agua: CONTROLADO para visualización en pantalla
                var watermarkedBytes = await _watermarkService.PrepareForScreenViewAsync(
                    fileBytes,
                    doc.DocCode,
                    currentVersion.VersionLabel,
                    doc.Status.ToString(),
                    doc.NextReviewDue);

                Response.Headers.Append("Content-Disposition", $"inline; filename=\"VIEW_{currentVersion.FileName}\"");
                return File(watermarkedBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al cargar documento para visualización: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene el documento PDF para descarga/impresión (Marca de agua: NO CONTROLADO)
        /// </summary>
        [HttpGet("{id}/print")]
        public async Task<IActionResult> PrintDocument(Guid id)
        {
            try
            {
                var doc = await _documentService.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound("Documento no encontrado.");

                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrent);
                if (currentVersion == null) return NotFound("El documento no tiene versión actual activa.");

                var fileBytes = await _documentService.GetFileContentAsync(id);
                if (fileBytes == null) return NotFound("Archivo no encontrado en el almacenamiento.");

                // Marca de agua: NO CONTROLADO para exportar/imprimir
                var watermarkedBytes = await _watermarkService.PrepareForExportAsync(
                    fileBytes,
                    currentVersion.VersionLabel,
                    DateTime.Now);

                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{currentVersion.FileName}\"");
                return File(watermarkedBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al exportar documento: {ex.Message}");
            }
        }
    }
}
