namespace AccessToSqlServerMigrator.App.Extensions;

public static class DataTypeExtensions
{
    private static readonly Dictionary<string, string> AccessToSqlServerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Text types
        ["Text"] = "NVARCHAR",
        ["Memo"] = "NTEXT",
        
        // Number types
        ["Number"] = "INT",
        ["Integer"] = "INT",
        ["Long"] = "BIGINT",
        ["Single"] = "REAL",
        ["Double"] = "FLOAT",
        ["Decimal"] = "DECIMAL",
        ["Currency"] = "MONEY",
        
        // Date/Time
        ["Date/Time"] = "DATETIME2",
        ["DateTime"] = "DATETIME2",
        
        // Boolean
        ["Yes/No"] = "BIT",
        ["Boolean"] = "BIT",
        
        // Binary
        ["OLE Object"] = "VARBINARY(MAX)",
        ["Binary"] = "VARBINARY(MAX)",
        
        // Auto Number
        ["AutoNumber"] = "INT IDENTITY(1,1)",
        ["Counter"] = "INT IDENTITY(1,1)"
    };

    public static string ToSqlServerDataType(this string accessDataType)
    {
        if (string.IsNullOrWhiteSpace(accessDataType))
            return "NVARCHAR(255)";

        // Handle special cases with sizes
        var cleanType = accessDataType.Trim();
        
        // Extract size information if present
        if (cleanType.Contains('('))
        {
            var baseType = cleanType.Substring(0, cleanType.IndexOf('(')).Trim();
            var sizeInfo = cleanType.Substring(cleanType.IndexOf('('));
            
            if (AccessToSqlServerTypeMap.TryGetValue(baseType, out var sqlType))
            {
                // For text types, preserve size information
                if (sqlType == "NVARCHAR")
                    return $"NVARCHAR{sizeInfo}";
                
                return sqlType;
            }
        }

        // Direct mapping
        if (AccessToSqlServerTypeMap.TryGetValue(cleanType, out var mappedType))
            return mappedType;

        // Default fallback
        return "NVARCHAR(255)";
    }

    public static string GetSqlServerDataTypeWithLength(this string accessDataType, int? maxLength = null)
    {
        var sqlType = accessDataType.ToSqlServerDataType();
        
        // Apply default lengths for variable-length types
        if (sqlType == "NVARCHAR" && maxLength.HasValue)
        {
            var length = maxLength.Value;
            // Use MAX for large text fields
            return length > 4000 ? "NVARCHAR(MAX)" : $"NVARCHAR({length})";
        }
        
        if (sqlType == "NVARCHAR")
            return "NVARCHAR(255)";
            
        return sqlType;
    }

    public static bool IsAutoNumber(this string accessDataType)
    {
        return accessDataType.Equals("AutoNumber", StringComparison.OrdinalIgnoreCase) ||
               accessDataType.Equals("Counter", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNumericType(this string sqlServerDataType)
    {
        var numericTypes = new[] { "INT", "BIGINT", "SMALLINT", "TINYINT", "DECIMAL", "NUMERIC", "FLOAT", "REAL", "MONEY", "SMALLMONEY" };
        return numericTypes.Any(t => sqlServerDataType.StartsWith(t, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTextType(this string sqlServerDataType)
    {
        return sqlServerDataType.StartsWith("NVARCHAR", StringComparison.OrdinalIgnoreCase) ||
               sqlServerDataType.StartsWith("VARCHAR", StringComparison.OrdinalIgnoreCase) ||
               sqlServerDataType.Equals("NTEXT", StringComparison.OrdinalIgnoreCase) ||
               sqlServerDataType.Equals("TEXT", StringComparison.OrdinalIgnoreCase);
    }
}