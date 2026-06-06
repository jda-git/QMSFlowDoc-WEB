using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.Identity
{
    public record RolePermissionDto(Guid Id, Guid RoleId, string RoleName, string Section, bool CanRead, bool CanCreate, bool CanEdit, bool CanDelete, bool CanPrint, bool CanApprove);
    public record PermissionSettingDto(bool CanRead, bool CanCreate, bool CanEdit, bool CanDelete, bool CanPrint, bool CanApprove);

    public interface IPermissionService
    {
        Task<List<RolePermissionDto>> GetRolePermissionsAsync();
        Task SaveRolePermissionsAsync(List<RolePermissionDto> permissions);
        Task<PermissionSettingDto> GetPermissionsForUserAsync(ClaimsPrincipal user, string section);
    }
}
