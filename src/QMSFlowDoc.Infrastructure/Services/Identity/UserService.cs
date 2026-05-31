using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Identity;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Identity
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly QmsDbContext _context;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            QmsDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<List<UserDto>> GetUsersAsync()
        {
            var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            var result = new List<UserDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var mainRole = roles.FirstOrDefault() ?? "Ninguno";
                result.Add(new UserDto(
                    user.Id,
                    user.UserName ?? "",
                    user.Email ?? "",
                    user.FullName,
                    mainRole,
                    user.IsActive,
                    user.CreatedAt
                ));
            }
            return result;
        }

        public async Task<bool> CreateUserAsync(CreateUserRequest request)
        {
            var user = new ApplicationUser
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(request.Role))
                {
                    if (!await _roleManager.RoleExistsAsync(request.Role))
                    {
                        await _roleManager.CreateAsync(new ApplicationRole(request.Role));
                    }
                    await _userManager.AddToRoleAsync(user, request.Role);
                }
                return true;
            }
            return false;
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return false;

            user.Email = request.Email;
            user.FullName = request.FullName;
            user.IsActive = request.IsActive;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count == 0 || roles[0] != request.Role)
                {
                    await _userManager.RemoveFromRolesAsync(user, roles);
                    if (!string.IsNullOrEmpty(request.Role))
                    {
                        if (!await _roleManager.RoleExistsAsync(request.Role))
                        {
                            await _roleManager.CreateAsync(new ApplicationRole(request.Role));
                        }
                        await _userManager.AddToRoleAsync(user, request.Role);
                    }
                }
                return true;
            }
            return false;
        }

        public async Task<bool> ResetPasswordAsync(Guid id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return false;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded;
        }

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            return await _roleManager.Roles
                .Select(r => new RoleDto(r.Id, r.Name ?? "", r.Description))
                .ToListAsync();
        }

        public async Task<bool> CreateRoleAsync(string name, string? description)
        {
            if (await _roleManager.RoleExistsAsync(name)) return false;

            var role = new ApplicationRole(name)
            {
                Description = description
            };
            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                // Seed default empty permissions for this new role so they can be customized
                var sections = new[] { "Documents", "Inventory", "Staff", "Quality", "Equipment", "EQA", "Audit" };
                foreach (var section in sections)
                {
                    var rp = new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        RoleId = role.Id,
                        Section = section,
                        CanRead = true, // default read-only for safety
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false,
                        CanPrint = false
                    };
                    _context.RolePermissions.Add(rp);
                }
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}
