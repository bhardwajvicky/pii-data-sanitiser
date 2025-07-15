using Bogus;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using DataObfuscation.Configuration;

namespace DataObfuscation.Data;

public interface IDeterministicAustralianProvider
{
    string GetDriverName(string originalValue, string? customSeed = null);
    string GetFirstName(string originalValue, string? customSeed = null);
    string GetLastName(string originalValue, string? customSeed = null);
    string GetDriverLicenseNumber(string originalValue, string? customSeed = null);
    string GetContactEmail(string originalValue, string? customSeed = null);
    string GetDriverPhone(string originalValue, string? customSeed = null);
    string GetVehicleRegistration(string originalValue, string? customSeed = null);
    string GetVINNumber(string originalValue, string? customSeed = null);
    string GetVehicleMakeModel(string originalValue, string? customSeed = null);
    string GetEngineNumber(string originalValue, string? customSeed = null);
    string GetOperatorName(string originalValue, string? customSeed = null);
    string GetBusinessABN(string originalValue, string? customSeed = null);
    string GetBusinessACN(string originalValue, string? customSeed = null);
    string GetAddress(string originalValue, string? customSeed = null);
    string GetFullAddress(string originalValue, string? customSeed = null);
    string GetAddressLine1(string originalValue, string? customSeed = null);
    string GetAddressLine2(string originalValue, string? customSeed = null);
    string GetCity(string originalValue, string? customSeed = null);
    string GetSuburb(string originalValue, string? customSeed = null);
    string GetState(string originalValue, string? customSeed = null);
    string GetStateAbbr(string originalValue, string? customSeed = null);
    string GetPostCode(string originalValue, string? customSeed = null);
    string GetCountry(string originalValue, string? customSeed = null);
    string GetGPSCoordinate(string originalValue, string? customSeed = null);
    string GetRouteCode(string originalValue, string? customSeed = null);
    string GetDepotLocation(string originalValue, string? customSeed = null);
    string GetCreditCard(string originalValue, string? customSeed = null);
    
    Dictionary<string, string> GetAllMappings();
    void ClearCache();
    Task SaveMappingsAsync(string directory);
    Task LoadMappingsAsync(string directory);
}

public class DeterministicAustralianProvider : IDeterministicAustralianProvider
{
    private readonly ILogger<DeterministicAustralianProvider> _logger;
    private readonly string _globalSeed;
    private readonly ConcurrentDictionary<string, string> _mappingCache;
    
    // Data is now loaded from files via DataFileLoader
    private static string[] AustralianStates => DataFileLoader.AU.States;
    private static string[] CompanySuffixes => DataFileLoader.AU.CompanySuffixes;
    private static string[] VehicleMakes => DataFileLoader.AU.VehicleMakes;
    private static string[] VehicleModels => DataFileLoader.AU.VehicleModels;

    public DeterministicAustralianProvider(ILogger<DeterministicAustralianProvider> logger, string globalSeed = "DefaultSeed2024")
    {
        _logger = logger;
        _globalSeed = globalSeed;
        _mappingCache = new ConcurrentDictionary<string, string>();
    }

