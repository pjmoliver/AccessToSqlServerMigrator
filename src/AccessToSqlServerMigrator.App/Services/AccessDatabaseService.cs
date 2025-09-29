using System.Data;
using System.Data.OleDb;
using AccessToSqlServerMigrator.App.Models;
using AccessToSqlServerMigrator.App.Extensions;

namespace AccessToSqlServerMigrator.App.Services;

public class AccessDatabaseService
{
    private readonly string _connectionString;

    public AccessDatabaseService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<string>> GetTableNamesAsync()
    {
        var tables = new List<string>();
        
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        
        var schema = connection.GetSchema("Tables");
        
        foreach (DataRow row in schema.Rows)
        {
            var tableName = row["TABLE_NAME"]?.ToString();
            var tableType = row["TABLE_TYPE"]?.ToString();
            
            // Only include user tables, exclude system tables
            if (!string.IsNullOrEmpty(tableName) && 
                tableType == "TABLE" && 
                !tableName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            {
                tables.Add(tableName);
            }
        }
        
        return tables.OrderBy(t => t).ToList();
    }

    public async Task<TableSchema> GetTableSchemaAsync(string tableName)
    {
        var tableSchema = new TableSchema { Name = tableName };
        
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        
        // Get columns information
        tableSchema.Columns = await GetColumnsAsync(connection, tableName);
        
        // Get indexes information
        tableSchema.Indexes = await GetIndexesAsync(connection, tableName);
        
        // Get record count
        tableSchema.RecordCount = await GetRecordCountAsync(connection, tableName);
        
        return tableSchema;
    }

    public async Task<List<RelationshipInfo>> GetRelationshipsAsync()
    {
        var relationships = new List<RelationshipInfo>();
        
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        
        try
        {
            // Try to get relationships from Access system tables
            var query = @"
                SELECT 
                    r.szRelationship as RelationName,
                    r.szReferencedObject as ParentTable,
                    r.szObject as ChildTable,
                    rc.szReferencedColumn as ParentColumn,
                    rc.szColumn as ChildColumn
                FROM MSysRelationships r 
                INNER JOIN MSysObjects o ON r.szObject = o.Name
                LEFT JOIN MSysObjects ro ON r.szReferencedObject = ro.Name
                LEFT JOIN (
                    SELECT szRelationship, szColumn, szReferencedColumn 
                    FROM MSysRelationships 
                    WHERE szColumn IS NOT NULL
                ) rc ON r.szRelationship = rc.szRelationship
                WHERE o.Type = 1 AND ro.Type = 1";
            
            using var command = new OleDbCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var relationship = new RelationshipInfo
                {
                    Name = reader["RelationName"]?.ToString() ?? string.Empty,
                    ParentTable = reader["ParentTable"]?.ToString() ?? string.Empty,
                    ChildTable = reader["ChildTable"]?.ToString() ?? string.Empty,
                    ParentColumn = reader["ParentColumn"]?.ToString() ?? string.Empty,
                    ChildColumn = reader["ChildColumn"]?.ToString() ?? string.Empty
                };
                
                if (!string.IsNullOrEmpty(relationship.ParentTable) && !string.IsNullOrEmpty(relationship.ChildTable))
                {
                    relationships.Add(relationship);
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't read system tables, log and continue
            Console.WriteLine($"Warning: Could not read relationships from Access database: {ex.Message}");
        }
        
        return relationships;
    }

    public async Task<DataTable> GetTableDataAsync(string tableName, int batchSize = 1000, int offset = 0)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        
        // Note: Access doesn't support OFFSET/LIMIT directly, so we'll get all data
        // and implement batching in the calling code
        var query = $"SELECT * FROM [{tableName}]";
        
        using var command = new OleDbCommand(query, connection);
        using var adapter = new OleDbDataAdapter(command);
        
        var dataTable = new DataTable();
        adapter.Fill(dataTable);
        
        return dataTable;
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(OleDbConnection connection, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        // Get columns from schema
        var schema = connection.GetSchema("Columns", new[] { null, null, tableName });
        
        foreach (DataRow row in schema.Rows)
        {
            var columnName = row["COLUMN_NAME"]?.ToString() ?? string.Empty;
            var accessDataType = GetAccessDataType(row);
            var maxLength = row["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value 
                ? Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]) 
                : (int?)null;
            
            var column = new ColumnInfo
            {
                Name = columnName,
                AccessDataType = accessDataType,
                SqlServerDataType = accessDataType.GetSqlServerDataTypeWithLength(maxLength),
                MaxLength = maxLength,
                IsNullable = row["IS_NULLABLE"]?.ToString() == "YES",
                IsAutoNumber = accessDataType.IsAutoNumber(),
                OrdinalPosition = row["ORDINAL_POSITION"] != DBNull.Value 
                    ? Convert.ToInt32(row["ORDINAL_POSITION"]) 
                    : 0
            };
            
            columns.Add(column);
        }
        
        // Get primary key information
        await SetPrimaryKeyInfoAsync(connection, tableName, columns);
        
        return columns.OrderBy(c => c.OrdinalPosition).ToList();
    }

    private Task<List<IndexInfo>> GetIndexesAsync(OleDbConnection connection, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        try
        {
            var schema = connection.GetSchema("Indexes", new[] { null, null, null, null, tableName });
            
            var indexGroups = schema.AsEnumerable()
                .GroupBy(row => row["INDEX_NAME"]?.ToString())
                .Where(g => !string.IsNullOrEmpty(g.Key));
            
            foreach (var group in indexGroups)
            {
                var firstRow = group.First();
                var indexName = group.Key!;
                
                var index = new IndexInfo
                {
                    Name = indexName,
                    TableName = tableName,
                    IsUnique = firstRow["UNIQUE"] != DBNull.Value && Convert.ToBoolean(firstRow["UNIQUE"]),
                    IsPrimaryKey = indexName.Equals("PrimaryKey", StringComparison.OrdinalIgnoreCase) || 
                                   indexName.StartsWith("PK_", StringComparison.OrdinalIgnoreCase),
                    ColumnNames = group.Select(row => row["COLUMN_NAME"]?.ToString() ?? string.Empty)
                                      .Where(name => !string.IsNullOrEmpty(name))
                                      .ToList()
                };
                
                indexes.Add(index);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not read indexes for table {tableName}: {ex.Message}");
        }
        
        return Task.FromResult(indexes);
    }

    private Task SetPrimaryKeyInfoAsync(OleDbConnection connection, string tableName, List<ColumnInfo> columns)
    {
        try
        {
            var schema = connection.GetSchema("Indexes", new[] { null, null, null, null, tableName });
            
            var primaryKeyColumns = schema.AsEnumerable()
                .Where(row => 
                {
                    var indexName = row["INDEX_NAME"]?.ToString() ?? string.Empty;
                    return indexName.Equals("PrimaryKey", StringComparison.OrdinalIgnoreCase) || 
                           indexName.StartsWith("PK_", StringComparison.OrdinalIgnoreCase);
                })
                .Select(row => row["COLUMN_NAME"]?.ToString())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            foreach (var column in columns)
            {
                column.IsPrimaryKey = primaryKeyColumns.Contains(column.Name);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not determine primary key for table {tableName}: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    private async Task<long> GetRecordCountAsync(OleDbConnection connection, string tableName)
    {
        try
        {
            var query = $"SELECT COUNT(*) FROM [{tableName}]";
            using var command = new OleDbCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not get record count for table {tableName}: {ex.Message}");
            return 0;
        }
    }

    private static string GetAccessDataType(DataRow columnRow)
    {
        // Try to determine Access data type from schema information
        var dataType = columnRow["DATA_TYPE"]?.ToString();
        var typeName = columnRow["TYPE_NAME"]?.ToString();
        
        if (!string.IsNullOrEmpty(typeName))
        {
            return typeName;
        }
        
        // Map OLE DB data types to Access types
        return dataType switch
        {
            "3" => "Integer",      // adInteger
            "4" => "Single",       // adSingle
            "5" => "Double",       // adDouble
            "6" => "Currency",     // adCurrency
            "7" => "Date/Time",    // adDate
            "11" => "Yes/No",      // adBoolean
            "129" => "Text",       // adChar
            "130" => "Text",       // adWChar
            "131" => "Decimal",    // adNumeric
            "202" => "Text",       // adVarWChar
            "203" => "Memo",       // adLongVarWChar
            _ => "Text"            // Default fallback
        };
    }
}