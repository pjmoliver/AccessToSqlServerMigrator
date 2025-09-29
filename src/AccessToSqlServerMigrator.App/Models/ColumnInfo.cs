namespace AccessToSqlServerMigrator.App.Models;

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string AccessDataType { get; set; } = string.Empty;
    public string SqlServerDataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public bool IsAutoNumber { get; set; }
    public bool IsPrimaryKey { get; set; }
    public object? DefaultValue { get; set; }
    public int OrdinalPosition { get; set; }
}