# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

This is a .NET 10 C# console application that migrates Microsoft Access databases (.mdb) to SQL Server, including schema, data, relationships, indexes, and foreign keys. The project uses a layered architecture with configuration-driven migration settings.

## Essential Commands

### Build and Run
```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Build in release mode
dotnet build --configuration Release

# Run the application
dotnet run --project src/AccessToSqlServerMigrator.App

# Run with specific configuration
dotnet run --project src/AccessToSqlServerMigrator.App --configuration Release
```

### Development
```bash
# Clean build artifacts
dotnet clean

# Format code (if using dotnet format)
dotnet format

# Check for security vulnerabilities
dotnet list package --vulnerable
```

### Platform Support
The project is configured to support multiple platforms (Any CPU, x64, x86) as defined in the solution file. Use platform-specific builds when needed:
```bash
# Build for specific platform
dotnet build --configuration Release --runtime win-x64
```

## Architecture Overview

### Project Structure
```
AccessToSqlServerMigrator/
├── src/AccessToSqlServerMigrator.App/     # Main console application
│   ├── Configuration/                     # Configuration models and setup
│   ├── Extensions/                        # Extension methods for data type mapping
│   ├── Models/                           # Data models for migration metadata
│   ├── Services/                         # Core migration services
│   ├── appsettings.json                  # Application configuration
│   └── Program.cs                        # Application entry point
└── AccessToSqlServerMigrator.sln         # Solution file
```

### Key Dependencies
- **Microsoft.Data.SqlClient**: SQL Server connectivity and data operations
- **System.Data.OleDb**: Access database connectivity via OLE DB provider
- **Microsoft.Extensions.Configuration**: Configuration management from appsettings.json

### Architecture Pattern
The application follows a service-oriented architecture:

1. **Configuration Layer**: Handles appsettings.json configuration binding
2. **Models Layer**: Defines data structures for migration metadata (table schemas, relationships, indexes)
3. **Services Layer**: Contains core migration logic, data type mapping, and database operations
4. **Extensions Layer**: Provides extension methods for data type conversion between Access and SQL Server

### Migration Flow
The application is designed to perform migration in these stages:
1. **Schema Analysis**: Read Access database structure and metadata
2. **Table Creation**: Generate equivalent SQL Server tables with proper data types
3. **Data Migration**: Transfer data in configurable batches
4. **Index Creation**: Recreate indexes from the source database
5. **Relationship Establishment**: Create foreign keys and constraints

## Configuration

### Connection Strings
Configure database connections in `src/AccessToSqlServerMigrator.App/appsettings.json`:

- **AccessDatabase**: OLE DB connection string for source Access database
- **SqlServerDatabase**: SQL Server connection string for target database

### Migration Settings
- **TablesToMigrate**: Specify which tables to migrate (empty array = all tables)
- **CreateIndexes**: Enable/disable index creation on target database
- **CreateForeignKeys**: Enable/disable foreign key creation
- **BatchSize**: Number of records to process per batch for performance optimization
- **LogLevel**: Logging verbosity (Trace, Debug, Information, Warning, Error, Critical)

## Data Type Mapping

The application maps Access data types to SQL Server equivalents:
- Access Text → SQL Server NVARCHAR
- Access Number (Integer) → SQL Server INT
- Access Date/Time → SQL Server DATETIME2
- Access AutoNumber → SQL Server IDENTITY
- (See README.md for complete mapping table)

## Prerequisites

### System Requirements
- .NET 10 SDK or runtime
- Microsoft Access Database Engine (ACE.OLEDB.12.0 provider)
- SQL Server instance (local or remote)

### Development Environment
- Visual Studio 2022 or VS Code recommended
- Access to both source (.mdb) and target SQL Server databases
- Appropriate permissions for database schema modification

## Common Development Patterns

### Error Handling
The application should implement comprehensive error handling for:
- Database connectivity issues
- Data type conversion failures
- Constraint violation during migration
- Memory management for large datasets

### Performance Considerations
- Batch processing for large datasets (configurable via BatchSize)
- Transaction management for data consistency
- Connection pooling optimization
- Memory-efficient data streaming for large tables

### Security Considerations
- Connection string encryption in production
- SQL injection prevention through parameterized queries
- Proper authentication and authorization validation
- Secure handling of database credentials