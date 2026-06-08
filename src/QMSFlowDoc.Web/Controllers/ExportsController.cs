using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Application.Services.Identity;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;

namespace QMSFlowDoc.Web.Controllers
{
    [Authorize]
    [Route("api/exports")]
    [ApiController]
    public class ExportsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IPermissionService _permissionService;
        private readonly QmsDbContext _context;

        public ExportsController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IPermissionService permissionService,
            QmsDbContext context)
        {
            _configuration = configuration;
            _environment = environment;
            _permissionService = permissionService;
            _context = context;
        }

        [HttpGet("{fileName}")]
        public async Task<IActionResult> Download(string fileName)
        {
            var permissions = await _permissionService.GetPermissionsForUserAsync(User, "Quality");
            if (!permissions.CanRead)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(fileName) ||
                Path.GetFileName(fileName) != fileName ||
                !IsAllowedExportExtension(fileName))
            {
                return BadRequest("Nombre de informe no valido.");
            }

            var exportDirectory = GetExportDirectory();
            var fullPath = Path.Combine(exportDirectory, fileName);
            var resolvedDirectory = Path.GetFullPath(exportDirectory);
            if (!resolvedDirectory.EndsWith(Path.DirectorySeparatorChar))
            {
                resolvedDirectory += Path.DirectorySeparatorChar;
            }

            var resolvedPath = Path.GetFullPath(fullPath);

            if (!resolvedPath.StartsWith(resolvedDirectory, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.File.Exists(resolvedPath))
            {
                return NotFound("Informe no encontrado.");
            }

            await LogExportAccessAsync(fileName);
            return PhysicalFile(resolvedPath, GetContentType(fileName), fileName);
        }

        private static bool IsAllowedExportExtension(string fileName)
        {
            return fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetContentType(string fileName)
        {
            return fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)
                ? "application/msword"
                : "text/html; charset=utf-8";
        }

        private string GetExportDirectory()
        {
            var configured = _configuration["ImprovementExports:Path"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            var documentRoot = _configuration["DocumentStorage:RootPath"];
            if (!string.IsNullOrWhiteSpace(documentRoot))
            {
                return Path.Combine(documentRoot, "Exports");
            }

            return Path.Combine(_environment.ContentRootPath, "App_Data", "Exports");
        }

        private async Task LogExportAccessAsync(string fileName)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = User.Identity?.Name ?? "Usuario web",
                Action = "DOWNLOAD",
                EntityType = "ImprovementExport",
                Details = $"Descarga de informe de mejora continua: {fileName}",
                Result = "Success",
                MachineName = Environment.MachineName
            });

            await _context.SaveChangesAsync();
        }
    }
}
