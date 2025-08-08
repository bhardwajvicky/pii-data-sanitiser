-- =============================================
-- Seed Data for PII Obfuscation Portal
-- =============================================

USE obfuscate;
GO

-- =============================================
-- Clear existing data first (optional)
-- =============================================

-- Delete existing data in reverse dependency order
DELETE FROM ColumnObfuscationMappings WHERE ProductId IN (SELECT Id FROM Products WHERE Name = 'adv');
DELETE FROM ObfuscationConfigurations WHERE ProductId IN (SELECT Id FROM Products WHERE Name = 'adv');
DELETE FROM TableColumns WHERE DatabaseSchemaId IN (SELECT Id FROM DatabaseSchemas WHERE ProductId IN (SELECT Id FROM Products WHERE Name = 'adv'));
DELETE FROM DatabaseSchemas WHERE ProductId IN (SELECT Id FROM Products WHERE Name = 'adv');
DELETE FROM Products WHERE Name = 'adv';
GO

-- =============================================
-- Insert Product
-- =============================================

INSERT INTO Products (
    Id,
    Name,
    Description,
    ConnectionString,
    DatabaseTechnology,
    GlobalSeed,
    BatchSize,
    SqlBatchSize,
    ParallelThreads,
    MaxCacheSize,
    CommandTimeoutSeconds,
    MappingCacheDirectory,
    IsActive,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) VALUES (
    NEWID(),
    'adv',
    'AdventureWorks Database - PII Obfuscation Configuration',
    'Server=test-db-server-try.database.windows.net;Database=adv2;User Id=vikas;Password=Count123#;Encrypt=true;',
    'SqlServer',
    'PII-Sanitizer-2024-CrossDB-Deterministic-AU-v3.7.2',
    2000,
    500,
    8,
    500000,
    600,
    'mappings/adv2',
    1,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
);
GO

-- =============================================
-- Insert Database Schemas (Tables)
-- =============================================

-- Employee table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'HumanResources',
    'Employee',
    'HumanResources.Employee',
    '["BusinessEntityID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';

-- PersonPhone table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'Person',
    'PersonPhone',
    'Person.PersonPhone',
    '["BusinessEntityID", "PhoneNumber", "PhoneNumberTypeID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';

-- Address table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'Person',
    'Address',
    'Person.Address',
    '["AddressID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';

-- EmailAddress table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'Person',
    'EmailAddress',
    'Person.EmailAddress',
    '["BusinessEntityID", "EmailAddressID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';

-- CreditCard table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'Sales',
    'CreditCard',
    'Sales.CreditCard',
    '["CreditCardID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';

