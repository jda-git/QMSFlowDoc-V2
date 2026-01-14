using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.Validation;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QMSFlowDoc.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("needs-bootstrap")]
    public async Task<ActionResult<bool>> NeedsBootstrap()
    {
        return Ok(!await _context.Users.AnyAsync());
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult> Bootstrap(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync())
        {
            return BadRequest("El sistema ya ha sido inicializado.");
        }

        // Validate password policy
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.Password);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        // Ensure roles exist
        if (!await _context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso total" },
                new Role { Id = Guid.NewGuid(), RoleName = "Consultor", Description = "Solo lectura" },
                new Role { Id = Guid.NewGuid(), RoleName = "Staff", Description = "Acceso básico" }
            };
            _context.Roles.AddRange(roles);
            await _context.SaveChangesAsync();
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Administrador");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            PasswordChangedAt = DateTime.UtcNow,
            Roles = adminRole != null ? new List<Role> { adminRole } : new List<Role>()
        };

        _context.Users.Add(user);
        
        // Audit log for bootstrap
        await LogAuditAsync(user.Id, "BOOTSTRAP", "User", user.Id, "Sistema inicializado con primer administrador", "OK");
        
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Administrador creado correctamente." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // User not found - don't reveal if user exists
        if (user == null)
        {
            await LogAuditAsync(null, "LOGIN_FAIL", "Auth", null, $"Usuario no encontrado: {request.Username}", "FAIL");
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remainingMinutes = (int)(user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
            await LogAuditAsync(user.Id, "LOGIN_BLOCKED", "Auth", user.Id, $"Cuenta bloqueada. Intentar en {remainingMinutes} minutos.", "FAIL");
            return Unauthorized($"Cuenta bloqueada. Intente de nuevo en {remainingMinutes} minutos.");
        }

        // Check if user is inactive
        if (!user.IsActive)
        {
            await LogAuditAsync(user.Id, "LOGIN_INACTIVE", "Auth", user.Id, "Usuario desactivado intentó login", "FAIL");
            return Unauthorized("Esta cuenta ha sido desactivada.");
        }

        // Verify password
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            
            if (user.FailedLoginAttempts >= PasswordPolicy.MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(PasswordPolicy.LockoutMinutes);
                await LogAuditAsync(user.Id, "ACCOUNT_LOCKED", "Auth", user.Id, 
                    $"Cuenta bloqueada tras {PasswordPolicy.MaxFailedAttempts} intentos fallidos", "FAIL");
            }
            else
            {
                await LogAuditAsync(user.Id, "LOGIN_FAIL", "Auth", user.Id, 
                    $"Contraseña incorrecta. Intento {user.FailedLoginAttempts}/{PasswordPolicy.MaxFailedAttempts}", "FAIL");
            }
            
            await _context.SaveChangesAsync();
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        // Successful login - reset failed attempts
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        
        await LogAuditAsync(user.Id, "LOGIN_OK", "Auth", user.Id, "Login exitoso", "OK");
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return Ok(new LoginResponse(
            token,
            user.Username,
            user.FullName,
            user.Roles.Select(r => r.RoleName).ToList()
        ));
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterRequest request)
    {
        // Validate password policy
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.Password);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest("El nombre de usuario ya existe.");
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName.ToLower() == request.RoleName.Trim().ToLower());
        if (role == null)
        {
            role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Consultor");
            if (role == null)
            {
                role = await _context.Roles.FirstOrDefaultAsync();
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            PasswordChangedAt = DateTime.UtcNow,
            Roles = role != null ? new List<Role> { role } : new List<Role>()
        };

        _context.Users.Add(user);
        
        var currentUserId = GetCurrentUserId();
        await LogAuditAsync(currentUserId, "USER_CREATED", "User", user.Id, 
            $"Nuevo usuario creado: {request.Username} con rol {role?.RoleName ?? "ninguno"}", "OK");
        
        await _context.SaveChangesAsync();

        return Ok(new { Id = user.Id, Username = user.Username });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost("reset-password/{userId}")]
    public async Task<ActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound("Usuario no encontrado.");

        // Validate new password
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.NewPassword);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = true; // Force change on next login
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        var currentUserId = GetCurrentUserId();
        await LogAuditAsync(currentUserId, "PASSWORD_RESET", "User", userId, 
            $"Contraseña restablecida por administrador", "OK");

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Contraseña restablecida correctamente." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        // Verify current password
        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            await LogAuditAsync(userId, "PASSWORD_CHANGE_FAIL", "User", userId, 
                "Intento de cambio de contraseña con contraseña actual incorrecta", "FAIL");
            return BadRequest("La contraseña actual es incorrecta.");
        }

        // Validate new password
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.NewPassword);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;

        await LogAuditAsync(userId, "PASSWORD_CHANGED", "User", userId, "Contraseña cambiada por el usuario", "OK");
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Contraseña cambiada correctamente." });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost("unlock/{userId}")]
    public async Task<ActionResult> UnlockAccount(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound("Usuario no encontrado.");

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        var currentUserId = GetCurrentUserId();
        await LogAuditAsync(currentUserId, "ACCOUNT_UNLOCKED", "User", userId, 
            "Cuenta desbloqueada por administrador", "OK");

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Cuenta desbloqueada correctamente." });
    }

    /// <summary>
    /// Emergency reset password - requires secret token from appsettings.json
    /// Use this when no admin can log in to reset passwords
    /// </summary>
    [HttpPost("emergency-reset")]
    public async Task<ActionResult> EmergencyResetPassword([FromBody] EmergencyResetRequest request)
    {
        // Verify emergency token from configuration
        var configToken = _configuration["Security:EmergencyResetToken"];
        if (string.IsNullOrEmpty(configToken) || request.EmergencyToken != configToken)
        {
            await LogAuditAsync(null, "EMERGENCY_RESET_FAIL", "Auth", null, 
                $"Intento de reset de emergencia fallido para usuario: {request.Username}", "FAIL");
            return Unauthorized("Token de emergencia inválido.");
        }

        var user = await _context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        
        if (user == null)
        {
            return NotFound($"Usuario '{request.Username}' no encontrado.");
        }

        // Validate new password
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.NewPassword);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        // Reset password and unlock account
        user.PasswordHash = HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.IsActive = true;
        
        await LogAuditAsync(null, "EMERGENCY_RESET_OK", "User", user.Id, 
            $"Contraseña reseteada vía token de emergencia para: {request.Username}", "OK");
        
        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Contraseña de '{request.Username}' reseteada correctamente. La cuenta ha sido desbloqueada." });
    }

    [HttpPost("promote-to-admin")]
    public async Task<ActionResult> PromoteToAdmin([FromBody] PromoteToAdminRequest request)
    {
        var configToken = _configuration["Security:EmergencyResetToken"];
        if (string.IsNullOrEmpty(configToken) || request.EmergencyToken != configToken)
        {
            await LogAuditAsync(null, "PROMOTE_ADMIN_FAIL", "Auth", null, 
                $"Intento de promoción fallido para: {request.Username}", "FAIL");
            return Unauthorized("Token de emergencia inválido.");
        }

        var user = await _context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null) return NotFound($"Usuario '{request.Username}' no encontrado.");

        var roleName = "Administrador";
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
        
        if (adminRole == null) 
        {
            // Auto-create role if missing
             adminRole = new Role 
             { 
                 Id = Guid.NewGuid(), 
                 RoleName = roleName, 
                 Description = "Auto-created by Emergency Promote" 
             };
             _context.Roles.Add(adminRole);
             await _context.SaveChangesAsync(); // Save to get ID
        }

        if (!user.Roles.Any(r => r.RoleName == roleName))
        {
            user.Roles.Add(adminRole);
            user.IsActive = true;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            
            await LogAuditAsync(null, "PROMOTE_ADMIN_OK", "User", user.Id, 
                $"Usuario promovido a Administrador: {request.Username}", "OK");
            await _context.SaveChangesAsync();
        }

        return Ok(new { Message = $"Usuario '{request.Username}' es ahora Administrador." });
    }

    /// <summary>
    /// Purge all users and start fresh - requires emergency token
    /// </summary>
    [HttpPost("purge-and-reset")]
    public async Task<ActionResult> PurgeAndReset([FromBody] PurgeAndResetRequest request)
    {
        // Verify emergency token
        var configToken = _configuration["Security:EmergencyResetToken"];
        if (string.IsNullOrEmpty(configToken) || request.EmergencyToken != configToken)
        {
            await LogAuditAsync(null, "PURGE_RESET_FAIL", "Auth", null, 
                "Intento de purga fallido - token inválido", "FAIL");
            return Unauthorized("Token de emergencia inválido.");
        }

        // Validate new admin password
        var (isValid, errorMessage) = PasswordPolicy.Validate(request.AdminPassword);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        // Delete all users and related data
        _context.StaffAuthorizations.RemoveRange(_context.StaffAuthorizations);
        _context.StaffCompetencyStatuses.RemoveRange(_context.StaffCompetencyStatuses);
        _context.CompetencyEvaluations.RemoveRange(_context.CompetencyEvaluations);
        _context.StaffTrainings.RemoveRange(_context.StaffTrainings);
        _context.StaffProfiles.RemoveRange(_context.StaffProfiles);
        _context.Users.RemoveRange(_context.Users);
        
        await _context.SaveChangesAsync();

        // Ensure roles exist
        if (!await _context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso total" },
                new Role { Id = Guid.NewGuid(), RoleName = "Consultor", Description = "Solo lectura" },
                new Role { Id = Guid.NewGuid(), RoleName = "Staff", Description = "Acceso básico" }
            };
            _context.Roles.AddRange(roles);
            await _context.SaveChangesAsync();
        }

        // Create new admin
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Administrador");
        var newAdmin = new User
        {
            Id = Guid.NewGuid(),
            Username = request.AdminUsername,
            FullName = request.AdminFullName ?? "Administrador",
            Email = request.AdminEmail ?? "",
            PasswordHash = HashPassword(request.AdminPassword),
            PasswordChangedAt = DateTime.UtcNow,
            IsActive = true,
            Roles = adminRole != null ? new List<Role> { adminRole } : new List<Role>()
        };

        _context.Users.Add(newAdmin);
        
        await LogAuditAsync(newAdmin.Id, "PURGE_AND_RESET", "System", null, 
            $"Sistema purgado y reiniciado. Nuevo admin: {request.AdminUsername}", "OK");
        
        await _context.SaveChangesAsync();

        return Ok(new { 
            Message = $"Sistema reiniciado. Nuevo administrador '{request.AdminUsername}' creado.",
            UserId = newAdmin.Id 
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("purge-users")]
    public async Task<ActionResult> PurgeUsers()
    {
        // Delete all related records first to avoid FK issues
        _context.StaffAuthorizations.RemoveRange(_context.StaffAuthorizations);
        _context.StaffCompetencyStatuses.RemoveRange(_context.StaffCompetencyStatuses);
        _context.CompetencyEvaluations.RemoveRange(_context.CompetencyEvaluations);
        _context.StaffTrainings.RemoveRange(_context.StaffTrainings);
        _context.StaffProfiles.RemoveRange(_context.StaffProfiles);
        
        // Delete all users except 'admin'
        var nonAdminUsers = _context.Users.Where(u => u.Username != "admin");
        _context.Users.RemoveRange(nonAdminUsers);
        
        var currentUserId = GetCurrentUserId();
        await LogAuditAsync(currentUserId, "PURGE_USERS", "User", null, 
            "Todos los usuarios eliminados excepto admin", "OK");
        
        await _context.SaveChangesAsync();
        return Ok("Registros de personal y usuarios eliminados correctamente (excepto administrador).");
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.RoleName));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ISO 15189 compliant password hashing using BCrypt
    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    private bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Fallback for legacy SHA256 hashes during migration
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var legacyHash = Convert.ToBase64String(hashedBytes);
            return legacyHash == hash;
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdString != null ? Guid.Parse(userIdString) : null;
    }

    private async Task LogAuditAsync(Guid? userId, string action, string entityType, Guid? entityId, string details, string result)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserName = User.Identity?.Name ?? "Sistema",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Result = result,
            MachineName = Environment.MachineName
        };
        _context.AuditLogs.Add(audit);
    }
}

// DTOs for new endpoints
public record ResetPasswordRequest(string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record EmergencyResetRequest(string EmergencyToken, string Username, string NewPassword);
public record PromoteToAdminRequest(string EmergencyToken, string Username);
public record PurgeAndResetRequest(
    string EmergencyToken, 
    string AdminUsername, 
    string AdminPassword, 
    string? AdminFullName = null, 
    string? AdminEmail = null
);
