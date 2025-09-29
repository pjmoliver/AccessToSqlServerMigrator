using Microsoft.Extensions.Configuration;
using AccessToSqlServerMigrator.App.Configuration;
using AccessToSqlServerMigrator.App.Services;

namespace AccessToSqlServerMigrator.App;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Access to SQL Server Migration Tool");
            Console.WriteLine("=====================================\n");

            // Build configuration
            var basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                          ?? Directory.GetCurrentDirectory();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Bind configuration sections
            var connectionStrings = new ConnectionStrings();
            configuration.GetSection("ConnectionStrings").Bind(connectionStrings);

            var migrationSettings = new MigrationSettings();
            configuration.GetSection("MigrationSettings").Bind(migrationSettings);

            // Validate configuration
            if (string.IsNullOrWhiteSpace(connectionStrings.AccessDatabase))
            {
                Console.WriteLine("Error: AccessDatabase connection string is not configured.");
                Console.WriteLine("Please check your appsettings.json file.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(connectionStrings.SqlServerDatabase))
            {
                Console.WriteLine("Error: SqlServerDatabase connection string is not configured.");
                Console.WriteLine("Please check your appsettings.json file.");
                return 1;
            }

            // Display configuration summary
            Console.WriteLine("Configuration loaded:");
            Console.WriteLine($"  Access DB: {MaskConnectionString(connectionStrings.AccessDatabase)}");
            Console.WriteLine($"  SQL Server DB: {MaskConnectionString(connectionStrings.SqlServerDatabase)}");
            Console.WriteLine($"  Tables to migrate: {(migrationSettings.TablesToMigrate?.Any() == true ? string.Join(", ", migrationSettings.TablesToMigrate) : "All tables")}");
            Console.WriteLine($"  Batch size: {migrationSettings.BatchSize}");
            Console.WriteLine();

            // Create services
            var accessService = new AccessDatabaseService(connectionStrings.AccessDatabase);
            var sqlServerService = new SqlServerService(connectionStrings.SqlServerDatabase);
            var migrationService = new MigrationService(accessService, sqlServerService, migrationSettings);

            // Run migration
            var success = await migrationService.MigrateAsync();
            
            if (success)
            {
                Console.WriteLine("\nMigration completed successfully!");
                return 0;
            }
            else
            {
                Console.WriteLine("\nMigration failed. Please check the error messages above.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nUnexpected error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "(not configured)";

        // Mask sensitive information in connection strings
        var masked = connectionString;
        
        // Common patterns to mask
        var patterns = new[]
        {
            @"(Password|Pwd)\s*=\s*[^;]+",
            @"(User Id|UID)\s*=\s*[^;]+",
            @"(Data Source)\s*=\s*([^;]+)"
        };

        foreach (var pattern in patterns)
        {
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, 
                pattern, 
                match => 
                {
                    var key = match.Groups[1].Value;
                    if (key.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
                    {
                        // Show partial data source
                        var value = match.Groups[2].Value;
                        return value.Length > 10 ? $"{key}={value.Substring(0, 10)}..." : match.Value;
                    }
                    return $"{key}=***";
                }, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return masked;
    }
}
