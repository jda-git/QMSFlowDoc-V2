using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;
using QMSFlowDoc.Data.Interceptors;

namespace QMSFlowDoc.Client.Services;

/// <summary>
/// Factory that creates QmsFlowDocDbContext instances for the client.
/// Each operation should use a fresh DbContext (short-lived scoped pattern)
/// to avoid stale data in a multi-user environment.
/// </summary>
public class ClientDbContextFactory
{
    private string? _connectionString;
    private string _currentUser = "System";

    /// <summary>Whether the factory has been configured with a valid connection string.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_connectionString);

    /// <summary>Set the current user for audit logging.</summary>
    public void SetCurrentUser(string username)
    {
        _currentUser = username;
    }

    /// <summary>
    /// Configures the factory with a connection string.
    /// Call this during startup after loading ClientSettings.
    /// </summary>
    public void Configure(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Configures the factory from ClientSettings model.
    /// </summary>
    public void Configure(ClientSettings settings)
    {
        _connectionString = settings.BuildConnectionString();
    }

    /// <summary>
    /// Creates a new DbContext instance.
    /// Each caller is responsible for disposing it (use 'using' pattern).
    /// Includes the AuditSaveChangesInterceptor for automatic audit logging.
    /// </summary>
    public QmsFlowDocDbContext CreateContext()
    {
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException(
                "ClientDbContextFactory not configured. Call Configure() first with a valid connection string.");

        var optionsBuilder = new DbContextOptionsBuilder<QmsFlowDocDbContext>();
        optionsBuilder.UseSqlServer(_connectionString, sqlOpts =>
        {
            sqlOpts.CommandTimeout(30);
            sqlOpts.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });

        // Add audit interceptor
        optionsBuilder.AddInterceptors(new AuditSaveChangesInterceptor(_currentUser));

        return new QmsFlowDocDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Tests the connection. Returns null on success, error message on failure.
    /// </summary>
    public async Task<string?> TestConnectionAsync()
    {
        try
        {
            using var ctx = CreateContext();
            await ctx.Database.CanConnectAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
