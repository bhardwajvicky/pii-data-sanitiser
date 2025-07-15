# Selective Caching Strategy Implementation

## Overview
The data obfuscation system now implements a selective caching strategy that only caches low-cardinality (frequently repeated) data types while skipping high-cardinality (unique) data types. This optimization significantly reduces memory usage and improves performance for large-scale databases.

## Implementation Details

### 1. Cache Configuration (`CacheConfiguration.cs`)
- **Purpose**: Centralized configuration defining which data types should be cached
- **Location**: `/data-obfuscation/Configuration/CacheConfiguration.cs`
- **Key Components**:
  - `AlwaysCacheDataTypes`: Low-cardinality fields that benefit from caching
  - `NeverCacheDataTypes`: High-cardinality fields that should not be cached
  - `ShouldCache()`: Helper method to determine caching behavior

### 2. Modified Providers
Both Australian and UK providers now support selective caching:

#### DeterministicAustralianProvider
- **Cached Data Types**:
  - Names: FirstName, LastName, DriverName
  - Geographic: City, State, StateAbbr, Country, PostCode
  - Business: OperatorName, VehicleMakeModel
  - Operations: RouteCode, DepotLocation

- **Not Cached Data Types**:
  - Addresses: AddressLine1, AddressLine2, FullAddress, GPSCoordinate
  - Identifiers: DriverLicense, VehicleRegistration, VIN, EngineNumber, ABN, ACN
  - Contact: Email, Phone
  - Financial: CreditCard

#### DeterministicUKProvider
- **Cached Data Types**:
  - Names: FirstName, LastName, FullName
  - Geographic: UKPostcode
  - Business: CompanyName

- **Not Cached Data Types**:
  - Contact: Email, Phone
  - Addresses: Address
  - Financial: CreditCard, BankSortCode
  - Identifiers: NINO, VehicleRegistration

### 3. GetOrCreateMapping Method Enhancement
The core mapping method now accepts a `shouldCache` parameter:
```csharp
private string GetOrCreateMapping(string key, string? customSeed, Func<string> generator, bool shouldCache = true)
```

When `shouldCache` is false, the method:
- Bypasses the cache entirely
- Generates values directly using the deterministic algorithm
- Maintains cross-database consistency through seeding

### 4. Benefits for Large Databases

For a 1TB database with millions of rows:

#### Memory Savings
- **Before**: Caching all values could consume gigabytes of RAM
- **After**: Only ~10-20% of values are cached (low-cardinality fields)
- **Example**: 100M records → ~5-10M cached entries vs 100M

#### Performance Impact
- **Cached fields**: Near-instant lookups for repeated values
- **Non-cached fields**: Slight overhead but prevents memory exhaustion
- **Overall**: More stable performance without OOM errors

#### Storage Savings
- **Mapping files**: Reduced from potential GBs to MBs
- **Load times**: Faster startup with smaller cache files
- **Network**: Easier to transfer mapping files between systems

### 5. Cache Statistics and Monitoring
The system now logs cache statistics when saving mappings:
```
Cache statistics - Total entries: 45,230
  FirstName: 8,542 entries
  LastName: 12,893 entries
  City: 4,371 entries
  State: 8 entries
  ...
```

### 6. Backward Compatibility
The `LoadMappingsAsync` method filters out high-cardinality entries when loading old mapping files, ensuring smooth migration to the selective caching strategy.

## Configuration Examples

### For Maximum Performance (More Caching)
```json
{
  "EnableValueCaching": true,
  "MaxCacheSize": 1000000,
  "PersistMappings": true
}
```

### For Minimum Memory (Less Caching)
```json
{
  "EnableValueCaching": true,
  "MaxCacheSize": 100000,
  "PersistMappings": false
}
```

### To Disable Caching Entirely
```json
{
  "EnableValueCaching": false
}
```

## Best Practices

1. **Monitor Cache Hit Rates**: Use logging to track which data types benefit most from caching
2. **Adjust Cache Size**: Set `MaxCacheSize` based on available memory
3. **Regular Cache Cleanup**: Delete old mapping files after successful runs
4. **Test Performance**: Measure impact on your specific data patterns

## Future Enhancements

1. **Dynamic Cache Sizing**: Automatically adjust based on available memory
2. **Cache Warmup**: Pre-load common values for better initial performance
3. **Tiered Caching**: Use Redis/Memcached for L2 cache
4. **Compression**: Store cached values in compressed format
5. **Smart Eviction**: Track usage patterns and evict least-used entries