-- ProductReview table
INSERT INTO DatabaseSchemas (
    Id,
    ProductId,
    SchemaName,
    TableName,
    FullTableName,
    PrimaryKeyColumns,
    [RowCount],
    IsAnalyzed,
    AnalyzedAt,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    Id,
    'Production',
    'ProductReview',
    'Production.ProductReview',
    '["ProductReviewID"]',
    0,
    1,
    GETDATE(),
    GETDATE(),
    GETDATE()
FROM Products WHERE Name = 'adv';
GO

-- =============================================
-- Insert Table Columns
-- =============================================

-- Employee table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'NationalIDNumber',
    'nvarchar',
    15,
    0,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Employee' AND p.Name = 'adv';

INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'BirthDate',
    'date',
    NULL,
    0,
    0,
    0,
    0,
    0,
    NULL,
    2,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Employee' AND p.Name = 'adv';

-- PersonPhone table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'PhoneNumber',
    'nvarchar',
    25,
    0,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'PersonPhone' AND p.Name = 'adv';

-- Address table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'AddressLine1',
    'nvarchar',
    60,
    0,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'AddressLine2',
    'nvarchar',
    60,
    1,
    0,
    0,
    0,
    0,
    NULL,
    2,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'City',
    'nvarchar',
    30,
    0,
    0,
    0,
    0,
    0,
    NULL,
    3,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'PostalCode',
    'nvarchar',
    15,
    0,
    0,
    0,
    0,
    0,
    NULL,
    4,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'Address' AND p.Name = 'adv';

-- EmailAddress table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'EmailAddress',
    'nvarchar',
    50,
    1,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'EmailAddress' AND p.Name = 'adv';

-- CreditCard table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'CardNumber',
    'nvarchar',
    25,
    0,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'CreditCard' AND p.Name = 'adv';

-- ProductReview table columns
INSERT INTO TableColumns (
    Id,
    DatabaseSchemaId,
    ColumnName,
    SqlDataType,
    MaxLength,
    IsNullable,
    IsPrimaryKey,
    IsForeignKey,
    IsIdentity,
    IsComputed,
    DefaultValue,
    OrdinalPosition,
    CreatedAt,
    UpdatedAt
) 
SELECT 
    NEWID(),
    ds.Id,
    'EmailAddress',
    'nvarchar',
    50,
    0,
    0,
    0,
    0,
    0,
    NULL,
    1,
    GETDATE(),
    GETDATE()
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
WHERE ds.TableName = 'ProductReview' AND p.Name = 'adv';
GO

-- =============================================
-- Insert Column Obfuscation Mappings
-- =============================================

-- Employee table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'LicenseNumber',
    1,
    1,
    0.8,
    '["Column name contains ID pattern", "License number format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'NationalIDNumber' AND ds.TableName = 'Employee' AND p.Name = 'adv';

INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'DateOfBirth',
    1,
    0,
    0.9,
    '["Column name contains birth pattern", "Date data type"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'BirthDate' AND ds.TableName = 'Employee' AND p.Name = 'adv';

-- PersonPhone table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'DriverPhone',
    1,
    1,
    0.9,
    '["Column name contains phone pattern", "Phone number format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'PhoneNumber' AND ds.TableName = 'PersonPhone' AND p.Name = 'adv';

-- Address table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'AddressLine1',
    1,
    0,
    0.8,
    '["Column name contains address pattern", "Address format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'AddressLine1' AND ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'AddressLine1',
    1,
    0,
    0.7,
    '["Column name contains address pattern", "Secondary address"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'AddressLine2' AND ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'City',
    1,
    0,
    0.8,
    '["Column name contains city pattern", "City name format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'City' AND ds.TableName = 'Address' AND p.Name = 'adv';

INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'PostCode',
    1,
    0,
    0.8,
    '["Column name contains postal pattern", "Postal code format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'PostalCode' AND ds.TableName = 'Address' AND p.Name = 'adv';

-- EmailAddress table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'ContactEmail',
    1,
    0,
    0.9,
    '["Column name contains email pattern", "Email format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'EmailAddress' AND ds.TableName = 'EmailAddress' AND p.Name = 'adv';

-- CreditCard table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'CreditCard',
    1,
    1,
    0.9,
    '["Column name contains card pattern", "Credit card format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'CardNumber' AND ds.TableName = 'CreditCard' AND p.Name = 'adv';

-- ProductReview table mappings
INSERT INTO ColumnObfuscationMappings (
    Id,
    ProductId,
    TableColumnId,
    ObfuscationDataType,
    IsEnabled,
    PreserveLength,
    ConfidenceScore,
    DetectionReasons,
    IsManuallyConfigured,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    p.Id,
    tc.Id,
    'ContactEmail',
    1,
    0,
    0.8,
    '["Column name contains email pattern", "Email format"]',
    0,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE tc.ColumnName = 'EmailAddress' AND ds.TableName = 'ProductReview' AND p.Name = 'adv';
GO

-- =============================================
-- Insert Obfuscation Configuration
-- =============================================

INSERT INTO ObfuscationConfigurations (
    Id,
    ProductId,
    Name,
    Description,
    ConfigurationJson,
    IsActive,
    IsDefault,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy
) 
SELECT 
    NEWID(),
    Id,
    'adv2-mapping.json',
    'AdventureWorks Database Obfuscation Configuration',
    '{
  "Global": {
    "ConnectionString": "Server=test-db-server-try.database.windows.net;Database=adv2;User Id=vikas;Password=Count123#;Encrypt=true;",
    "DatabaseTechnology": "SqlServer",
    "GlobalSeed": "PII-Sanitizer-2024-CrossDB-Deterministic-AU-v3.7.2",
    "BatchSize": 2000,
    "SqlBatchSize": 500,
    "ParallelThreads": 8,
    "MaxCacheSize": 500000,
    "DryRun": false,
    "PersistMappings": true,
    "EnableValueCaching": true,
    "CommandTimeoutSeconds": 600,
    "MappingCacheDirectory": "mappings/adv2"
  },
  "DataTypes": {},
  "ReferentialIntegrity": {
    "Enabled": false,
    "Relationships": [],
    "StrictMode": false,
    "OnViolation": "warn"
  },
  "PostProcessing": {
    "GenerateReport": true,
    "ReportPath": "reports/adv2-obfuscation-{timestamp}.json",
    "ValidateResults": true,
    "BackupMappings": true
  },
  "Tables": [
    {
      "TableName": "Employee",
      "Schema": "HumanResources",
      "FullTableName": "HumanResources.Employee",
      "PrimaryKey": ["BusinessEntityID"],
      "Columns": [
        {
          "ColumnName": "NationalIDNumber",
          "DataType": "LicenseNumber",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": true
        },
        {
          "ColumnName": "BirthDate",
          "DataType": "DateOfBirth",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        }
      ]
    },
    {
      "TableName": "PersonPhone",
      "Schema": "Person",
      "FullTableName": "Person.PersonPhone",
      "PrimaryKey": ["BusinessEntityID", "PhoneNumber", "PhoneNumberTypeID"],
      "Columns": [
        {
          "ColumnName": "PhoneNumber",
          "DataType": "Phone",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": true
        }
      ]
    },
    {
      "TableName": "Address",
      "Schema": "Person",
      "FullTableName": "Person.Address",
      "PrimaryKey": ["AddressID"],
      "Columns": [
        {
          "ColumnName": "AddressLine1",
          "DataType": "AddressLine1",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        },
        {
          "ColumnName": "AddressLine2",
          "DataType": "AddressLine1",
          "Enabled": true,
          "IsNullable": true,
          "PreserveLength": false
        },
        {
          "ColumnName": "City",
          "DataType": "City",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        },
        {
          "ColumnName": "PostalCode",
          "DataType": "PostCode",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        }
      ]
    },
    {
      "TableName": "EmailAddress",
      "Schema": "Person",
      "FullTableName": "Person.EmailAddress",
      "PrimaryKey": ["BusinessEntityID", "EmailAddressID"],
      "Columns": [
        {
          "ColumnName": "EmailAddress",
          "DataType": "AddressLine1",
          "Enabled": true,
          "IsNullable": true,
          "PreserveLength": false
        }
      ]
    },
    {
      "TableName": "CreditCard",
      "Schema": "Sales",
      "FullTableName": "Sales.CreditCard",
      "PrimaryKey": ["CreditCardID"],
      "Columns": [
        {
          "ColumnName": "CardNumber",
          "DataType": "CreditCard",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": true
        }
      ]
    },
    {
      "TableName": "ProductReview",
      "Schema": "Production",
      "FullTableName": "Production.ProductReview",
      "PrimaryKey": ["ProductReviewID"],
      "Columns": [
        {
          "ColumnName": "EmailAddress",
          "DataType": "AddressLine1",
          "Enabled": true,
          "IsNullable": false,
          "PreserveLength": false
        }
      ]
    }
  ]
}',
    1,
    1,
    GETDATE(),
    GETDATE(),
    'System',
    'System'
FROM Products WHERE Name = 'adv';
GO

-- =============================================
-- Verification Query
-- =============================================

PRINT 'Seed data inserted successfully!';
PRINT 'Product: adv';
PRINT 'Tables: 6';
PRINT 'Columns with obfuscation mappings: 10';

-- Display summary
SELECT 
    p.Name as ProductName,
    COUNT(DISTINCT ds.Id) as TableCount,
    COUNT(DISTINCT tc.Id) as ColumnCount,
    COUNT(DISTINCT com.Id) as ObfuscatedColumnsCount
FROM Products p
LEFT JOIN DatabaseSchemas ds ON p.Id = ds.ProductId
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
WHERE p.Name = 'adv'
GROUP BY p.Name;
GO
