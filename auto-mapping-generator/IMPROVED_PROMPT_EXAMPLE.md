# Improved LLM Prompt Example

## Current Prompt Being Sent

```
You are an expert data privacy consultant analyzing a database schema to identify columns that likely contain Personally Identifiable Information (PII).

Please analyze the following database schema and identify columns that likely contain PII data:

Database: AdventureWorks
Table: Sales.SalesPerson
Row Count: 17
Columns:
  - BusinessEntityID (int) NOT NULL PRIMARY KEY FOREIGN KEY
  - TerritoryID (int) NULL FOREIGN KEY
  - SalesQuota (money) NULL
  - Bonus (money) NOT NULL
  - CommissionPct (smallmoney) NOT NULL
  - SalesYTD (money) NOT NULL
  - SalesLastYear (money) NOT NULL
  - rowguid (uniqueidentifier) NOT NULL
  - ModifiedDate (datetime) NOT NULL

For each column you identify as containing PII, classify it into one of these categories:
- FirstName: First names, given names only (NOT full names)
- LastName: Last names, surnames only (NOT full names)
- FullName: Full names, display names, complete personal names
- Email: Email addresses
- Phone: Phone numbers, contact numbers
- AddressLine1: Primary address line (street number + name)
- AddressLine2: Secondary address line (apartment, unit, suite)
- City: City/Town names
- State: State/Province/County names
- PostCode: Postal codes/ZIP codes
- Country: Country names
- CreditCard: Credit card numbers
- LicenseNumber: License numbers, permit IDs
- CompanyName: Company/operator names
- BusinessABN: Australian Business Numbers
- BusinessACN: Australian Company Numbers
- VehicleRegistration: Vehicle registration plates
- Date: Personal dates ONLY (anniversary, hire date, termination date)
- DateOfBirth: Date of birth, birthdate ONLY

IMPORTANT ADDRESS MAPPING GUIDELINES:
- Use AddressLine1 for: Address, Address1, StreetAddress, Street
- Use AddressLine2 for: Address2, Unit, Apt, Suite, Level
- Use City for: City, Town, Municipality
- Use Suburb for: Suburb (Australian context)
- Use State for: State, Province, Region
- Use PostCode for: PostCode, ZipCode, PostalCode
- Use FullAddress for: FullAddress, CompleteAddress

IMPORTANT NAME MAPPING GUIDELINES:
- Use FirstName ONLY for: FirstName, GivenName, FName
- Use LastName ONLY for: LastName, Surname, FamilyName, LName
- Use FullName for: Name, DisplayName, PersonName, CustomerName

IMPORTANT DATE MAPPING GUIDELINES:
- Use DateOfBirth for: DOB, DateOfBirth, BirthDate, Birthday, Born
- Use Date ONLY for personal dates: Anniversary, HireDate, TerminationDate
- NEVER use Date for: ModifiedDate, CreatedDate, OrderDate, ShipDate, DueDate, etc.

CRITICAL EXCLUSION RULES:
- DO NOT identify as PII: Sales figures, amounts, quantities, metrics, statistics
- DO NOT identify as PII: System dates like ModifiedDate, CreatedDate, UpdatedDate
- DO NOT identify as PII: Business metrics like SalesLastYear, Revenue, Count
- DO NOT identify as PII: IDs that are just numbers (PersonID, CustomerID, etc.)
- DO NOT identify as PII: Status fields, flags, types, categories
- DO NOT identify as PII: Technical fields like versions, checksums, hashes

Only include columns where confidence >= 0.7. Focus on actual PII data that identifies or contacts individuals.

Please respond in JSON format with an array of objects containing:
- tableName: Full table name (schema.table)
- columnName: Column name
- piiType: One of the categories above (exact match required)
- confidence: Confidence level (0.0-1.0)
- reasoning: Brief explanation why this column contains PII
```

## Issues with Current Detection

1. **SalesLastYear** (money type) → Incorrectly identified as **FirstName**
   - This is a business metric, not a name
   - The data type is `money`, not suitable for names

2. **ModifiedDate** (datetime) → Incorrectly identified as **Date** (PII)
   - This is a system timestamp, not personal information
   - Should be excluded based on the rules

3. **CreditCardID** (int) → Incorrectly identified as **CreditCard**
   - This is likely a foreign key ID, not an actual credit card number
   - Integer type is not suitable for credit card numbers

4. **PersonID** (int) → Incorrectly identified as **FirstName**
   - This is just a numeric ID reference
   - Should be excluded based on the ID exclusion rule

5. **AccountNumber** → Incorrectly identified as **Phone**
   - Account numbers are not phone numbers
   - Need better distinction between different types of identifiers

## Recommended Prompt Improvements

1. Add data type validation:
   ```
   IMPORTANT DATA TYPE RULES:
   - Names (FirstName, LastName, FullName) must be text types (varchar, nvarchar)
   - Phone numbers must be text types, not numeric types
   - Credit card numbers must be text types (varchar), not int
   - Money/decimal types are NEVER personal names
   ```

2. Add column name negative patterns:
   ```
   EXCLUDE THESE PATTERNS:
   - Columns ending with: *ID, *YTD, *LastYear, *Count, *Amount, *Total
   - Columns starting with: Total*, Sum*, Count*, Avg*, Min*, Max*
   - System columns: Modified*, Created*, Updated*, Deleted*, rowguid
   ```

3. Add context awareness:
   ```
   CONTEXT RULES:
   - In Sales/Financial tables, assume columns relate to business metrics unless clearly personal
   - ID columns are references, not the actual PII data
   - Consider the table's purpose when evaluating columns
   ```