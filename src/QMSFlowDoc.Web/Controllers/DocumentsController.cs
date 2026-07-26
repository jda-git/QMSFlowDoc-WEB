using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Application.Services.Documents;
using QMSFlowDoc.Application.Services.Identity;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;

namespace QMSFlowDoc.Web.Controllers
{
    [Authorize]
    [Route("api/documents")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IPdfWatermarkService _watermarkService;
        private readonly IPermissionService _permissionService;
        private readonly QmsDbContext _context;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentService documentService,
            IPdfWatermarkService watermarkService,
            IPermissionService permissionService,
            QmsDbContext context,
            ILogger<DocumentsController> logger)
        {
            _documentService = documentService;
            _watermarkService = watermarkService;
            _permissionService = permissionService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el documento PDF para vista en pantalla (Marca de agua: CONTROLADO)
        /// </summary>
        [HttpGet("{id}/view")]
        public async Task<IActionResult> ViewDocument(Guid id)
        {
            try
            {
                var perms = await _permissionService.GetPermissionsForUserAsync(User, "Documents");
                if (!perms.CanRead) return Forbid();

                var doc = await _documentService.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound("Documento no encontrado.");

                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrent);
                if (currentVersion == null) return NotFound("El documento no tiene versión actual activa.");

                var fileBytes = await _documentService.GetFileContentAsync(id);
                if (fileBytes == null) return NotFound("Archivo no encontrado en el almacenamiento.");

                var watermarkedBytes = await _watermarkService.PrepareForScreenViewAsync(
                    fileBytes,
                    doc.DocCode,
                    currentVersion.VersionLabel,
                    doc.Status.ToString(),
                    doc.NextReviewDue);

                Response.Headers.Append("Content-Disposition", $"inline; filename=\"VIEW_{currentVersion.FileName}\"");
                await LogDocumentAccessAsync("VIEW", id, $"Visualizacion controlada del documento {doc.DocCode} - {doc.Title}");
                return File(watermarkedBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return InternalError(ex, "load the document for viewing");
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
                var perms = await _permissionService.GetPermissionsForUserAsync(User, "Documents");
                if (!perms.CanPrint && !perms.CanRead) return Forbid();

                var doc = await _documentService.GetDocumentByIdAsync(id);
                if (doc == null) return NotFound("Documento no encontrado.");

                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrent);
                if (currentVersion == null) return NotFound("El documento no tiene versión actual activa.");

                var fileBytes = await _documentService.GetFileContentAsync(id);
                if (fileBytes == null) return NotFound("Archivo no encontrado en el almacenamiento.");

                var watermarkedBytes = await _watermarkService.PrepareForExportAsync(
                    fileBytes,
                    currentVersion.VersionLabel,
                    DateTime.Now);

                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{currentVersion.FileName}\"");
                await LogDocumentAccessAsync("PRINT", id, $"Exportacion/impresion no controlada del documento {doc.DocCode} - {doc.Title}");
                return File(watermarkedBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return InternalError(ex, "export the document");
            }
        }

        private ObjectResult InternalError(Exception exception, string operation)
        {
            var incidentId = HttpContext.TraceIdentifier;
            _logger.LogError(exception, "Unable to {Operation}. IncidentId: {IncidentId}", operation, incidentId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"No se ha podido completar la operación. Indique el identificador de incidencia {incidentId} al soporte técnico.");
        }

        private async Task LogDocumentAccessAsync(string action, Guid documentId, string details)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = User.Identity?.Name ?? "Usuario web",
                Action = action,
                EntityType = "Document",
                EntityId = documentId,
                Details = details,
                Result = "Success",
                MachineName = Environment.MachineName
            });

            await _context.SaveChangesAsync();
        }
    }
}
