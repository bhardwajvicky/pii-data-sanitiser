# PII Obfuscation Portal

A web-based portal for managing PII (Personally Identifiable Information) obfuscation configurations across multiple database products.

## Features

- **Product Management**: View and manage database products
- **Mapping Visualization**: View detailed column mappings and obfuscation configurations
- **Vertical Slice Architecture**: Clean separation of concerns with feature-based organization
- **RESTful APIs**: Well-structured API endpoints for data access
- **Modern UI**: Bootstrap-based responsive interface

## Architecture

The application follows the **Vertical Slice Pattern** with the following structure:

```
portal/
├── api/                    # REST API (ASP.NET Core)
│   ├── Controllers/       # API Controllers
│   ├── Features/          # Vertical slices (CQRS with MediatR)
│   └── Mapping/          # AutoMapper profiles
├── contracts/             # Shared DTOs and models
├── dal/                  # Data Access Layer
│   └── Repositories/     # Repository pattern implementation
└── web/                  # Web UI (ASP.NET Core MVC)
    ├── Controllers/      # MVC Controllers
    └── Views/           # Razor Views
```

## Technology Stack

- **Backend**: ASP.NET Core 8.0
- **Database**: SQL Server 2022
- **ORM**: Entity Framework Core
- **Architecture**: Vertical Slice Pattern with CQRS
- **UI**: Bootstrap 5.3 with Bootstrap Icons
- **Containerization**: Docker & Docker Compose

## Quick Start

### Option 1: Using Docker Compose (Recommended)

1. **Prerequisites**:
   - Docker Desktop installed
   - Git

2. **Clone and Run**:
   ```bash
   cd portal
   docker-compose up -d
   ```

3. **Access the Application**:
   - Web Portal: http://localhost:7002
   - API Documentation: http://localhost:7001/swagger
   - Database: localhost:1433 (sa/YourStrong@Passw0rd)

### Option 2: Local Development

1. **Prerequisites**:
   - .NET 8.0 SDK
   - SQL Server 2022 (or SQL Server Express)
   - Visual Studio 2022 or VS Code

2. **Database Setup**:
   ```sql
   -- Run the database schema and seed scripts
   -- database-schema.sql
   -- seed.sql
   ```

3. **API Configuration**:
   ```bash
   cd portal/api
   dotnet restore
   dotnet run
   ```

4. **Web Configuration**:
   ```bash
   cd portal/web
   dotnet restore
   dotnet run
   ```

## API Endpoints

### Products
- `GET /api/products` - Get all active products
- `GET /api/products/{id}/mappings` - Get product mappings

### Response Examples

**Get Products**:
```json
[
  {
    "id": "guid",
    "name": "adv",
    "description": "AdventureWorks Database",
    "databaseTechnology": "SqlServer",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z"
  }
]
```

**Get Product Mappings**:
```json
{
  "id": "guid",
  "name": "adv",
  "description": "AdventureWorks Database",
  "databaseTechnology": "SqlServer",
  "isActive": true,
  "tables": [
    {
      "id": "guid",
      "schemaName": "HumanResources",
      "tableName": "Employee",
      "fullTableName": "HumanResources.Employee",
      "isAnalyzed": true,
      "columns": [
        {
          "id": "guid",
          "columnName": "BusinessEntityID",
          "sqlDataType": "int",
          "isPrimaryKey": true,
          "obfuscationMapping": {
            "obfuscationDataType": "None",
            "isEnabled": false
          }
        }
      ]
    }
  ]
}
```

## Web Pages

### 1. Products List Page (`/`)
- Displays all active products in a table format
- Shows product name, description, database technology, and status
- Provides navigation to view detailed mappings for each product

### 2. Product Mappings Page (`/Home/ProductMappings/{id}`)
- Shows detailed information about a specific product
- Displays database tables in an accordion layout
- Shows column-level obfuscation mappings
- Provides visual indicators for mapping status and configuration

## Database Schema

The application uses the following key tables:

- **Products**: Main product configurations
- **DatabaseSchemas**: Table-level information
- **TableColumns**: Column-level metadata
- **ColumnObfuscationMappings**: Obfuscation configuration per column
- **ObfuscationConfigurations**: Global obfuscation settings

## Development

### Adding New Features

1. **API Features**: Add new vertical slices in `api/Features/`
2. **DTOs**: Add new DTOs in `contracts/DTOs/`
3. **Views**: Add new views in `web/Views/`
4. **Controllers**: Add new controllers as needed

### Code Organization

- **Vertical Slice Pattern**: Each feature is self-contained with its own query/command handlers
- **CQRS**: Separate read and write operations
- **Repository Pattern**: Data access abstraction
- **AutoMapper**: Object-to-object mapping

## Configuration

### API Configuration (`api/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "PortalDb": "Server=localhost;Database=obfuscate;..."
  }
}
```

### Web Configuration (`web/appsettings.json`)
```json
{
  "ApiBaseUrl": "https://localhost:7001/"
}
```

## Troubleshooting

### Common Issues

1. **Database Connection**: Ensure SQL Server is running and accessible
2. **API Communication**: Check that the API is running on the correct port
3. **Docker Issues**: Ensure Docker Desktop is running and has sufficient resources

### Logs

- API logs: Check the console output or Docker logs
- Web logs: Check the console output or Docker logs
- Database logs: Check SQL Server logs

## Contributing

1. Follow the vertical slice pattern for new features
2. Add appropriate error handling and logging
3. Include unit tests for new functionality
4. Update documentation as needed

## License

This project is licensed under the MIT License.
