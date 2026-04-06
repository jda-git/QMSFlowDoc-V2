using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

/// <summary>
/// Client-side connection settings for the V2 centralized architecture.
/// Stored locally at %AppData%\QMSFlowDoc\clientsettings.json
/// </summary>
public class ClientSettings
{
    /// <summary>SQL Server hostname or IP (e.g. "SERVIDOR" or "192.168.1.100")</summary>
    public string SqlServer { get; set; } = string.Empty;

    /// <summary>SQL Server instance name (e.g. "SQLEXPRESS"). Leave empty for default instance.</summary>
    public string SqlInstance { get; set; } = "SQLEXPRESS";

    /// <summary>Database name on the SQL Server.</summary>
    public string DatabaseName { get; set; } = "QMSFlowDoc";

    /// <summary>True = Windows Authentication. False = SQL Server authentication.</summary>
    public bool UseIntegratedSecurity { get; set; } = true;

    /// <summary>SQL Login username (only when UseIntegratedSecurity = false).</summary>
    public string? SqlUsername { get; set; }

    /// <summary>SQL Login password (only when UseIntegratedSecurity = false).</summary>
    public string? SqlPassword { get; set; }

    /// <summary>Connection timeout in seconds.</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 15;

    /// <summary>UNC path to the central document repository (e.g. "\\SERVIDOR\QMSFlowDoc").</summary>
    public string DocumentRepositoryPath { get; set; } = string.Empty;

    /// <summary>Whether the client has been configured at least once.</summary>
    public bool IsConfigured { get; set; } = false;

    /// <summary>
    /// Builds the SQL Server connection string from the stored settings.
    /// </summary>
    public string BuildConnectionString()
    {
        var server = string.IsNullOrWhiteSpace(SqlInstance)
            ? SqlServer
            : $"{SqlServer}\\{SqlInstance}";

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = DatabaseName,
            IntegratedSecurity = UseIntegratedSecurity,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = ConnectionTimeoutSeconds,
            ApplicationName = "QMSFlowDoc V2"
        };

        if (!UseIntegratedSecurity)
        {
            builder.UserID = SqlUsername ?? string.Empty;
            builder.Password = SqlPassword ?? string.Empty;
        }

        return builder.ConnectionString;
    }
}

/// <summary>
/// Service to load/save client settings from the local AppData folder.
/// </summary>
public static class ClientSettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QMSFlowDoc");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "clientsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Loads settings from disk. Returns defaults if file doesn't exist.</summary>
    public static ClientSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new ClientSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions) ?? new ClientSettings();
        }
        catch
        {
            return new ClientSettings();
        }
    }

    /// <summary>Saves settings to disk.</summary>
    public static void Save(ClientSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>Tests the SQL Server connection. Returns null on success, error message on failure.</summary>
    public static async Task<string?> TestConnectionAsync(ClientSettings settings)
    {
        try
        {
            var connStr = settings.BuildConnectionString();
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connStr);
            await connection.OpenAsync();
            return null; // Success
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Tests write access to the document repository folder. Returns null on success.</summary>
    public static Task<string?> TestDocumentPathAsync(ClientSettings settings)
    {
        try
        {
            var path = settings.DocumentRepositoryPath;
            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult<string?>("La ruta del repositorio documental está vacía.");

            if (!Directory.Exists(path))
                return Task.FromResult<string?>($"La carpeta no existe: {path}");

            // Test write access
            var testFile = Path.Combine(path, $"_write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return Task.FromResult<string?>(null); // Success
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }
    }
}
