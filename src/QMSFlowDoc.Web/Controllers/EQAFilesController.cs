using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Application.Services.EQA;
using QMSFlowDoc.Application.Services.Identity;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Web.Controllers
{
    [Authorize]
    [Route("api/eqa/files")]
    [ApiController]
    public class EQAFilesController : ControllerBase
    {
        private readonly IEQAService _eqaService;
        private readonly IPermissionService _permissionService;
        private readonly IWebHostEnvironment _env;

        public EQAFilesController(
            IEQAService eqaService,
            IPermissionService permissionService,
            IWebHostEnvironment env)
        {
            _eqaService = eqaService;
            _permissionService = permissionService;
            _env = env;
        }

        [HttpGet("report/{roundId}")]
        public async Task<IActionResult> DownloadReport(Guid roundId)
        {
            var perms = await _permissionService.GetPermissionsForUserAsync(User, "EQA");
            if (!perms.CanRead) return Forbid();

            var round = await _eqaService.GetRoundByIdAsync(roundId);
            if (round == null || string.IsNullOrEmpty(round.ReportFilePath) || string.IsNullOrEmpty(round.ReportFileName))
                return NotFound("Informe no encontrado o no cargado.");

            return ServeFile(round.ReportFilePath, round.ReportFileName);
        }

        [HttpGet("certificate/{roundId}")]
        public async Task<IActionResult> DownloadCertificate(Guid roundId)
        {
            var perms = await _permissionService.GetPermissionsForUserAsync(User, "EQA");
            if (!perms.CanRead) return Forbid();

            var round = await _eqaService.GetRoundByIdAsync(roundId);
            if (round == null || string.IsNullOrEmpty(round.CertificateFilePath) || string.IsNullOrEmpty(round.CertificateFileName))
                return NotFound("Certificado no encontrado o no cargado.");

            return ServeFile(round.CertificateFilePath, round.CertificateFileName);
        }

        [HttpGet("evidence/{enrollmentId}")]
        public async Task<IActionResult> DownloadEvidence(Guid enrollmentId)
        {
            var perms = await _permissionService.GetPermissionsForUserAsync(User, "EQA");
            if (!perms.CanRead) return Forbid();

            var enrollment = await _eqaService.GetEnrollmentByIdAsync(enrollmentId);
            if (enrollment == null || string.IsNullOrEmpty(enrollment.EvidenceFilePath) || string.IsNullOrEmpty(enrollment.EvidenceFileName))
                return NotFound("Justificante de inscripción no encontrado.");

            return ServeFile(enrollment.EvidenceFilePath, enrollment.EvidenceFileName);
        }

        private IActionResult ServeFile(string storedPath, string originalFileName)
        {
            string physicalPath;

            // Backwards compatibility for old paths starting with /uploads/eqa/
            if (storedPath.StartsWith("/uploads/eqa/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = storedPath.TrimStart('/');
                physicalPath = Path.Combine(_env.WebRootPath, relativePath);
            }
            else
            {
                physicalPath = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", "eqa", storedPath);
            }

            if (!System.IO.File.Exists(physicalPath))
                return NotFound("El archivo físico no se encuentra en el almacenamiento.");

            var mimeType = "application/pdf";
            return PhysicalFile(physicalPath, mimeType, originalFileName);
        }
    }
}
