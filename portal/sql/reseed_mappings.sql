USE obfuscate;
GO

DECLARE @pid UNIQUEIDENTIFIER = (SELECT Id FROM Products WHERE Name = 'adv');
IF @pid IS NULL
BEGIN
    RAISERROR('Product adv not found',16,1);
    RETURN;
END

-- Clear existing mappings for this product
DELETE FROM ColumnObfuscationMappings WHERE ProductId = @pid;

-- Employee
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'LicenseNumber',1,1,0.8,'["Column name contains ID pattern", "License number format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Employee' AND tc.ColumnName='NationalIDNumber';

INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'DateOfBirth',1,0,0.9,'["Column name contains birth pattern", "Date data type"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Employee' AND tc.ColumnName='BirthDate';

-- PersonPhone
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'DriverPhone',1,1,0.9,'["Column name contains phone pattern", "Phone number format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='PersonPhone' AND tc.ColumnName='PhoneNumber';

-- Address
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'AddressLine1',1,0,0.8,'["Column name contains address pattern", "Address format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Address' AND tc.ColumnName='AddressLine1';

INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'AddressLine1',1,0,0.7,'["Column name contains address pattern", "Secondary address"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Address' AND tc.ColumnName='AddressLine2';

INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'City',1,0,0.8,'["Column name contains city pattern", "City name format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Address' AND tc.ColumnName='City';

INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'PostCode',1,0,0.8,'["Column name contains postal pattern", "Postal code format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='Address' AND tc.ColumnName='PostalCode';

-- EmailAddress
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'ContactEmail',1,0,0.9,'["Column name contains email pattern", "Email format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='EmailAddress' AND tc.ColumnName='EmailAddress';

-- CreditCard
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'CreditCard',1,1,0.9,'["Column name contains card pattern", "Credit card format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='CreditCard' AND tc.ColumnName='CardNumber';

-- ProductReview
INSERT INTO ColumnObfuscationMappings (Id,ProductId,TableColumnId,ObfuscationDataType,IsEnabled,PreserveLength,ConfidenceScore,DetectionReasons,IsManuallyConfigured,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy)
SELECT NEWID(), p.Id, tc.Id, 'ContactEmail',1,0,0.8,'["Column name contains email pattern", "Email format"]',0,GETDATE(),GETDATE(),'System','System'
FROM TableColumns tc
JOIN DatabaseSchemas ds ON tc.DatabaseSchemaId = ds.Id
JOIN Products p ON ds.ProductId = p.Id
WHERE p.Id=@pid AND ds.TableName='ProductReview' AND tc.ColumnName='EmailAddress';

-- Summary
SELECT ds.FullTableName, COUNT(com.Id) AS MappedCols
FROM DatabaseSchemas ds
JOIN Products p ON ds.ProductId = p.Id
LEFT JOIN TableColumns tc ON ds.Id = tc.DatabaseSchemaId
LEFT JOIN ColumnObfuscationMappings com ON tc.Id = com.TableColumnId
WHERE p.Id = @pid
GROUP BY ds.FullTableName
ORDER BY MappedCols DESC;
GO
