# LLM Prompt Flow Analysis

## Overview
The system sends **ALL TABLES AT ONCE** to the LLM in a single API call, not one table at a time.

## Flow Diagram

```
┌─────────────────────┐
│   Program.cs        │
│  (Entry Point)      │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ SchemaAnalysisService│
│ Reads ALL tables    │
│ from database       │
│ (SCHEMA ONLY)       │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────────┐
│ DatabaseSchema object   │
│ Contains:               │
│ - Database name         │
│ - ALL tables            │
│ - ALL columns per table │
│ - Column metadata       │
└─────────┬───────────────┘
          │
          ▼
┌─────────────────────────┐
│ EnhancedPIIDetection    │
│ Service                 │
└─────────┬───────────────┘
          │
          ▼
┌─────────────────────────┐
│ LLM Service (Claude or  │
│ Azure OpenAI)           │
│ - Builds ONE prompt     │
│ - Sends ALL tables      │
│ - Gets ONE response     │
└─────────────────────────┘
```

## What's Sent to LLM

### 1. Schema Description (ALL TABLES)
```
Database: AdventureWorks
Total Tables: 71

Table: dbo.Person
Columns:
  - PersonId (int) NOT NULL PRIMARY KEY
  - FirstName (nvarchar) MAX:50
  - LastName (nvarchar) MAX:50
  - DateOfBirth (datetime) NULL
  - Email (nvarchar) MAX:100

Table: dbo.Address
Columns:
  - AddressId (int) NOT NULL PRIMARY KEY
  - AddressLine1 (nvarchar) MAX:60
  - AddressLine2 (nvarchar) MAX:60 NULL
  - City (nvarchar) MAX:30
  - StateProvinceId (int) NOT NULL FOREIGN KEY
  - PostalCode (nvarchar) MAX:15

[... ALL OTHER TABLES ...]
```

### 2. Full Prompt Structure
```
You are an expert data privacy consultant analyzing a database schema to identify columns that likely contain Personally Identifiable Information (PII).

Please analyze the following database schema and identify columns that likely contain PII data:

[ENTIRE DATABASE SCHEMA - ALL TABLES]

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
- CreditCard: Credit card numbers
- LicenseNumber: License numbers, permit IDs
- CompanyName: Company/operator names
- Date: General date fields (anniversary, hire date, etc.)
- DateOfBirth: Date of birth, birthdate
[... more data types ...]

IMPORTANT ADDRESS MAPPING GUIDELINES:
- Use AddressLine1 for: Address, Address1, StreetAddress, Street
- Use AddressLine2 for: Address2, Unit, Apt, Suite, Level
[... more guidelines ...]

Please respond in JSON format with an array of objects containing:
- tableName: Full table name (schema.table)
- columnName: Column name
- piiType: One of the categories above (exact match required)
- confidence: Confidence level (0.0-1.0)
- reasoning: Brief explanation why this column contains PII

Only include columns where confidence >= 0.7. Focus on actual PII data, not technical IDs or system fields.
```

### 3. LLM Response (ALL PII COLUMNS AT ONCE)
```json
[
  {
    "tableName": "dbo.Person",
    "columnName": "FirstName",
    "piiType": "FirstName",
    "confidence": 0.95,
    "reasoning": "Column clearly contains personal first names"
  },
  {
    "tableName": "dbo.Person",
    "columnName": "LastName",
    "piiType": "LastName",
    "confidence": 0.95,
    "reasoning": "Column contains surnames"
  },
  {
    "tableName": "dbo.Person",
    "columnName": "DateOfBirth",
    "piiType": "DateOfBirth",
    "confidence": 0.98,
    "reasoning": "Column name explicitly indicates date of birth"
  },
  {
    "tableName": "dbo.Address",
    "columnName": "AddressLine1",
    "piiType": "AddressLine1",
    "confidence": 0.92,
    "reasoning": "Primary address line containing street information"
  }
  // ... ALL OTHER PII COLUMNS FROM ALL TABLES ...
]
```

## Key Points

1. **NO DATA IS SENT** - Only schema structure (table names, column names, data types, constraints)
2. **ALL TABLES AT ONCE** - The entire database schema is sent in a single LLM call
3. **SINGLE RESPONSE** - LLM returns all PII columns from all tables in one response
4. **SCHEMA ONLY** - No actual data values are fetched or sent to the LLM
5. **LIMITS** - Azure OpenAI limits to first 100 tables to prevent token overflow

## Token Optimization

For large databases:
- Azure OpenAI: Takes first 100 tables only
- Claude: Sends all tables (no explicit limit in code)
- Both include column metadata (data type, nullability, max length, keys)

## Performance Implications

- **Pros**: Single API call is more efficient than multiple calls
- **Cons**: Large databases might hit token limits
- **Mitigation**: Azure OpenAI limits to 100 tables; could implement batching for very large schemas