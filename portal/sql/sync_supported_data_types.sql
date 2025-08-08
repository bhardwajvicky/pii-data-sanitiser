USE obfuscate;
GO

SET NOCOUNT ON;

-- This script aligns SupportedDataTypes table with the types defined in code (Common/DataTypes/SupportedDataTypes.cs)
-- Policy: Insert/Update expected types and set IsActive = 1; deactivate (IsActive = 0) any types not in the expected list.

DECLARE @Expected TABLE (
    Name NVARCHAR(100) PRIMARY KEY,
    Description NVARCHAR(MAX)
);

INSERT INTO @Expected (Name, Description) VALUES
    -- Core Personal Data Types
    ('FirstName', 'Given names only (NOT full names)'),
    ('LastName', 'Surnames only (NOT full names)'),
    ('FullName', 'Full personal name/display name'),

    -- Contact Information
    ('Email', 'Email addresses'),
    ('Phone', 'Phone numbers'),

    -- Address Components
    ('FullAddress', 'Full address (all components combined)'),
    ('AddressLine1', 'Primary address line (street number + name)'),
    ('AddressLine2', 'Secondary address line (apartment/unit/suite)'),
    ('City', 'City/Suburb/Town'),
    ('Suburb', 'Alternative name for City (AU)'),
    ('State', 'State/Province/County'),
    ('StateAbbr', 'State abbreviation (e.g., NSW, VIC)'),
    ('PostCode', 'Postal/ZIP code'),
    ('ZipCode', 'Alternative name for PostCode'),
    ('Country', 'Country name'),
    ('Address', 'Legacy address type - use specific components instead'),

    -- Date Information
    ('Date', 'General date value'),
    ('DateOfBirth', 'Date of Birth specifically'),

    -- Financial Information
    ('CreditCard', 'Credit card numbers'),
    ('NINO', 'UK National Insurance Number'),
    ('NationalInsuranceNumber', 'UK National Insurance Number (alias)'),
    ('SortCode', 'UK bank sort code'),
    ('BankSortCode', 'UK bank sort code (alias)'),

    -- Identification & Licenses
    ('LicenseNumber', 'License numbers, permit IDs'),

    -- Business Information
    ('CompanyName', 'Company/operator names'),
    ('BusinessABN', 'Australian Business Number (ABN)'),
    ('BusinessACN', 'Australian Company Number (ACN)'),

    -- Vehicle Information
    ('VehicleRegistration', 'Vehicle registration plates'),
    ('VINNumber', 'Vehicle identification numbers'),
    ('VehicleMakeModel', 'Vehicle make and model information'),
    ('EngineNumber', 'Engine identification numbers'),

    -- Location & Geographic
    ('GPSCoordinate', 'GPS coordinates, location data'),
    ('RouteCode', 'Route identifiers'),
    ('DepotLocation', 'Depot/facility locations'),

    -- UK-Specific Types
    ('UKPostcode', 'UK postal codes (e.g., SW1A 1AA)');

-- Upsert expected types and activate them
MERGE SupportedDataTypes AS target
USING @Expected AS src
    ON target.Name = src.Name
WHEN MATCHED THEN
    UPDATE SET
        target.Description = src.Description,
        target.IsActive = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Description, IsActive)
    VALUES (src.Name, src.Description, 1)
WHEN NOT MATCHED BY SOURCE THEN
    UPDATE SET target.IsActive = 0
OUTPUT $action AS MergeAction, inserted.Name AS InsertedName, deleted.Name AS DeletedName;
GO

-- Optional: review the current state
PRINT 'Active Supported Data Types (should match code):';
SELECT Name, Description FROM SupportedDataTypes WHERE IsActive = 1 ORDER BY Name;

PRINT 'Deactivated (not present in code):';
SELECT Name FROM SupportedDataTypes WHERE IsActive = 0 ORDER BY Name;
GO


