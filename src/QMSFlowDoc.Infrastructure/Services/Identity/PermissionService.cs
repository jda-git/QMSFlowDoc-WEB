using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Identity;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Identity
{
    public class PermissionService : IPermissionService
    {
        private readonly QmsDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(
            QmsDbContext context,
            RoleManager<ApplicationRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<List<RolePermissionDto>> GetRolePermissionsAsync()
        {
            var permissions = await _context.RolePermissions.ToListAsync();
            var roles = await _roleManager.Roles.ToListAsync();
            
            var result = new List<RolePermissionDto>();
            foreach (var rp in permissions)
            {
                var role = roles.FirstOrDefault(r => r.Id == rp.RoleId);
                if (role != null)
                {
                    result.Add(new RolePermissionDto(
                        rp.Id,
                        rp.RoleId,
                        role.Name ?? "",
                        rp.Section,
                        rp.CanRead,
                        rp.CanCreate,
                        rp.CanEdit,
                        rp.CanDelete,
                        rp.CanPrint,
                        rp.CanApprove
                    ));
                }
            }
            return result;
        }

        public async Task SaveRolePermissionsAsync(List<RolePermissionDto> permissions)
        {
            foreach (var dto in permissions)
            {
                var rp = await _context.RolePermissions.FindAsync(dto.Id);
                if (rp != null)
                {
                    rp.CanRead = dto.CanRead;
                    rp.CanCreate = dto.CanCreate;
                    rp.CanEdit = dto.CanEdit;
                    rp.CanDelete = dto.CanDelete;
                    rp.CanPrint = dto.CanPrint;
                    rp.CanApprove = dto.CanApprove;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<PermissionSettingDto> GetPermissionsForUserAsync(ClaimsPrincipal user, string section)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return new PermissionSettingDto(false, false, false, false, false, false);
            }

            var roleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            
            // Administrador has full access
            if (roleClaims.Contains("Administrador"))
            {
                return new PermissionSettingDto(true, true, true, true, true, true);
            }

            if (!roleClaims.Any())
            {
                return new PermissionSettingDto(false, false, false, false, false, false);
            }

            var permissions = await (from rp in _context.RolePermissions
                                     join r in _context.Roles on rp.RoleId equals r.Id
                                     where rp.Section == section && roleClaims.Contains(r.Name)
                                     select rp).ToListAsync();

            if (!permissions.Any())
            {
                return new PermissionSettingDto(false, false, false, false, false, false);
            }

            bool canRead = permissions.Any(p => p.CanRead);
            bool canCreate = permissions.Any(p => p.CanCreate);
            bool canEdit = permissions.Any(p => p.CanEdit);
            bool canDelete = permissions.Any(p => p.CanDelete);
            bool canPrint = permissions.Any(p => p.CanPrint);
            bool canApprove = permissions.Any(p => p.CanApprove);

            return new PermissionSettingDto(canRead, canCreate, canEdit, canDelete, canPrint, canApprove);
        }
    }
}
