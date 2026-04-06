using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IPermissionsService
{
    Task EnsureSeedDataAsync();
    Task<List<Permission>> GetAllPermissionsAsync();
    Task<List<Role>> GetAllRolesAsync();
    Task<Role?> GetRoleByIdAsync(Guid id);
    Task<bool> UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds);
    Task<List<User>> GetAllUsersAsync();
    Task<bool> UpdateUserRolesAsync(Guid userId, List<Guid> roleIds);
    Task<bool> HasPermissionAsync(string permissionKey, Guid? currentUserId = null);
    Task<List<Permission>> GetPermissionsForRoleAsync(Guid roleId);
}

/// <summary>
/// V2: Permissions service using SQL Server via EF Core.
/// </summary>
public class PermissionsService : IPermissionsService
{
    private readonly ClientDbContextFactory _dbFactory;

    public PermissionsService(ClientDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task EnsureSeedDataAsync()
    {
        using var ctx = _dbFactory.CreateContext();

        if (await ctx.Roles.AnyAsync()) return; // Already seeded

        // Seed roles
        var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso completo" };
        var qualityRole = new Role { Id = Guid.NewGuid(), RoleName = "Responsable Calidad", Description = "Gestión de calidad" };
        var techRole = new Role { Id = Guid.NewGuid(), RoleName = "Técnico", Description = "Operaciones" };
        var userRole = new Role { Id = Guid.NewGuid(), RoleName = "Usuario", Description = "Solo lectura" };

        ctx.Roles.AddRange(adminRole, qualityRole, techRole, userRole);

        // Seed permissions
        var permissions = new[]
        {
            "documents.read", "documents.write", "documents.approve", "documents.delete",
            "equipment.read", "equipment.write", "equipment.delete",
            "inventory.read", "inventory.write", "inventory.delete",
            "quality.read", "quality.write", "quality.close",
            "staff.read", "staff.write",
            "admin.users", "admin.roles", "admin.settings",
            "reports.view", "reports.export"
        };

        foreach (var p in permissions)
        {
            var perm = new Permission { Id = Guid.NewGuid(), PermissionKey = p, Description = p };
            ctx.Permissions.Add(perm);
            // Admin gets all permissions
            adminRole.Permissions.Add(perm);
        }

        await ctx.SaveChangesAsync();
    }

    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Permissions.OrderBy(p => p.PermissionKey).ToListAsync();
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Roles.Include(r => r.Permissions).OrderBy(r => r.RoleName).ToListAsync();
    }



    public async Task<List<Permission>> GetPermissionsForRoleAsync(Guid roleId)
    {
        using var ctx = _dbFactory.CreateContext();
        var role = await ctx.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
        return role?.Permissions ?? new List<Permission>();
    }

    public async Task<Role?> GetRoleByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<bool> UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds)
    {
        using var ctx = _dbFactory.CreateContext();
        var role = await ctx.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
        if (role == null) return false;

        role.Permissions.Clear();
        var perms = await ctx.Permissions.Where(p => permissionIds.Contains(p.Id)).ToListAsync();
        foreach (var p in perms) role.Permissions.Add(p);

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Users.Include(u => u.Roles).OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<bool> UpdateUserRolesAsync(Guid userId, List<Guid> roleIds)
    {
        using var ctx = _dbFactory.CreateContext();
        var user = await ctx.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        user.Roles.Clear();
        var roles = await ctx.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync();
        foreach (var r in roles) user.Roles.Add(r);

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasPermissionAsync(string permissionKey, Guid? currentUserId = null)
    {
        if (currentUserId == null) return false;

        using var ctx = _dbFactory.CreateContext();
        return await ctx.Users
            .Where(u => u.Id == currentUserId)
            .SelectMany(u => u.Roles)
            .SelectMany(r => r.Permissions)
            .AnyAsync(p => p.PermissionKey == permissionKey);
    }
}
