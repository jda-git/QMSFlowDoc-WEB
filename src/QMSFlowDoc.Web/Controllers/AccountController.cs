using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QMSFlowDoc.Web.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly QmsDbContext _context;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            QmsDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);
                
                if (result.Succeeded)
                {
                    await LogAuditAsync("LOGIN_SUCCESS", "ApplicationUser", user?.Id, $"Sesión iniciada correctamente para el usuario '{model.Username}'", "OK", user?.Id, model.Username);
                    return LocalRedirect(model.ReturnUrl ?? "/");
                }

                if (result.IsLockedOut)
                {
                    await LogAuditAsync("LOGIN_LOCKOUT", "ApplicationUser", user?.Id, $"Cuenta bloqueada por múltiples intentos fallidos para el usuario '{model.Username}'", "FAIL", user?.Id, model.Username);
                    return Redirect($"/login?error=Cuenta bloqueada temporalmente por múltiples intentos fallidos. Inténtelo en 15 minutos.");
                }
                
                await LogAuditAsync("LOGIN_FAILURE", "ApplicationUser", user?.Id, $"Intento de inicio de sesión fallido para el usuario '{model.Username}' (Credenciales incorrectas)", "FAIL", user?.Id, model.Username);
                return Redirect($"/login?error=Credenciales incorrectas");
            }

            return Redirect($"/login?error=Please provide username and password");
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name ?? "Anónimo";
            Guid? userId = null;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uId))
                userId = uId;

            await _signInManager.SignOutAsync();
            await LogAuditAsync("LOGOUT", "ApplicationUser", userId, $"Sesión cerrada para el usuario '{username}'", "OK", userId, username);
            return LocalRedirect("/login");
        }

        private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, string resultVal, Guid? userId, string userName)
        {
            var audit = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                UserId = userId ?? Guid.Empty,
                UserName = userName,
                Timestamp = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                Result = resultVal
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
    }

    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
