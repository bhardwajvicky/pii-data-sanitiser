-- =============================================
-- Database Schema for PII Obfuscation Portal
-- =============================================

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'obfuscate')
BEGIN
    CREATE DATABASE obfuscate;
END
GO

USE obfuscate;
GO

-- =============================================
-- Drop existing objects if they exist
-- =============================================

-- Drop triggers first
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'tr_Products_Audit')
    DROP TRIGGER tr_Products_Audit;
GO

-- Drop stored procedures
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetProductConfiguration')
    DROP PROCEDURE sp_GetProductConfiguration;
GO

-- Drop views
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_SchemaAnalysisSummary')
    DROP VIEW vw_SchemaAnalysisSummary;
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ProductSummary')
    DROP VIEW vw_ProductSummary;
GO

-- Drop tables in reverse dependency order
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnObfuscationMappings')
    DROP TABLE ColumnObfuscationMappings;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ObfuscationConfigurations')
    DROP TABLE ObfuscationConfigurations;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TableColumns')
    DROP TABLE TableColumns;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DatabaseSchemas')
    DROP TABLE DatabaseSchemas;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaAnalysisSessions')
    DROP TABLE SchemaAnalysisSessions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLog')
    DROP TABLE AuditLog;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'UserProductPermissions')
    DROP TABLE UserProductPermissions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
    DROP TABLE Roles;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
    DROP TABLE Users;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PIIDetectionRules')
    DROP TABLE PIIDetectionRules;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SupportedDataTypes')
    DROP TABLE SupportedDataTypes;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
    DROP TABLE Products;
GO

-- =============================================
-- Core Tables
-- =============================================

-- Products/Organizations
CREATE TABLE Products (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    ConnectionString NVARCHAR(MAX) NOT NULL,
    DatabaseTechnology NVARCHAR(50) NOT NULL, -- SqlServer, PostgreSQL, MySQL, etc.
    GlobalSeed NVARCHAR(255),
    BatchSize INT DEFAULT 2000,
    SqlBatchSize INT DEFAULT 500,
    ParallelThreads INT DEFAULT 8,
    MaxCacheSize INT DEFAULT 500000,
    CommandTimeoutSeconds INT DEFAULT 600,
    MappingCacheDirectory NVARCHAR(500),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255)
);
GO

-- Database Schemas (for each product)
CREATE TABLE DatabaseSchemas (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    SchemaName NVARCHAR(255) NOT NULL,
    TableName NVARCHAR(255) NOT NULL,
    FullTableName NVARCHAR(500) NOT NULL,
    PrimaryKeyColumns NVARCHAR(MAX), -- JSON array of primary key column names
    [RowCount] BIGINT DEFAULT 0,
    IsAnalyzed BIT DEFAULT 0,
    AnalyzedAt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- Table Columns
CREATE TABLE TableColumns (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DatabaseSchemaId UNIQUEIDENTIFIER NOT NULL,
    ColumnName NVARCHAR(255) NOT NULL,
    SqlDataType NVARCHAR(100) NOT NULL,
    MaxLength INT,
    IsNullable BIT DEFAULT 1,
    IsPrimaryKey BIT DEFAULT 0,
    IsForeignKey BIT DEFAULT 0,
    IsIdentity BIT DEFAULT 0,
    IsComputed BIT DEFAULT 0,
    DefaultValue NVARCHAR(MAX),
    OrdinalPosition INT,
    NumericPrecision TINYINT,
    NumericScale TINYINT,
    CharacterSet NVARCHAR(100),
    Collation NVARCHAR(100),
    IsRowGuid BIT DEFAULT 0,
    IsFileStream BIT DEFAULT 0,
    IsSparse BIT DEFAULT 0,
    IsXmlDocument BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (DatabaseSchemaId) REFERENCES DatabaseSchemas(Id) ON DELETE CASCADE
);
GO

-- Obfuscation Configurations
CREATE TABLE ObfuscationConfigurations (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    ConfigurationJson NVARCHAR(MAX) NOT NULL, -- Full JSON configuration
    IsActive BIT DEFAULT 1,
    IsDefault BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- Column Obfuscation Mappings
CREATE TABLE ColumnObfuscationMappings (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    TableColumnId UNIQUEIDENTIFIER NOT NULL,
    ObfuscationDataType NVARCHAR(100) NOT NULL, -- e.g., DriverName, ContactEmail, etc.
    IsEnabled BIT DEFAULT 1,
    PreserveLength BIT DEFAULT 1,
    ConfidenceScore DECIMAL(3,2) DEFAULT 0.0,
    DetectionReasons NVARCHAR(MAX), -- JSON array of detection reasons
    IsManuallyConfigured BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    CreatedBy NVARCHAR(255),
    UpdatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    FOREIGN KEY (TableColumnId) REFERENCES TableColumns(Id) ON DELETE NO ACTION
);
GO

-- =============================================
-- User Management Tables
-- =============================================

-- Users
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(255) NOT NULL UNIQUE,
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- User Roles
CREATE TABLE Roles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- User Product Permissions
CREATE TABLE UserProductPermissions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    RoleId UNIQUEIDENTIFIER NOT NULL,
    GrantedAt DATETIME2 DEFAULT GETDATE(),
    GrantedBy NVARCHAR(255),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);
GO

-- =============================================
-- Audit and Logging Tables
-- =============================================

-- Audit Log
CREATE TABLE AuditLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER,
    ProductId UNIQUEIDENTIFIER,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(100),
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE SET NULL
);
GO

