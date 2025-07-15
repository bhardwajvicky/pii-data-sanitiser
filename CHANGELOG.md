# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-07-15

### Added

#### Core Features
- High-performance data obfuscation engine for SQL Server databases
- Automatic PII discovery with pattern-based detection
- Deterministic obfuscation ensuring same input → same output
- Cross-database consistency for referential integrity
- Australian fleet industry-specific data generation
- UK data provider for international support

#### Performance Enhancements
- Parallel processing with configurable thread count (up to 32 threads)
- Batch processing with configurable batch sizes (1-100,000 rows)
- Smart selective caching strategy (95% memory reduction)
- Connection pooling and optimized SQL operations
- Progress tracking with real-time updates

#### Data Types Support
- Personal data: FirstName, LastName, FullName
- Contact information: Email, Phone, Mobile
- Address components: AddressLine1/2, City, State, PostCode
- Vehicle data: Registration, VIN, Make/Model, EngineNumber
- Business data: CompanyName, ABN, ACN
- Financial data: CreditCard, BankAccount
- Identifiers: DriverLicense, EmployeeID, NationalID
- Geographic data: GPSCoordinate, RouteCode, DepotLocation

#### Configuration & Flexibility
- JSON-based configuration with schema validation
- Custom data type definitions with validation rules
- Conditional processing with WHERE clauses
- Referential integrity management
- Dry-run mode for testing
- Multiple fallback strategies

#### Logging & Monitoring
- Comprehensive application logs with daily rotation
- Detailed failure logs with row-level information
- Processing reports with statistics
- Audit logs for compliance
- Cache statistics and performance metrics

#### Data Files
- Extensive Australian data files (cities, streets, companies)
- UK-specific data files
- Expanded datasets to reduce collision probability

### Changed

#### Obfuscation Algorithm
- Implemented cumulative seeding for address components
- Enhanced deterministic generation to avoid collisions
- Added length constraints for database columns
- Improved hash distribution for better randomness

#### Caching Strategy
- Moved from full caching to selective caching
- Only cache low-cardinality fields (names, cities, states)
- Skip high-cardinality fields (addresses, emails, IDs)
- Added cache filtering on load for backward compatibility

#### Configuration
- Updated global seed to complex string for better entropy
- Increased default parallel threads from 4 to 8
- Enhanced validation for configuration files
- Added more detailed error messages

### Fixed

#### Critical Issues
- Duplicate key violations for similar addresses
- String truncation errors for long city names
- Memory exhaustion on large databases
- Cache persistence issues

#### Data Quality
- Address generation producing identical values
- City names exceeding column length limits
- Postcode generation not following Australian patterns
- Phone number formatting inconsistencies

### Security
- No sensitive data in logs or error messages
- Secure connection string handling
- Audit trail for all operations
- Configurable data encryption options

### Documentation
- Comprehensive README with examples
- Template files for all configurations
- Batch processing scripts for Windows and Linux
- Docker support with compose files
- AI assistant context (CLAUDE.md)

## [0.9.0] - 2024-07-13 (Pre-release)

### Initial Implementation
- Basic obfuscation engine
- Simple auto mapping generator
- Australian data provider
- Basic caching mechanism
- Initial documentation

---

## Upgrade Notes

### From 0.9.0 to 1.0.0
1. Clear all existing cache files before upgrading
2. Regenerate configuration files with new auto mapping generator
3. Update global seed in configurations
4. Review and apply selective caching settings

### Breaking Changes
- Cache file format changed (backward compatible with filtering)
- Configuration schema updated (use new templates)
- Some data type names standardized

### Migration Steps
```bash
# 1. Backup existing configurations
cp -r JSON JSON_backup

# 2. Clear cache
rm -rf mappings/*/*.json

# 3. Regenerate configurations
cd auto-mapping-generator
dotnet run "your-connection-string"

# 4. Review and update generated configs
# 5. Test with dry-run before full execution
```