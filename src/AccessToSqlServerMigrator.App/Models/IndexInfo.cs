namespace AccessToSqlServerMigrator.App.Models;

public class IndexInfo
{
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<string> ColumnNames { get; set; } = new();
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsClustered { get; set; } = true;
}