    public string GetDriverName(string originalValue, string? customSeed = null)
    {
        // Driver names are low cardinality - cache them
        return GetOrCreateMapping($"DriverName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.FullName();
        }, shouldCache: true);
    }

    public string GetFirstName(string originalValue, string? customSeed = null)
    {
        // First names are low cardinality - cache them
        return GetOrCreateMapping($"FirstName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.FirstName();
        }, shouldCache: true);
    }

    public string GetLastName(string originalValue, string? customSeed = null)
    {
        // Last names are low cardinality - cache them
        return GetOrCreateMapping($"LastName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.LastName();
        }, shouldCache: true);
    }

    public string GetDriverLicenseNumber(string originalValue, string? customSeed = null)
    {
        // Driver license numbers are unique identifiers - do not cache
        return GetOrCreateMapping($"DriverLicense_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var state = faker.PickRandom(AustralianStates);
            var number = faker.Random.Number(10000000, 99999999);
            return $"{state}-{number}";
        }, shouldCache: false);
    }

    public string GetContactEmail(string originalValue, string? customSeed = null)
    {
        // Emails are usually unique - do not cache
        return GetOrCreateMapping($"ContactEmail_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var domains = new[] { "transport.com.au", "logistics.net.au", "freight.com.au", "haulage.org.au" };
            var firstName = faker.Name.FirstName().ToLower();
            var lastName = faker.Name.LastName().ToLower();
            var domain = faker.PickRandom(domains);
            return $"{firstName}.{lastName}@{domain}";
        }, shouldCache: false);
    }

    public string GetDriverPhone(string originalValue, string? customSeed = null)
    {
        // Phone numbers are usually unique - do not cache
        return GetOrCreateMapping($"DriverPhone_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var number = faker.Random.Number(400000000, 499999999);
            var formatted = $"04{number.ToString()[2..]}";
            return $"{formatted[..4]} {formatted[4..7]} {formatted[7..]}";
        }, shouldCache: false);
    }

    public string GetVehicleRegistration(string originalValue, string? customSeed = null)
    {
        // Vehicle registrations are unique identifiers - do not cache
        return GetOrCreateMapping($"VehicleReg_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var state = faker.PickRandom(AustralianStates);
            
            return state switch
            {
                "NSW" or "VIC" or "QLD" => faker.Random.Bool() 
                    ? $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(100, 999)}"
                    : $"{faker.Random.Number(100, 999)}{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}",
                "WA" => $"1{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(100, 999)}",
                "SA" => $"S{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(100, 999)}",
                _ => $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(100, 999)}"
            };
        }, shouldCache: false);
    }

    public string GetVINNumber(string originalValue, string? customSeed = null)
    {
        // VIN numbers are unique identifiers - do not cache
        return GetOrCreateMapping($"VIN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var chars = "ABCDEFGHJKLMNPRSTUVWXYZ1234567890";
            return faker.Random.String2(17, chars);
        }, shouldCache: false);
    }

    public string GetVehicleMakeModel(string originalValue, string? customSeed = null)
    {
        // Vehicle make/model combinations are limited - cache them
        return GetOrCreateMapping($"VehicleMakeModel_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var make = faker.PickRandom(VehicleMakes);
            var model = faker.PickRandom(VehicleModels);
            return $"{make} {model}";
        }, shouldCache: true);
    }

    public string GetEngineNumber(string originalValue, string? customSeed = null)
    {
        // Engine numbers are unique identifiers - do not cache
        return GetOrCreateMapping($"Engine_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var prefix = faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            var number = faker.Random.Number(1000000, 9999999);
            return $"{prefix}{number}";
        }, shouldCache: false);
    }

    public string GetOperatorName(string originalValue, string? customSeed = null)
    {
        // Company/operator names are limited - cache them
        return GetOrCreateMapping($"Operator_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var baseName = faker.Company.CompanyName().Split(' ')[0];
            var suffix = faker.PickRandom(CompanySuffixes);
            return $"{baseName} {suffix}";
        }, shouldCache: true);
    }

    public string GetBusinessABN(string originalValue, string? customSeed = null)
    {
        // ABN numbers are unique identifiers - do not cache
        return GetOrCreateMapping($"ABN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var abn = faker.Random.Long(10000000000L, 99999999999L).ToString();
            return $"{abn[..2]} {abn[2..5]} {abn[5..8]} {abn[8..]}";
        }, shouldCache: false);
    }

    public string GetBusinessACN(string originalValue, string? customSeed = null)
    {
        // ACN numbers are unique identifiers - do not cache
        return GetOrCreateMapping($"ACN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var acn = faker.Random.Number(100000000, 999999999).ToString();
            return $"{acn[..3]} {acn[3..6]} {acn[6..]}";
        }, shouldCache: false);
    }

    public string GetAddress(string originalValue, string? customSeed = null)
    {
        // Full addresses are highly unique - do not cache
        return GetOrCreateMapping($"Address_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var streetNumber = faker.Random.Number(1, 999);
            var streetName = faker.Address.StreetName();
            var streetType = faker.PickRandom(new[] { "Street", "Road", "Avenue", "Drive", "Lane", "Circuit" });
            var suburb = faker.Address.City();
            var state = faker.PickRandom(AustralianStates);
            var postcode = faker.Random.Number(1000, 9999);
            
            return $"{streetNumber} {streetName} {streetType}, {suburb} {state} {postcode}";
        }, shouldCache: false);
    }

    public string GetFullAddress(string originalValue, string? customSeed = null)
    {
        // Full addresses are highly unique - do not cache
        return GetOrCreateMapping($"FullAddress_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var streetNumber = faker.Random.Number(1, 999);
            var streetName = faker.Address.StreetName();
            var streetType = faker.PickRandom(new[] { "Street", "Road", "Avenue", "Drive", "Lane", "Circuit" });
            var suburb = faker.Address.City();
            var state = faker.PickRandom(AustralianStates);
            var postcode = faker.Random.Number(1000, 9999);
            
            return $"{streetNumber} {streetName} {streetType}, {suburb} {state} {postcode}";
        }, shouldCache: false);
    }

    public string GetAddressLine1(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"AddressLine1_{originalValue}", customSeed, () =>
        {
            const int MAX_ADDRESS_LENGTH = 60; // Database constraint: nvarchar(60)
            
            // Generate street number with its own seed
            var numberSeed = $"{originalValue}_number";
            var numberFaker = CreateFaker(numberSeed, customSeed);
            var streetNumber = numberFaker.Random.Number(1, 9999);
            
            // Generate prefix with cumulative seed (original + number)
            var prefixSeed = $"{originalValue}_{streetNumber}_prefix";
            var prefixFaker = CreateFaker(prefixSeed, customSeed);
            var prefixes = DataFileLoader.AU.StreetPrefixes;
            var prefixIndex = Math.Abs(prefixSeed.GetHashCode()) % prefixes.Length;
            var hasPrefix = prefixFaker.Random.Bool(0.33f); // 33% chance of prefix
            var prefix = hasPrefix ? prefixes[prefixIndex] + " " : "";
            
            // Generate street name with cumulative seed (original + number + prefix)
            var nameSeed = $"{originalValue}_{streetNumber}_{prefix}_name";
            var nameFaker = CreateFaker(nameSeed, customSeed);
            // Use data file instead of Bogus street names for more control
            var cities = DataFileLoader.AU.Cities; // Use cities as street names for variety
            var nameIndex = Math.Abs(nameSeed.GetHashCode()) % cities.Length;
            var streetName = cities[nameIndex];
            
            // Generate street type with cumulative seed (all previous parts)
            var typeSeed = $"{originalValue}_{streetNumber}_{prefix}{streetName}_type";
            var typeFaker = CreateFaker(typeSeed, customSeed);
            var streetTypes = DataFileLoader.AU.StreetTypes;
            var typeIndex = Math.Abs(typeSeed.GetHashCode()) % streetTypes.Length;
            var streetType = streetTypes[typeIndex];
            
            var result = $"{streetNumber} {prefix}{streetName} {streetType}".Trim();
            
            // If result is too long, try without prefix first
            if (result.Length > MAX_ADDRESS_LENGTH && hasPrefix)
            {
                result = $"{streetNumber} {streetName} {streetType}".Trim();
            }
            
            // If still too long, truncate street name
            if (result.Length > MAX_ADDRESS_LENGTH)
            {
                var baseLength = $"{streetNumber}  {streetType}".Length; // Include spaces
                var availableForName = MAX_ADDRESS_LENGTH - baseLength;
                if (availableForName > 5) // Ensure minimum reasonable street name length
                {
                    streetName = streetName.Substring(0, Math.Min(streetName.Length, availableForName));
                    result = $"{streetNumber} {streetName} {streetType}".Trim();
                }
                else
                {
                    // Last resort: simple truncation
                    result = result.Substring(0, MAX_ADDRESS_LENGTH).Trim();
                }
            }
            
            return result;
        }, shouldCache: false); // Address lines are highly unique
    }

    public string GetAddressLine2(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"AddressLine2_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            // 30% chance of having a second address line
            if (faker.Random.Bool(0.3f))
            {
                var unitTypes = new[] { "Unit", "Apt", "Suite", "Level" };
                var unitType = faker.PickRandom(unitTypes);
                var unitNumber = faker.Random.Number(1, 999);
                return $"{unitType} {unitNumber}";
            }
            return string.Empty;
        }, shouldCache: false); // Address lines are highly unique
    }

    public string GetCity(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"City_{originalValue}", customSeed, () =>
        {
            const int MAX_CITY_LENGTH = 30; // Database constraint: nvarchar(30)
            
            // Generate base city with its own seed
            var citySeed = $"{originalValue}_city";
            var cityFaker = CreateFaker(citySeed, customSeed);
            var cities = DataFileLoader.AU.Cities;
            var cityIndex = Math.Abs(citySeed.GetHashCode()) % cities.Length;
            var baseCity = cities[cityIndex];
            
            // If base city is already at or near limit, return it without suffix
            if (baseCity.Length >= MAX_CITY_LENGTH - 5) // Leave room for potential suffix
            {
                return baseCity.Length > MAX_CITY_LENGTH ? baseCity.Substring(0, MAX_CITY_LENGTH) : baseCity;
            }
            
            // Generate suffix with cumulative seed (original + baseCity)
            var suffixSeed = $"{originalValue}_{baseCity}_suffix";
            var suffixFaker = CreateFaker(suffixSeed, customSeed);
            var suffixes = DataFileLoader.AU.CitySuffixes;
            var hasSuffix = suffixFaker.Random.Bool(0.4f); // 40% chance of suffix
            var suffix = "";
            
            if (hasSuffix)
            {
                var suffixIndex = Math.Abs(suffixSeed.GetHashCode()) % suffixes.Length;
                suffix = suffixes[suffixIndex];
                
                // Combine intelligently - avoid duplicates like "Hills Hills"
                if (!baseCity.EndsWith(suffix))
                {
                    var potentialResult = $"{baseCity} {suffix}";
                    
                    // Check length constraint before adding suffix
                    if (potentialResult.Length <= MAX_CITY_LENGTH)
                    {
                        suffix = " " + suffix;
                    }
                    else
                    {
                        suffix = ""; // Skip suffix if it would exceed length limit
                    }
                }
                else
                {
                    suffix = ""; // Skip if it would create duplication
                }
            }
            
            var result = $"{baseCity}{suffix}".Trim();
            
            // Final safety check - truncate if still too long
            return result.Length > MAX_CITY_LENGTH ? result.Substring(0, MAX_CITY_LENGTH).Trim() : result;
        }, shouldCache: true); // Cities are low cardinality - cache them
    }

    public string GetSuburb(string originalValue, string? customSeed = null)
    {
        // In Australia, suburb is equivalent to city
        return GetCity(originalValue, customSeed); // Inherits caching from GetCity
    }

    public string GetState(string originalValue, string? customSeed = null)
    {
        // States are very low cardinality (only 8 in Australia) - cache them
        return GetOrCreateMapping($"State_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.PickRandom(AustralianStates);
        }, shouldCache: true);
    }

    public string GetStateAbbr(string originalValue, string? customSeed = null)
    {
        // State abbreviations are very low cardinality - cache them
        return GetOrCreateMapping($"StateAbbr_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var stateAbbrs = new[] { "NSW", "VIC", "QLD", "WA", "SA", "TAS", "NT", "ACT" };
            return faker.PickRandom(stateAbbrs);
        }, shouldCache: true);
    }

    public string GetPostCode(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"PostCode_{originalValue}", customSeed, () =>
        {
            // Use hash to ensure more variation while maintaining Australian postcode patterns
            var hash = Math.Abs(originalValue.GetHashCode());
            
            // Australian postcodes are 4 digits and follow state patterns
            // Using hash to deterministically generate valid postcodes
            var postcodeRanges = new[]
            {
                (2000, 2999), // NSW
                (3000, 3999), // VIC  
                (4000, 4999), // QLD
                (5000, 5999), // SA
                (6000, 6999), // WA
                (7000, 7999), // TAS
                (2600, 2699), // ACT
                (800, 899)    // NT (will be padded to 4 digits)
            };
            
            var rangeIndex = hash % postcodeRanges.Length;
            var (min, max) = postcodeRanges[rangeIndex];
            var postcode = min + (hash / postcodeRanges.Length) % (max - min + 1);
            
            // NT postcodes need padding
            return postcode < 1000 ? postcode.ToString("D4") : postcode.ToString();
        }, shouldCache: true); // Postcodes are limited (few thousand) - cache them
    }

    public string GetCountry(string originalValue, string? customSeed = null)
    {
        // Countries are very low cardinality - cache them
        return GetOrCreateMapping($"Country_{originalValue}", customSeed, () =>
        {
            return "Australia"; // Always return Australia for Australian provider
        }, shouldCache: true);
    }

    public string GetGPSCoordinate(string originalValue, string? customSeed = null)
    {
        // GPS coordinates are highly unique - do not cache
        return GetOrCreateMapping($"GPS_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var latitude = faker.Random.Double(-44.0, -10.0);
            var longitude = faker.Random.Double(113.0, 154.0);
            return $"{latitude:F6},{longitude:F6}";
        }, shouldCache: false);
    }

    public string GetRouteCode(string originalValue, string? customSeed = null)
    {
        // Route codes have limited patterns - cache them
        return GetOrCreateMapping($"Route_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var routeTypes = new[] { "R", "SYD-MEL", "BNE-SYD", "MEL-ADL", "PER-ALB" };
            var type = faker.PickRandom(routeTypes);
            var number = faker.Random.Number(1, 999).ToString("D3");
            return $"{type}-{number}";
        }, shouldCache: true);
    }

    public string GetDepotLocation(string originalValue, string? customSeed = null)
    {
        // Depot locations are limited - cache them
        return GetOrCreateMapping($"Depot_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var depotTypes = new[] { "Distribution Centre", "Transport Hub", "Logistics Facility", "Service Depot" };
            var type = faker.PickRandom(depotTypes);
            var location = faker.Address.City();
            var state = faker.PickRandom(AustralianStates);
            return $"{type} - {location}, {state}";
        }, shouldCache: true);
    }

    public string GetCreditCard(string originalValue, string? customSeed = null)
    {
        // Credit card numbers are unique - do not cache
        return GetOrCreateMapping($"CreditCard_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Finance.CreditCardNumber();
        }, shouldCache: false);
    }

    private string GetOrCreateMapping(string key, string? customSeed, Func<string> generator, bool shouldCache = true)
    {
        var finalKey = customSeed != null ? $"{key}_{customSeed}" : key;
        
        // If caching is disabled for this type, generate directly
        if (!shouldCache)
        {
            try
            {
                return generator();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate value for key: {Key}", finalKey);
                return "GENERATION_FAILED";
            }
        }
        
        // Use cache for low-cardinality fields
        return _mappingCache.GetOrAdd(finalKey, _ =>
        {
            try
            {
                return generator();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate value for key: {Key}", finalKey);
                return "GENERATION_FAILED";
            }
        });
    }

    private Faker CreateFaker(string originalValue, string? customSeed)
    {
        var seedValue = ComputeSeed(originalValue, customSeed);
        return new Faker("en_AU") { Random = new Randomizer(seedValue) };
    }

    private int ComputeSeed(string originalValue, string? customSeed)
    {
        var combinedInput = $"{_globalSeed}_{customSeed ?? "default"}_{originalValue}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedInput));
        return BitConverter.ToInt32(hashBytes, 0);
    }

    public Dictionary<string, string> GetAllMappings()
    {
        return new Dictionary<string, string>(_mappingCache);
    }

    public void ClearCache()
    {
        _mappingCache.Clear();
        _logger.LogInformation("Mapping cache cleared");
    }

    public async Task SaveMappingsAsync(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var filePath = Path.Combine(directory, $"mappings_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            var mappings = GetAllMappings();
            
            // Log statistics about what types of data are cached
            var stats = mappings.Keys
                .GroupBy(k => k.Split('_')[0])
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(s => s.Count);
            
            _logger.LogInformation("Cache statistics - Total entries: {Total}", mappings.Count);
            foreach (var stat in stats)
            {
                _logger.LogInformation("  {Type}: {Count} entries", stat.Type, stat.Count);
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(mappings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved {Count} cached mappings to {FilePath}", mappings.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mappings to directory: {Directory}", directory);
            throw;
        }
    }

    public async Task LoadMappingsAsync(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogInformation("Mappings directory does not exist: {Directory}", directory);
                return;
            }

            var latestFile = Directory.GetFiles(directory, "mappings_*.json")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (latestFile == null)
            {
                _logger.LogInformation("No mapping files found in directory: {Directory}", directory);
                return;
            }

            var json = await File.ReadAllTextAsync(latestFile);
            var mappings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (mappings != null)
            {
                // Only load mappings for data types that should be cached
                var loadedCount = 0;
                var skippedCount = 0;
                
                foreach (var mapping in mappings)
                {
                    var keyParts = mapping.Key.Split('_');
                    if (keyParts.Length > 0)
                    {
                        var dataTypePrefix = keyParts[0];
                        
                        // Check if this type should be cached based on prefix
                        var shouldCache = dataTypePrefix switch
                        {
                            "FirstName" or "LastName" or "DriverName" => true,
                            "City" or "State" or "StateAbbr" or "Country" or "PostCode" => true,
                            "Operator" or "VehicleMakeModel" => true,
                            "Route" or "Depot" => true,
                            // Skip high-cardinality types
                            "AddressLine1" or "AddressLine2" or "Address" or "FullAddress" => false,
                            "ContactEmail" or "DriverPhone" => false,
                            "DriverLicense" or "VehicleReg" or "VIN" or "Engine" => false,
                            "ABN" or "ACN" or "CreditCard" or "GPS" => false,
                            _ => false
                        };
                        
                        if (shouldCache)
                        {
                            _mappingCache.TryAdd(mapping.Key, mapping.Value);
                            loadedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                }
                
                _logger.LogInformation("Loaded {LoadedCount} cached mappings from {FilePath} (skipped {SkippedCount} high-cardinality entries)", 
                    loadedCount, latestFile, skippedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mappings from directory: {Directory}", directory);
            throw;
        }
    }
}