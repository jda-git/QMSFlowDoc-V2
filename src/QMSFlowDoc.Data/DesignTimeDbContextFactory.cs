using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace QMSFlowDoc.Data;

/// <summary>
/// Factory used by EF Core CLI tools (dotnet ef migrations add, etc.)
/// to create the DbContext at design time.
/// Reads connection string from appsettings.json in the Data project.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QmsFlowDocDbContext>
{
    public QmsFlowDocDbContext CreateDbContext(string[] args)
    {
        // Default connection string for migrations tooling
        var connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=QMSFlowDoc;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        // Try to read from appsettings.json if available
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(configPath))
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            connectionString = config.GetConnectionString("QMSFlowDoc") ?? connectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<QmsFlowDocDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(120);
        });

        return new QmsFlowDocDbContext(optionsBuilder.Options);
    }
}