-- Schema Analysis Sessions
CREATE TABLE SchemaAnalysisSessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    StartedAt DATETIME2 DEFAULT GETDATE(),
    CompletedAt DATETIME2,
    Status NVARCHAR(50) DEFAULT 'Running', -- Running, Completed, Failed
    ErrorMessage NVARCHAR(MAX),
    TablesAnalyzed INT DEFAULT 0,
    ColumnsAnalyzed INT DEFAULT 0,
    PIIColumnsFound INT DEFAULT 0,
    CreatedBy NVARCHAR(255),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);
GO

-- =============================================
-- Configuration and Settings Tables
-- =============================================

-- Supported Data Types
CREATE TABLE SupportedDataTypes (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(MAX),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- PII Detection Rules
CREATE TABLE PIIDetectionRules (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DataType NVARCHAR(100) NOT NULL,
    ObfuscationDataType NVARCHAR(100) NOT NULL,
    ColumnNamePatterns NVARCHAR(MAX), -- JSON array of patterns
    SqlDataTypes NVARCHAR(MAX), -- JSON array of SQL data types
    TableNamePatterns NVARCHAR(MAX), -- JSON array of table name patterns
    BaseConfidence DECIMAL(3,2) DEFAULT 0.5,
    PreserveLength BIT DEFAULT 1,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);
GO

-- =============================================
-- Indexes for Performance
-- =============================================

-- Products
CREATE INDEX IX_Products_Name ON Products(Name);
CREATE INDEX IX_Products_IsActive ON Products(IsActive);
GO

-- Database Schemas
CREATE INDEX IX_DatabaseSchemas_ProductId ON DatabaseSchemas(ProductId);
CREATE INDEX IX_DatabaseSchemas_FullTableName ON DatabaseSchemas(FullTableName);
CREATE INDEX IX_DatabaseSchemas_IsAnalyzed ON DatabaseSchemas(IsAnalyzed);
GO

-- Table Columns
CREATE INDEX IX_TableColumns_DatabaseSchemaId ON TableColumns(DatabaseSchemaId);
CREATE INDEX IX_TableColumns_ColumnName ON TableColumns(ColumnName);
CREATE INDEX IX_TableColumns_IsPrimaryKey ON TableColumns(IsPrimaryKey);
GO

-- Obfuscation Configurations
CREATE INDEX IX_ObfuscationConfigurations_ProductId ON ObfuscationConfigurations(ProductId);
CREATE INDEX IX_ObfuscationConfigurations_IsActive ON ObfuscationConfigurations(IsActive);
CREATE INDEX IX_ObfuscationConfigurations_IsDefault ON ObfuscationConfigurations(IsDefault);
GO

-- Column Obfuscation Mappings
CREATE INDEX IX_ColumnObfuscationMappings_ProductId ON ColumnObfuscationMappings(ProductId);
CREATE INDEX IX_ColumnObfuscationMappings_TableColumnId ON ColumnObfuscationMappings(TableColumnId);
CREATE INDEX IX_ColumnObfuscationMappings_IsEnabled ON ColumnObfuscationMappings(IsEnabled);
GO

-- User Product Permissions
CREATE INDEX IX_UserProductPermissions_UserId ON UserProductPermissions(UserId);
CREATE INDEX IX_UserProductPermissions_ProductId ON UserProductPermissions(ProductId);
GO

-- Audit Log
CREATE INDEX IX_AuditLog_UserId ON AuditLog(UserId);
CREATE INDEX IX_AuditLog_ProductId ON AuditLog(ProductId);
CREATE INDEX IX_AuditLog_CreatedAt ON AuditLog(CreatedAt);
GO

-- =============================================
-- Initial Data
-- =============================================

-- Insert default roles
INSERT INTO Roles (Name, Description) VALUES
('Admin', 'Full access to all products and configurations'),
('ProductAdmin', 'Admin access to specific products'),
('Analyst', 'Can view and analyze schemas'),
('Viewer', 'Read-only access to assigned products');
GO

-- Insert default supported data types
INSERT INTO SupportedDataTypes (Name, Description) VALUES
('DriverName', 'Person names (first, last, full names)'),
('ContactEmail', 'Email addresses'),
('DriverPhone', 'Phone numbers'),
('AddressLine1', 'Street addresses'),
('AddressLine2', 'Secondary address information'),
('City', 'City names'),
('PostCode', 'Postal/ZIP codes'),
('CreditCard', 'Credit card numbers'),
('LicenseNumber', 'Driver license and permit numbers'),
('DateOfBirth', 'Birth dates'),
('SSN', 'Social Security Numbers'),
('TaxFileNumber', 'Tax identification numbers'),
('BusinessNumber', 'Business registration numbers'),
('BankAccount', 'Bank account numbers'),
('IPAddress', 'IP addresses'),
('URL', 'Web URLs'),
('UserName', 'User login names'),
('Password', 'Password fields'),
('Comments', 'General comments and notes'),
('Description', 'Descriptive text fields');
GO

-- Insert default PII detection rules
INSERT INTO PIIDetectionRules (DataType, ObfuscationDataType, ColumnNamePatterns, SqlDataTypes, TableNamePatterns, BaseConfidence, PreserveLength) VALUES
('PersonName', 'DriverName', '["*name*", "*first*", "*last*", "*middle*", "*full*", "*display*"]', '["varchar", "nvarchar", "char", "nchar"]', '["*person*", "*employee*", "*customer*", "*contact*"]', 0.7, 1),
('EmailAddress', 'ContactEmail', '["*email*", "*mail*", "*e_mail*", "*emailaddress*"]', '["varchar", "nvarchar", "text"]', '["*person*", "*contact*", "*user*"]', 0.8, 1),
('PhoneNumber', 'DriverPhone', '["*phone*", "*mobile*", "*cell*", "*tel*", "*telephone*"]', '["varchar", "nvarchar", "char"]', '["*person*", "*contact*", "*phone*"]', 0.8, 1),
('Address', 'AddressLine1', '["*address*", "*street*", "*line1*"]', '["varchar", "nvarchar", "text"]', '["*address*", "*contact*", "*location*"]', 0.7, 0),
('City', 'City', '["*city*", "*town*", "*municipality*"]', '["varchar", "nvarchar", "char"]', '["*address*", "*location*", "*city*"]', 0.6, 0),
('PostalCode', 'PostCode', '["*postal*", "*zip*", "*postcode*", "*zipcode*"]', '["varchar", "nvarchar", "char"]', '["*address*", "*location*"]', 0.7, 0),
('CreditCard', 'CreditCard', '["*card*", "*credit*", "*cc*", "*cardnumber*"]', '["varchar", "nvarchar", "char"]', '["*payment*", "*credit*", "*card*"]', 0.9, 1),
('DateOfBirth', 'DateOfBirth', '["*birth*", "*dob*", "*born*", "*birthdate*"]', '["date", "datetime", "datetime2"]', '["*person*", "*employee*", "*customer*"]', 0.8, 0);
GO

-- =============================================
-- Views for Common Queries
-- =============================================

-- Product Summary View
CREATE VIEW vw_ProductSummary AS
SELECT 
    p.Id,
    p.Name,
    p.Description,
    p.DatabaseTechnology,
    p.IsActive,
    COUNT(DISTINCT ds.Id) as TableCount,
    COUNT(DISTINCT tc.Id) as ColumnCount,
    COUNT(DISTINCT CASE WHEN com.IsEnabled = 1 THEN com.Id END) as ObfuscatedColumnsCount,
    p.CreatedAt,
    p.UpdatedAt
FROM Products p
LEFT JOIN DatabaseSchemas ds ON p.Id = ds.ProductId
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
GROUP BY p.Id, p.Name, p.Description, p.DatabaseTechnology, p.IsActive, p.CreatedAt, p.UpdatedAt;
GO

-- Schema Analysis Summary View
CREATE VIEW vw_SchemaAnalysisSummary AS
SELECT 
    p.Name as ProductName,
    p.Id as ProductId,
    COUNT(DISTINCT ds.Id) as TotalTables,
    COUNT(DISTINCT tc.Id) as TotalColumns,
    COUNT(DISTINCT CASE WHEN ds.IsAnalyzed = 1 THEN ds.Id END) as AnalyzedTables,
    COUNT(DISTINCT CASE WHEN com.IsEnabled = 1 THEN com.Id END) as ObfuscatedColumns,
    MAX(ds.AnalyzedAt) as LastAnalyzedAt
FROM Products p
LEFT JOIN DatabaseSchemas ds ON p.Id = ds.ProductId
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
GROUP BY p.Name, p.Id;
GO

-- =============================================
-- Stored Procedures
-- =============================================

-- Get Product Configuration as JSON
CREATE PROCEDURE sp_GetProductConfiguration
    @ProductId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id as ProductId,
        p.Name as ProductName,
        p.ConnectionString,
        p.DatabaseTechnology,
        p.GlobalSeed,
        p.BatchSize,
        p.SqlBatchSize,
        p.ParallelThreads,
        p.MaxCacheSize,
        p.CommandTimeoutSeconds,
        p.MappingCacheDirectory,
        (
            SELECT 
                ds.SchemaName,
                ds.TableName,
                ds.FullTableName,
                ds.PrimaryKeyColumns,
                ds.[RowCount],
                (
                    SELECT 
                        tc.ColumnName,
                        tc.SqlDataType,
                        tc.MaxLength,
                        tc.IsNullable,
                        tc.IsPrimaryKey,
                        com.ObfuscationDataType,
                        com.IsEnabled,
                        com.PreserveLength,
                        com.ConfidenceScore
                    FROM TableColumns tc
                    LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
                    WHERE tc.DatabaseSchemaId = ds.Id
                    FOR JSON PATH
                ) as Columns
            FROM DatabaseSchemas ds
            WHERE ds.ProductId = p.Id
            FOR JSON PATH
        ) as Tables
    FROM Products p
    WHERE p.Id = @ProductId
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
END;
GO

-- =============================================
-- Triggers for Audit Logging
-- =============================================

-- Audit trigger for Products table
CREATE TRIGGER tr_Products_Audit
ON Products
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Action NVARCHAR(10);
    DECLARE @UserId NVARCHAR(255) = SYSTEM_USER;
    
    IF EXISTS(SELECT * FROM INSERTED) AND EXISTS(SELECT * FROM DELETED)
        SET @Action = 'UPDATE';
    ELSE IF EXISTS(SELECT * FROM INSERTED)
        SET @Action = 'INSERT';
    ELSE
        SET @Action = 'DELETE';
    
    INSERT INTO AuditLog (Action, EntityType, EntityId, OldValues, NewValues)
    SELECT 
        @Action,
        'Product',
        ISNULL(i.Id, d.Id),
        CASE WHEN @Action IN ('UPDATE', 'DELETE') THEN (SELECT * FROM DELETED FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) ELSE NULL END,
        CASE WHEN @Action IN ('INSERT', 'UPDATE') THEN (SELECT * FROM INSERTED FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) ELSE NULL END
    FROM INSERTED i
    FULL OUTER JOIN DELETED d ON i.Id = d.Id;
END;
GO
