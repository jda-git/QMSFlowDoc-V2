using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Api.Services;

/// <summary>
/// Centralized audit service for ISO 15189 compliance.
/// Logs all CRUD operations with before/after snapshots.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityType, Guid entityId, string details, 
                  object? beforeSnapshot = null, object? afterSnapshot = null, string? reason = null);
    
    Task LogAsync<T>(string action, T? beforeState, T? afterState, string? reason = null) where T : class;
    
    Task LogLoginEventAsync(string result, string username, string? details = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entityType, Guid entityId, string details,
                               object? beforeSnapshot = null, object? afterSnapshot = null, string? reason = null)
    {
        var user = GetCurrentUser();
        
        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = user.UserId,
            UserName = user.UserName,
            Details = details,
            Reason = reason,
            BeforeSnapshot = beforeSnapshot != null ? SerializeSnapshot(beforeSnapshot) : null,
            AfterSnapshot = afterSnapshot != null ? SerializeSnapshot(afterSnapshot) : null,
            MachineName = Environment.MachineName,
            Result = "OK",
            IntegrityHash = GenerateIntegrityHash(action, entityType, entityId, details)
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task LogAsync<T>(string action, T? beforeState, T? afterState, string? reason = null) where T : class
    {
        var entityType = typeof(T).Name;
        var entityId = GetEntityId(beforeState ?? afterState);
        var details = GenerateChangeDetails(action, beforeState, afterState);

        await LogAsync(action, entityType, entityId, details, beforeState, afterState, reason);
    }

    public async Task LogLoginEventAsync(string result, string username, string? details = null)
    {
        var user = GetCurrentUser();
        
        var auditLog = new AuditLog
        {
            Action = result == "OK" ? "LOGIN_OK" : "LOGIN_FAIL",
            EntityType = "User",
            EntityId = null,
            UserId = user.UserId,
            UserName = username,
            Details = details ?? $"Login attempt: {result}",
            Result = result,
            MachineName = Environment.MachineName,
            IntegrityHash = GenerateIntegrityHash("LOGIN", "User", null, username)
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    private (Guid? UserId, string UserName) GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return (null, "Anonymous");
        }

        var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext.User.Identity?.Name ?? "Unknown";
        
        Guid? userId = null;
        if (Guid.TryParse(userIdStr, out var parsedGuid))
        {
            userId = parsedGuid;
        }

        return (userId, userName);
    }

    private static string SerializeSnapshot(object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = false,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                MaxDepth = 5
            });
        }
        catch
        {
            return $"{{\"type\": \"{obj.GetType().Name}\", \"error\": \"Serialization failed\"}}";
        }
    }

    private static Guid GetEntityId<T>(T? entity) where T : class
    {
        if (entity == null) return Guid.Empty;
        
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty?.GetValue(entity) is Guid id)
        {
            return id;
        }
        
        return Guid.Empty;
    }

    private static string GenerateChangeDetails<T>(string action, T? before, T? after) where T : class
    {
        return action switch
        {
            "CREATE" => $"Nuevo {typeof(T).Name} creado",
            "UPDATE" => $"{typeof(T).Name} actualizado",
            "DELETE" => $"{typeof(T).Name} eliminado (soft-delete)",
            _ => $"Acción {action} en {typeof(T).Name}"
        };
    }

    private static string GenerateIntegrityHash(string action, string entityType, Guid? entityId, string details)
    {
        // Simple hash for ISO compliance - in production, use HMAC with secret key
        var data = $"{action}|{entityType}|{entityId}|{details}|{DateTime.UtcNow:yyyyMMddHHmmss}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}
