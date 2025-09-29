# Access to SQL Server Migrator

Un proyecto .NET en C# para migrar tablas y datos desde una base de datos Microsoft Access (.mdb) a SQL Server, incluyendo la creación de tablas, relaciones, índices y claves foráneas.

## Características

- **Migración completa de esquemas**: Crea tablas con tipos de datos equivalentes en SQL Server
- **Preservación de relaciones**: Transfiere relaciones entre tablas y crea claves foráneas
- **Creación de índices**: Recrea índices de la base de datos de origen
- **Migración de datos**: Transfiere todos los datos manteniendo la integridad referencial
- **Configuración flexible**: Lista configurable de tablas a migrar
- **Procesamiento por lotes**: Migración optimizada con configuración de tamaño de lote

## Requisitos

- .NET 8.0 o superior
- Microsoft Access Database Engine (para conectividad con archivos .mdb)
- SQL Server (local o remoto)
- Visual Studio 2022 o VS Code (recomendado)

## Instalación

1. **Clonar el repositorio**:
   ```bash
   git clone <repository-url>
   cd AccessToSqlServerMigrator
   ```

2. **Instalar Microsoft Access Database Engine**:
   - Descargar e instalar [Microsoft Access Database Engine 2016 Redistributable](https://www.microsoft.com/en-us/download/details.aspx?id=54920)

3. **Restaurar paquetes NuGet**:
   ```bash
   dotnet restore
   ```

## Configuración

Edita el archivo `src/AccessToSqlServerMigrator.App/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AccessDatabase": "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\\ruta\\a\\tu\\database.mdb;",
    "SqlServerDatabase": "Server=localhost;Database=TuBaseDeDatos;Trusted_Connection=true;"
  },
  "MigrationSettings": {
    "TablesToMigrate": [
      "Customers",
      "Orders",
      "OrderDetails",
      "Products"
    ],
    "CreateIndexes": true,
    "CreateForeignKeys": true,
    "BatchSize": 1000,
    "LogLevel": "Information"
  }
}
```

### Parámetros de configuración

- **AccessDatabase**: Cadena de conexión para la base de datos Access de origen
- **SqlServerDatabase**: Cadena de conexión para la base de datos SQL Server de destino
- **TablesToMigrate**: Lista de nombres de tablas a migrar (vacío = todas las tablas)
- **CreateIndexes**: Si se deben crear índices en la base de datos de destino
- **CreateForeignKeys**: Si se deben crear claves foráneas
- **BatchSize**: Número de registros a procesar por lote
- **LogLevel**: Nivel de logging (Trace, Debug, Information, Warning, Error, Critical)

## Uso

1. **Compilar el proyecto**:
   ```bash
   dotnet build
   ```

2. **Ejecutar la migración**:
   ```bash
   dotnet run --project src/AccessToSqlServerMigrator.App
   ```

## Estructura del proyecto

```
AccessToSqlServerMigrator/
├── src/
│   └── AccessToSqlServerMigrator.App/
│       ├── Configuration/          # Clases de configuración
│       ├── Extensions/             # Métodos de extensión
│       ├── Models/                 # Modelos de datos
│       ├── Services/               # Servicios de migración
│       ├── appsettings.json       # Configuración
│       └── Program.cs             # Punto de entrada
├── README.md
└── AccessToSqlServerMigrator.sln
```

## Flujo de migración

1. **Análisis del esquema**: Lee la estructura de las tablas de Access
2. **Creación de tablas**: Crea las tablas equivalentes en SQL Server
3. **Migración de datos**: Transfiere los datos en lotes
4. **Creación de índices**: Recrea los índices de las tablas originales
5. **Establecimiento de relaciones**: Crea las claves foráneas y relaciones

## Tipos de datos compatibles

| Access | SQL Server |
|--------|------------|
| Text | NVARCHAR |
| Memo | NTEXT |
| Number (Integer) | INT |
| Number (Long Integer) | BIGINT |
| Number (Single) | REAL |
| Number (Double) | FLOAT |
| Currency | MONEY |
| Date/Time | DATETIME2 |
| Yes/No | BIT |
| OLE Object | VARBINARY(MAX) |
| AutoNumber | IDENTITY |

## Solución de problemas

### Error de conectividad con Access
- Asegúrate de tener instalado Microsoft Access Database Engine
- Verifica que la ruta del archivo .mdb sea correcta
- Comprueba los permisos de lectura del archivo

### Error de conexión con SQL Server
- Verifica que SQL Server esté ejecutándose
- Comprueba la cadena de conexión
- Asegúrate de tener permisos para crear bases de datos y tablas

### Conflictos de nombres
- Las palabras reservadas de SQL Server se manejan automáticamente
- Los nombres de tablas y campos se adaptan a las convenciones de SQL Server

## Contribuir

1. Fork el proyecto
2. Crea una rama para tu feature (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request

## Licencia

Este proyecto está bajo la Licencia MIT. Ver el archivo `LICENSE` para más detalles.

## Autor

Proyecto creado para facilitar la migración de bases de datos Access a SQL Server.