using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;
using QMSFlowDoc.DocumentStorage;
using QMSFlowDoc.DocumentStorage.Helpers;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.MigrationTool;

/// <summary>
/// Migrates data from QMSFlowDoc V1 (SQLite + local files) to V2 (SQL Server + central repo).
/// Usage: QMSFlowDoc.MigrationTool.exe --sqlite "path/to/v1.db" --docs "path/to/v1/docs"
///        --connection "Server=...;Database=QMSFlowDoc;..."  --repo "\\SERVER\QMSFlowDoc"
/// </summary>
public class Program
{
    // Migration statistics
    private static readonly Dictionary<string, (int Inserted, int Failed)> Stats = new();
    private static readonly List<string> Warnings = new();
    private static int docsCopied = 0, docsNotFound = 0;

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("  QMSFlowDoc V1 → V2 Migration Tool");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine();

        // Parse arguments
        var sqlitePath = GetArg(args, "--sqlite");
        var docsPath = GetArg(args, "--docs");
        var connectionString = GetArg(args, "--connection");
        var repoPath = GetArg(args, "--repo");

        if (string.IsNullOrEmpty(sqlitePath) || string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  QMSFlowDoc.MigrationTool --sqlite <path-to-v1-db>");
            Console.WriteLine("                            --docs <path-to-v1-docs>");
            Console.WriteLine("                            --connection <sql-server-connection-string>");
            Console.WriteLine("                            --repo <path-to-v2-doc-repository>");
            return 1;
        }

        if (!File.Exists(sqlitePath))
        {
            Console.WriteLine($"ERROR: SQLite file not found: {sqlitePath}");
            return 1;
        }

        Console.WriteLine($"Source SQLite:  {sqlitePath}");
        Console.WriteLine($"Source Docs:    {docsPath ?? "(not specified)"}");
        Console.WriteLine($"Target SQL:     {MaskConnectionString(connectionString)}");
        Console.WriteLine($"Target Repo:    {repoPath ?? "(not specified)"}");
        Console.WriteLine();

        try
        {
            // Open SQLite source
            var sqliteConn = new SqliteConnection($"Data Source={sqlitePath};Mode=ReadOnly");
            await sqliteConn.OpenAsync();

            // Open SQL Server target
            var options = new DbContextOptionsBuilder<QmsFlowDocDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var ctx = new QmsFlowDocDbContext(options);

            // Ensure schema exists
            Console.WriteLine("Applying EF Core migrations to target database...");
            await ctx.Database.MigrateAsync();
            Console.WriteLine("✅ Schema ready.");
            Console.WriteLine();

            // ──────────────────────────────────────
            // Migrate each table
            // ──────────────────────────────────────
            await MigrateTableAsync(sqliteConn, ctx, "Users", "Users", reader => new User
            {
                Id = reader.GetGuid("Id"),
                Username = reader.GetString("Username"),
                FullName = reader.GetString("FullName"),
                Email = reader.GetStringOrNull("Email"),
                PasswordHash = reader.GetString("PasswordHash"),
                IsActive = reader.GetBool("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedAt = reader.GetDateTime("UpdatedAt")
            }, async (ctx2, user) => { ctx2.Users.Add(user); await ctx2.SaveChangesAsync(); });

            await MigrateTableAsync(sqliteConn, ctx, "Roles", "Roles", reader => new Role
            {
                Id = reader.GetGuid("Id"),
                RoleName = reader.GetString("RoleName"),
                Description = reader.GetStringOrNull("Description")
            }, async (ctx2, role) => { ctx2.Roles.Add(role); await ctx2.SaveChangesAsync(); });

            await MigrateTableAsync(sqliteConn, ctx, "Documents", "Documents", reader => new Document
            {
                Id = reader.GetGuid("Id"),
                DocCode = reader.GetString("DocCode"),
                Title = reader.GetString("Title"),
                Status = Enum.TryParse<DocumentStatus>(reader.GetStringOrNull("Status"), out var ds) ? ds : DocumentStatus.DRAFT,
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedAt = reader.GetDateTime("UpdatedAt")
            }, async (ctx2, doc) => { ctx2.Documents.Add(doc); await ctx2.SaveChangesAsync(); });

            await MigrateTableAsync(sqliteConn, ctx, "Equipments", "Equipments", reader => new Equipment
            {
                Id = reader.GetGuid("Id"),
                Name = reader.GetString("Name"),
                Manufacturer = reader.GetStringOrNull("Manufacturer"),
                Model = reader.GetStringOrNull("Model"),
                SerialNumber = reader.GetStringOrNull("SerialNumber"),
                Location = reader.GetStringOrNull("Location"),
                InternalId = reader.GetStringOrNull("InternalId"),
                Status = Enum.TryParse<EquipmentStatus>(reader.GetStringOrNull("Status"), out var es) ? es : EquipmentStatus.ACTIVE
            }, async (ctx2, eq) => { ctx2.Equipments.Add(eq); await ctx2.SaveChangesAsync(); });

            await MigrateTableAsync(sqliteConn, ctx, "Suppliers", "Suppliers", reader => new Supplier
            {
                Id = reader.GetGuid("Id"),
                Name = reader.GetString("Name"),
                ContactName = reader.GetStringOrNull("ContactName"),
                Email = reader.GetStringOrNull("Email"),
                Phone = reader.GetStringOrNull("Phone")
            }, async (ctx2, s) => { ctx2.Suppliers.Add(s); await ctx2.SaveChangesAsync(); });

            await MigrateTableAsync(sqliteConn, ctx, "Reagents", "Reagents", reader => new Reagent
            {
                Id = reader.GetGuid("Id"),
                Name = reader.GetString("Name"),
                Manufacturer = reader.GetStringOrNull("Manufacturer"),
                ReagentType = reader.GetStringOrNull("ReagentType") ?? string.Empty,
                Reference = reader.GetStringOrNull("Reference") ?? string.Empty
            }, async (ctx2, r) => { ctx2.Reagents.Add(r); await ctx2.SaveChangesAsync(); });

            // Copy documents if paths provided
            if (!string.IsNullOrEmpty(docsPath) && !string.IsNullOrEmpty(repoPath) && Directory.Exists(docsPath))
            {
                Console.WriteLine();
                Console.WriteLine("Copying document files...");
                await CopyDocumentsAsync(docsPath, repoPath);
            }

            sqliteConn.Close();

            // Print report
            PrintReport();

            // Save JSON report
            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), $"migration_report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var report = new
            {
                Timestamp = DateTime.UtcNow,
                SourceSqlite = sqlitePath,
                TargetConnection = MaskConnectionString(connectionString),
                Tables = Stats.Select(kv => new { Table = kv.Key, kv.Value.Inserted, kv.Value.Failed }),
                DocumentsCopied = docsCopied,
                DocumentsNotFound = docsNotFound,
                Warnings
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"\n📄 Report saved: {reportPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 2;
        }
    }

