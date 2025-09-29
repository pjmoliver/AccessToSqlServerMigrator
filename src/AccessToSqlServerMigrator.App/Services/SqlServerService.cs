using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using AccessToSqlServerMigrator.App.Models;
using AccessToSqlServerMigrator.App.Extensions;

namespace AccessToSqlServerMigrator.App.Services;

public class SqlServerService
{
    private readonly string _connectionString;

    public SqlServerService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateTableAsync(TableSchema tableSchema)
    {
        var sql = GenerateCreateTableSql(tableSchema);
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine($"Created table: {tableSchema.Name}");
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = @TableName AND TABLE_TYPE = 'BASE TABLE'";
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);
        
        var result = await command.ExecuteScalarAsync();
        var count = result != null ? Convert.ToInt32(result) : 0;
        return count > 0;
    }

    public async Task DropTableAsync(string tableName)
    {
        var sql = $"DROP TABLE IF EXISTS [{tableName}]";
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine($"Dropped table: {tableName}");
    }

    public async Task InsertDataAsync(string tableName, DataTable data, List<ColumnInfo> columns, int batchSize = 1000)
    {
        if (data.Rows.Count == 0) return;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Process data in batches
        var totalRows = data.Rows.Count;
        var processedRows = 0;

        for (int i = 0; i < totalRows; i += batchSize)
        {
            var batchEnd = Math.Min(i + batchSize, totalRows);
            var currentBatch = batchEnd - i;

            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = GenerateInsertSql(tableName, columns);
                using var command = new SqlCommand(sql, connection, transaction);

                // Add parameters for the batch
                for (int rowIndex = i; rowIndex < batchEnd; rowIndex++)
                {
                    var row = data.Rows[rowIndex];
                    command.Parameters.Clear();

                    foreach (var column in columns.Where(c => !c.IsAutoNumber))
                    {
                        var value = row[column.Name];
                        var parameter = command.Parameters.AddWithValue($"@{column.Name}", 
                            value == DBNull.Value ? DBNull.Value : ConvertValueForSqlServer(value, column));
                    }

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                processedRows += currentBatch;
                
                Console.WriteLine($"Inserted {processedRows}/{totalRows} rows into {tableName}");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task CreateIndexesAsync(string tableName, List<IndexInfo> indexes)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var index in indexes)
        {
            if (index.IsPrimaryKey) continue; // Primary keys are created with table

            var sql = GenerateCreateIndexSql(index);
            
            try
            {
                using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Created index: {index.Name} on table {tableName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create index {index.Name} on table {tableName}: {ex.Message}");
            }
        }
    }

    public async Task CreateForeignKeysAsync(List<RelationshipInfo> relationships)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var relationship in relationships)
        {
            var sql = GenerateCreateForeignKeySql(relationship);
            
            try
            {
                using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Created foreign key: {relationship.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create foreign key {relationship.Name}: {ex.Message}");
            }
        }
    }

    private string GenerateCreateTableSql(TableSchema tableSchema)
    {
        var sql = new StringBuilder();
        sql.AppendLine($"CREATE TABLE [{tableSchema.Name}] (");

        var columnDefinitions = new List<string>();
        var primaryKeyColumns = new List<string>();

        foreach (var column in tableSchema.Columns)
        {
            var definition = new StringBuilder();
            definition.Append($"    [{column.Name}] {column.SqlServerDataType}");

            if (!column.IsNullable && !column.IsAutoNumber)
            {
                definition.Append(" NOT NULL");
            }

            if (column.DefaultValue != null && !column.IsAutoNumber)
            {
                definition.Append($" DEFAULT {FormatDefaultValue(column.DefaultValue, column.SqlServerDataType)}");
            }

            columnDefinitions.Add(definition.ToString());

            if (column.IsPrimaryKey)
            {
                primaryKeyColumns.Add($"[{column.Name}]");
            }
        }

        sql.AppendLine(string.Join(",\n", columnDefinitions));

        // Add primary key constraint if exists
        if (primaryKeyColumns.Any())
        {
            sql.AppendLine($",    CONSTRAINT [PK_{tableSchema.Name}] PRIMARY KEY CLUSTERED ({string.Join(", ", primaryKeyColumns)})");
        }

        sql.AppendLine(");");

        return sql.ToString();
    }

    private string GenerateInsertSql(string tableName, List<ColumnInfo> columns)
    {
        var insertColumns = columns.Where(c => !c.IsAutoNumber).Select(c => $"[{c.Name}]");
        var parameters = columns.Where(c => !c.IsAutoNumber).Select(c => $"@{c.Name}");

        return $"INSERT INTO [{tableName}] ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", parameters)})";
    }

    private string GenerateCreateIndexSql(IndexInfo index)
    {
        var uniqueKeyword = index.IsUnique ? "UNIQUE " : "";
        var clusteredKeyword = index.IsClustered ? "CLUSTERED" : "NONCLUSTERED";
        var columns = string.Join(", ", index.ColumnNames.Select(c => $"[{c}]"));

        return $"CREATE {uniqueKeyword}{clusteredKeyword} INDEX [{index.Name}] ON [{index.TableName}] ({columns})";
    }

    private string GenerateCreateForeignKeySql(RelationshipInfo relationship)
    {
        var constraintName = $"FK_{relationship.ChildTable}_{relationship.ParentTable}";
        if (!string.IsNullOrEmpty(relationship.Name))
        {
            constraintName = relationship.Name;
        }

        return $@"ALTER TABLE [{relationship.ChildTable}] 
                 ADD CONSTRAINT [{constraintName}] 
                 FOREIGN KEY ([{relationship.ChildColumn}]) 
                 REFERENCES [{relationship.ParentTable}] ([{relationship.ParentColumn}])
                 ON DELETE {relationship.DeleteRule} 
                 ON UPDATE {relationship.UpdateRule}";
    }

    private object ConvertValueForSqlServer(object value, ColumnInfo column)
    {
        if (value == null || value == DBNull.Value)
            return DBNull.Value;

        // Handle specific data type conversions
        if (column.SqlServerDataType.StartsWith("BIT", StringComparison.OrdinalIgnoreCase))
        {
            // Convert various boolean representations
            if (value is bool boolValue)
                return boolValue;
            
            var stringValue = value.ToString()?.ToLower();
            return stringValue is "true" or "yes" or "1" or "-1";
        }

        if (column.SqlServerDataType.StartsWith("DATETIME", StringComparison.OrdinalIgnoreCase))
        {
            if (value is DateTime dateValue)
                return dateValue;
            
            if (DateTime.TryParse(value.ToString(), out var parsedDate))
                return parsedDate;
        }

        if (column.SqlServerDataType.IsNumericType())
        {
            // Ensure numeric values are properly formatted
            if (value is string strValue && string.IsNullOrWhiteSpace(strValue))
                return DBNull.Value;
        }

        return value;
    }

    private string FormatDefaultValue(object defaultValue, string sqlServerDataType)
    {
        if (defaultValue == null)
            return "NULL";

        if (sqlServerDataType.IsTextType())
        {
            return $"'{defaultValue.ToString()?.Replace("'", "''")}'";
        }

        if (sqlServerDataType.StartsWith("BIT", StringComparison.OrdinalIgnoreCase))
        {
            var boolValue = Convert.ToBoolean(defaultValue);
            return boolValue ? "1" : "0";
        }

        if (sqlServerDataType.StartsWith("DATETIME", StringComparison.OrdinalIgnoreCase))
        {
            if (defaultValue.ToString()?.ToUpper() == "NOW()" || defaultValue.ToString()?.ToUpper() == "DATE()")
            {
                return "GETDATE()";
            }
            
            if (DateTime.TryParse(defaultValue.ToString(), out var dateValue))
            {
                return $"'{dateValue:yyyy-MM-dd HH:mm:ss}'";
            }
        }

        return defaultValue.ToString() ?? "NULL";
    }
}