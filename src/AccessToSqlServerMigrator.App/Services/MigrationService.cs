using AccessToSqlServerMigrator.App.Configuration;
using AccessToSqlServerMigrator.App.Models;

namespace AccessToSqlServerMigrator.App.Services;

public class MigrationService
{
    private readonly AccessDatabaseService _accessService;
    private readonly SqlServerService _sqlServerService;
    private readonly MigrationSettings _settings;

    public MigrationService(
        AccessDatabaseService accessService,
        SqlServerService sqlServerService,
        MigrationSettings settings)
    {
        _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        _sqlServerService = sqlServerService ?? throw new ArgumentNullException(nameof(sqlServerService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<bool> MigrateAsync()
    {
        try
        {
            Console.WriteLine("Starting Access to SQL Server migration...");
            Console.WriteLine($"Batch size: {_settings.BatchSize}");
            Console.WriteLine($"Create indexes: {_settings.CreateIndexes}");
            Console.WriteLine($"Create foreign keys: {_settings.CreateForeignKeys}");
            Console.WriteLine();

            // Test connections first
            if (!await TestConnectionsAsync())
            {
                return false;
            }

            // Stage 1: Schema Analysis
            Console.WriteLine("=== Stage 1: Schema Analysis ===");
            var tableSchemas = await AnalyzeSchemaAsync();
            if (!tableSchemas.Any())
            {
                Console.WriteLine("No tables found to migrate.");
                return false;
            }

            // Stage 2: Table Creation
            Console.WriteLine("\n=== Stage 2: Table Creation ===");
            await CreateTablesAsync(tableSchemas);

            // Stage 3: Data Migration
            Console.WriteLine("\n=== Stage 3: Data Migration ===");
            await MigrateDataAsync(tableSchemas);

            // Stage 4: Index Creation
            if (_settings.CreateIndexes)
            {
                Console.WriteLine("\n=== Stage 4: Index Creation ===");
                await CreateIndexesAsync(tableSchemas);
            }
            else
            {
                Console.WriteLine("\n=== Stage 4: Index Creation (Skipped) ===");
            }

            // Stage 5: Relationship Establishment
            if (_settings.CreateForeignKeys)
            {
                Console.WriteLine("\n=== Stage 5: Relationship Establishment ===");
                await CreateForeignKeysAsync();
            }
            else
            {
                Console.WriteLine("\n=== Stage 5: Relationship Establishment (Skipped) ===");
            }

            Console.WriteLine("\n=== Migration Completed Successfully ===");
            PrintMigrationSummary(tableSchemas);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nMigration failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private async Task<bool> TestConnectionsAsync()
    {
        Console.WriteLine("Testing database connections...");

        try
        {
            // Test Access connection by getting table names
            var accessTables = await _accessService.GetTableNamesAsync();
            Console.WriteLine($"✓ Access database connected successfully. Found {accessTables.Count} tables.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to connect to Access database: {ex.Message}");
            return false;
        }

        try
        {
            // Test SQL Server connection
            var connected = await _sqlServerService.TestConnectionAsync();
            if (connected)
            {
                Console.WriteLine("✓ SQL Server database connected successfully.");
            }
            else
            {
                Console.WriteLine("✗ Failed to connect to SQL Server database.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to connect to SQL Server database: {ex.Message}");
            return false;
        }

        return true;
    }

    private async Task<List<TableSchema>> AnalyzeSchemaAsync()
    {
        var allTables = await _accessService.GetTableNamesAsync();
        
        // Filter tables based on configuration
        var tablesToMigrate = _settings.TablesToMigrate?.Any() == true 
            ? allTables.Where(t => _settings.TablesToMigrate.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList()
            : allTables;

        if (!tablesToMigrate.Any())
        {
            Console.WriteLine("No tables match the migration criteria.");
            return new List<TableSchema>();
        }

        Console.WriteLine($"Found {tablesToMigrate.Count} tables to migrate:");
        foreach (var tableName in tablesToMigrate)
        {
            Console.WriteLine($"  - {tableName}");
        }

        var tableSchemas = new List<TableSchema>();

        foreach (var tableName in tablesToMigrate)
        {
            try
            {
                Console.WriteLine($"Analyzing table: {tableName}");
                var schema = await _accessService.GetTableSchemaAsync(tableName);
                tableSchemas.Add(schema);

                Console.WriteLine($"  Columns: {schema.Columns.Count}");
                Console.WriteLine($"  Indexes: {schema.Indexes.Count}");
                Console.WriteLine($"  Records: {schema.RecordCount:N0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not analyze table {tableName}: {ex.Message}");
            }
        }

        return tableSchemas;
    }

    private async Task CreateTablesAsync(List<TableSchema> tableSchemas)
    {
        foreach (var tableSchema in tableSchemas)
        {
            try
            {
                // Check if table already exists
                if (await _sqlServerService.TableExistsAsync(tableSchema.Name))
                {
                    Console.WriteLine($"Table {tableSchema.Name} already exists. Dropping and recreating...");
                    await _sqlServerService.DropTableAsync(tableSchema.Name);
                }

                await _sqlServerService.CreateTableAsync(tableSchema);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating table {tableSchema.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private async Task MigrateDataAsync(List<TableSchema> tableSchemas)
    {
        foreach (var tableSchema in tableSchemas)
        {
            try
            {
                Console.WriteLine($"\nMigrating data for table: {tableSchema.Name}");
                
                if (tableSchema.RecordCount == 0)
                {
                    Console.WriteLine($"  No records to migrate for {tableSchema.Name}");
                    continue;
                }

                var data = await _accessService.GetTableDataAsync(tableSchema.Name);
                
                if (data.Rows.Count > 0)
                {
                    await _sqlServerService.InsertDataAsync(
                        tableSchema.Name, 
                        data, 
                        tableSchema.Columns, 
                        _settings.BatchSize);
                }
                else
                {
                    Console.WriteLine($"  No data retrieved for {tableSchema.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrating data for table {tableSchema.Name}: {ex.Message}");
                
                // Continue with other tables instead of failing completely
                Console.WriteLine("Continuing with next table...");
            }
        }
    }

    private async Task CreateIndexesAsync(List<TableSchema> tableSchemas)
    {
        foreach (var tableSchema in tableSchemas)
        {
            try
            {
                if (tableSchema.Indexes.Any())
                {
                    Console.WriteLine($"Creating indexes for table: {tableSchema.Name}");
                    await _sqlServerService.CreateIndexesAsync(tableSchema.Name, tableSchema.Indexes);
                }
                else
                {
                    Console.WriteLine($"No indexes to create for table: {tableSchema.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating indexes for table {tableSchema.Name}: {ex.Message}");
                // Continue with other tables
            }
        }
    }

    private async Task CreateForeignKeysAsync()
    {
        try
        {
            Console.WriteLine("Reading relationships from Access database...");
            var relationships = await _accessService.GetRelationshipsAsync();
            
            if (relationships.Any())
            {
                Console.WriteLine($"Found {relationships.Count} relationships to create:");
                foreach (var rel in relationships)
                {
                    Console.WriteLine($"  {rel.ChildTable}.{rel.ChildColumn} -> {rel.ParentTable}.{rel.ParentColumn}");
                }

                await _sqlServerService.CreateForeignKeysAsync(relationships);
            }
            else
            {
                Console.WriteLine("No relationships found in the Access database.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating foreign keys: {ex.Message}");
            // Don't fail the entire migration for foreign key issues
        }
    }

    private void PrintMigrationSummary(List<TableSchema> tableSchemas)
    {
        Console.WriteLine("\n=== Migration Summary ===");
        Console.WriteLine($"Tables migrated: {tableSchemas.Count}");
        
        var totalRecords = tableSchemas.Sum(t => t.RecordCount);
        Console.WriteLine($"Total records migrated: {totalRecords:N0}");
        
        var totalIndexes = tableSchemas.Sum(t => t.Indexes.Count);
        Console.WriteLine($"Indexes created: {(_settings.CreateIndexes ? totalIndexes : 0)}");
        
        Console.WriteLine($"Foreign keys: {(_settings.CreateForeignKeys ? "Enabled" : "Disabled")}");

        Console.WriteLine("\nMigrated tables:");
        foreach (var table in tableSchemas.OrderBy(t => t.Name))
        {
            Console.WriteLine($"  {table.Name}: {table.RecordCount:N0} records, {table.Columns.Count} columns");
        }
    }
}