    private static async Task MigrateTableAsync<T>(
        SqliteConnection source, QmsFlowDocDbContext ctx,
        string sourceTable, string targetTable,
        Func<SqliteDataReader, T> mapper,
        Func<QmsFlowDocDbContext, T, Task> inserter) where T : class
    {
        Console.Write($"  Migrating {sourceTable}... ");
        int inserted = 0, failed = 0;

        try
        {
            using var cmd = source.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {sourceTable}";
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                try
                {
                    var entity = mapper((SqliteDataReader)reader);
                    await inserter(ctx, entity);
                    inserted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Warnings.Add($"{sourceTable}: Row failed - {ex.Message}");
                    ctx.ChangeTracker.Clear();
                }
            }
        }
        catch (SqliteException ex)
        {
            Warnings.Add($"{sourceTable}: Table not found or read error - {ex.Message}");
        }

        Stats[targetTable] = (inserted, failed);
        Console.WriteLine($"{inserted} OK, {failed} failed");
    }

    private static async Task CopyDocumentsAsync(string sourcePath, string destPath)
    {
        foreach (var file in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var destFile = Path.Combine(destPath, "Documentos", "Versiones", relativePath);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                File.Copy(file, destFile, overwrite: false);
                docsCopied++;
            }
            catch (Exception ex)
            {
                docsNotFound++;
                Warnings.Add($"Doc copy failed: {file} - {ex.Message}");
            }
        }
        Console.WriteLine($"  {docsCopied} files copied, {docsNotFound} failures");
        await Task.CompletedTask;
    }

    private static void PrintReport()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("  MIGRATION REPORT");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"  {"Table",-25} {"Inserted",10} {"Failed",10}");
        Console.WriteLine(new string('─', 50));
        foreach (var kv in Stats)
            Console.WriteLine($"  {kv.Key,-25} {kv.Value.Inserted,10} {kv.Value.Failed,10}");
        Console.WriteLine(new string('─', 50));
        Console.WriteLine($"  {"Documents Copied",-25} {docsCopied,10}");
        Console.WriteLine($"  {"Documents Not Found",-25} {docsNotFound,10}");
        if (Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚠ WARNINGS:");
            foreach (var w in Warnings)
                Console.WriteLine($"  • {w}");
        }
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static string MaskConnectionString(string cs) =>
        System.Text.RegularExpressions.Regex.Replace(cs, @"Password=[^;]+", "Password=***");
}

/// <summary>Extension methods for reading nullable columns from SQLite.</summary>
internal static class SqliteReaderExtensions
{
    public static Guid GetGuid(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal)) return Guid.NewGuid();
        var val = reader.GetString(ordinal);
        return Guid.TryParse(val, out var g) ? g : Guid.NewGuid();
    }

    public static string GetString(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static string? GetStringOrNull(this SqliteDataReader reader, string name)
    {
        try
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch { return null; }
    }

    public static bool GetBool(this SqliteDataReader reader, string name)
    {
        try
        {
            var ordinal = reader.GetOrdinal(name);
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }
        catch { return false; }
    }

    public static DateTime GetDateTime(this SqliteDataReader reader, string name)
    {
        try
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? DateTime.UtcNow : reader.GetDateTime(ordinal);
        }
        catch { return DateTime.UtcNow; }
    }
}
