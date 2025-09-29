namespace AccessToSqlServerMigrator.App.Configuration;

public class MigrationSettings
{
    public List<string> TablesToMigrate { get; set; } = new();
    public bool CreateIndexes { get; set; } = true;
    public bool CreateForeignKeys { get; set; } = true;
    public int BatchSize { get; set; } = 1000;
    public string LogLevel { get; set; } = "Information";
}