# PII Data Sanitizer - AI Assistant Context

## Project Overview
This is a high-performance data obfuscation solution for SQL Server databases, designed for cross-database deterministic obfuscation with a focus on Australian fleet management industry data.

## Key Requirements
1. **Deterministic Obfuscation**: Same input MUST always produce same output across all databases
2. **Cross-Database Consistency**: Values must match across different databases for referential integrity
3. **Australian Data Focus**: Generate realistic Australian addresses, phone numbers, ABNs, etc.
4. **High Performance**: Handle 1TB+ databases with millions of rows efficiently

## Architecture Summary
- **Auto Mapping Generator**: Discovers PII columns automatically using pattern matching
- **Data Obfuscation Engine**: Performs the actual data transformation
- **Common**: Shared models and data types library
- **Selective Caching**: Only caches low-cardinality fields to manage memory

## Critical Implementation Details

### Deterministic Generation
- Uses SHA-256 hashing with global seed + custom seed + original value
- Bogus library with deterministic Randomizer for consistent fake data
- Cumulative seeding for address components to avoid collisions

### Performance Optimizations
- Parallel processing with configurable threads (default: 8)
- Batch processing (default: 1000 rows)
- Selective caching (only ~5% of values cached)
- Connection pooling and command timeout handling

### Data Type Handling
- **Cached**: Names, cities, states, postcodes, company names
- **Not Cached**: Addresses, emails, phones, IDs, credit cards
- Length constraints enforced (e.g., City max 30 chars)

## Common Commands

### Build and Test
```bash
dotnet build
dotnet test
```

### Run Schema Analysis
```bash
cd auto-mapping-generator
dotnet run "Server=localhost;Database=AdventureWorks;Integrated Security=true;"
```

### Run Obfuscation
```bash
cd data-obfuscation
dotnet run ../JSON/mapping.json ../JSON/config.json --dry-run
```

### Clear Cache (Important!)
```bash
rm -rf mappings/*/*.json
```

## Known Issues & Solutions

### Duplicate Key Violations
- **Cause**: Cached values from previous runs
- **Solution**: Clear mapping cache before runs

### String Truncation Errors
- **Cause**: Generated values exceed column length
- **Solution**: Length constraints implemented in providers

### Memory Issues
- **Cause**: Too many cached values
- **Solution**: Selective caching implemented

## Testing Approach
1. Always use `--dry-run` first
2. Test with small batches initially
3. Monitor cache statistics in logs
4. Verify deterministic behavior across runs

## File Locations
- **Configs**: `/JSON/*.json`
- **Logs**: `/logs/obfuscation-*.log`
- **Reports**: `/reports/*-obfuscation-*.json`
- **Cache**: `/mappings/*/mappings_*.json`
- **Data Files**: `/data-obfuscation/Data/AU/*.txt`

## Performance Expectations
- **Small DB (< 1GB)**: ~2000 rows/sec
- **Medium DB (1-10GB)**: ~1800 rows/sec
- **Large DB (10-100GB)**: ~1500 rows/sec
- **Cache Hit Rate**: 80-90% for cached types

## When Modifying Code
1. Maintain deterministic behavior at all costs
2. Test with problematic addresses: "4022 H Pine Creek Way", "1522 Azalea Ave."
3. Ensure length constraints are respected
4. Clear cache after provider changes
5. Run in dry-run mode first

## Critical Files
- `/data-obfuscation/Data/DeterministicAustralianProvider.cs` - Main provider logic
- `/data-obfuscation/Configuration/CacheConfiguration.cs` - Caching rules
- `/auto-mapping-generator/Core/ObfuscationConfigGenerator.cs` - Config generation
- `/data-obfuscation/Core/ObfuscationEngine.cs` - Main processing engine