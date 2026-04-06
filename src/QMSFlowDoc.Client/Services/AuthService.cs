using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    Task<Guid?> RegisterAsync(RegisterRequest request);
    Task PurgeUsersAsync();
    Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
    Task<bool> ResetPasswordAsync(Guid userId, ResetPasswordRequest request);
    Task<bool> UnlockAccountAsync(Guid userId);
    Task<bool> NeedsBootstrapAsync();
    Task<bool> BootstrapAsync(RegisterRequest request);
    void Logout();
    string? CurrentToken { get; }
    string? CurrentUsername { get; }
    Guid? CurrentUserId { get; }
    List<string> CurrentRoles { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

/// <summary>
/// V2: Authentication service using SQL Server via EF Core.
/// Validates credentials directly against the Users table with BCrypt.
/// No HTTP fallback — all auth goes through the central database.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ClientDbContextFactory _dbFactory;

    public string? CurrentToken { get; private set; }
    public string? CurrentUsername { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public List<string> CurrentRoles { get; private set; } = new();
    public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentUsername);
    public bool IsAdmin => CurrentRoles.Contains("Administrador");

    public AuthService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            using var ctx = _dbFactory.CreateContext();
            var user = await ctx.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user == null) return false;

            // Check if account is locked
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
                return false;

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                await ctx.SaveChangesAsync();
                return false;
            }

            // Success
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();

            CurrentToken = $"v2_{user.Id}";
            CurrentUsername = user.Username;
            CurrentUserId = user.Id;
            CurrentRoles = user.Roles.Select(r => r.RoleName).ToList();

            // Set the user in the factory for audit logging
            _dbFactory.SetCurrentUser(user.Username);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Guid?> RegisterAsync(RegisterRequest request)
    {
        using var ctx = _dbFactory.CreateContext();

        // Check if username exists
        if (await ctx.Users.AnyAsync(u => u.Username == request.Username))
            throw new Exception($"El usuario '{request.Username}' ya existe.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assign role
        if (!string.IsNullOrEmpty(request.RoleName))
        {
            var role = await ctx.Roles.FirstOrDefaultAsync(r => r.RoleName == request.RoleName);
            if (role != null)
                user.Roles.Add(role);
        }

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    public async Task PurgeUsersAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        var users = await ctx.Users.ToListAsync();
        ctx.Users.RemoveRange(users);
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
    {
        if (CurrentUserId == null) return false;

        using var ctx = _dbFactory.CreateContext();
        var user = await ctx.Users.FindAsync(CurrentUserId.Value);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, ResetPasswordRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var user = await ctx.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnlockAccountAsync(Guid userId)
    {
        using var ctx = _dbFactory.CreateContext();
        var user = await ctx.Users.FindAsync(userId);
        if (user == null) return false;

        user.LockedUntil = null;
        user.FailedLoginAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> NeedsBootstrapAsync()
    {
        try
        {
            using var ctx = _dbFactory.CreateContext();
            return !await ctx.Users.AnyAsync();
        }
        catch { return true; } // DB not ready = needs bootstrap
    }

    public async Task<bool> BootstrapAsync(RegisterRequest request)
    {
        try
        {
            using var ctx = _dbFactory.CreateContext();

            // Ensure roles exist
            if (!await ctx.Roles.AnyAsync())
            {
                ctx.Roles.AddRange(
                    new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso completo al sistema" },
                    new Role { Id = Guid.NewGuid(), RoleName = "Responsable Calidad", Description = "Gestión de calidad y documentación" },
                    new Role { Id = Guid.NewGuid(), RoleName = "Técnico", Description = "Operaciones de laboratorio" },
                    new Role { Id = Guid.NewGuid(), RoleName = "Usuario", Description = "Acceso de solo lectura" }
                );
                await ctx.SaveChangesAsync();
            }

            var adminRole = await ctx.Roles.FirstOrDefaultAsync(r => r.RoleName == "Administrador");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (adminRole != null) user.Roles.Add(adminRole);

            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            return true;
        }
        catch { return false; }
    }

    public void Logout()
    {
        CurrentToken = null;
        CurrentUsername = null;
        CurrentUserId = null;
        CurrentRoles.Clear();
    }
}
