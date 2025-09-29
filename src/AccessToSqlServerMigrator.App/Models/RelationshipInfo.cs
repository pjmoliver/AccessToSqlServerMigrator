namespace AccessToSqlServerMigrator.App.Models;

public class RelationshipInfo
{
    public string Name { get; set; } = string.Empty;
    public string ParentTable { get; set; } = string.Empty;
    public string ParentColumn { get; set; } = string.Empty;
    public string ChildTable { get; set; } = string.Empty;
    public string ChildColumn { get; set; } = string.Empty;
    public string DeleteRule { get; set; } = "NO ACTION";
    public string UpdateRule { get; set; } = "NO ACTION";
}