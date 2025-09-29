namespace AccessToSqlServerMigrator.App.Models;

public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<IndexInfo> Indexes { get; set; } = new();
    public List<RelationshipInfo> Relationships { get; set; } = new();
    public long RecordCount { get; set; }
}