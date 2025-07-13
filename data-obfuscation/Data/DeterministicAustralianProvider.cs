using Bogus;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    
    private static readonly string[] AustralianStates = { "NSW", "VIC", "QLD", "WA", "SA", "TAS", "NT", "ACT" };
    private static readonly string[] CompanySuffixes = { "Transport", "Logistics", "Haulage", "Fleet Services", "Distribution", "Freight" };
    private static readonly string[] VehicleMakes = { "Toyota", "Ford", "Isuzu", "Mercedes-Benz", "Volvo", "Scania", "Kenworth", "Mack" };
    private static readonly string[] VehicleModels = { "Hiace", "Transit", "Sprinter", "Crafter", "Daily", "Canter", "Dyna", "Ranger" };

    public DeterministicAustralianProvider(ILogger<DeterministicAustralianProvider> logger, string globalSeed = "DefaultSeed2024")
    {
        _logger = logger;
        _globalSeed = globalSeed;
        _mappingCache = new ConcurrentDictionary<string, string>();
    }

    public string GetDriverName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"DriverName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.FullName();
        });
    }

    public string GetFirstName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"FirstName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.FirstName();
        });
    }

    public string GetLastName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"LastName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.LastName();
        });
    }

    public string GetDriverLicenseNumber(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"DriverLicense_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var state = faker.PickRandom(AustralianStates);
            var number = faker.Random.Number(10000000, 99999999);
            return $"{state}-{number}";
        });
    }

    public string GetContactEmail(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"ContactEmail_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var domains = new[] { "transport.com.au", "logistics.net.au", "freight.com.au", "haulage.org.au" };
            var firstName = faker.Name.FirstName().ToLower();
            var lastName = faker.Name.LastName().ToLower();
            var domain = faker.PickRandom(domains);
            return $"{firstName}.{lastName}@{domain}";
        });
    }

    public string GetDriverPhone(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"DriverPhone_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var number = faker.Random.Number(400000000, 499999999);
            var formatted = $"04{number.ToString()[2..]}";
            return $"{formatted[..4]} {formatted[4..7]} {formatted[7..]}";
        });
    }

    public string GetVehicleRegistration(string originalValue, string? customSeed = null)
    {
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
        });
    }

    public string GetVINNumber(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"VIN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var chars = "ABCDEFGHJKLMNPRSTUVWXYZ1234567890";
            return faker.Random.String2(17, chars);
        });
    }

    public string GetVehicleMakeModel(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"VehicleMakeModel_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var make = faker.PickRandom(VehicleMakes);
            var model = faker.PickRandom(VehicleModels);
            return $"{make} {model}";
        });
    }

    public string GetEngineNumber(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Engine_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var prefix = faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            var number = faker.Random.Number(1000000, 9999999);
            return $"{prefix}{number}";
        });
    }

    public string GetOperatorName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Operator_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var baseName = faker.Company.CompanyName().Split(' ')[0];
            var suffix = faker.PickRandom(CompanySuffixes);
            return $"{baseName} {suffix}";
        });
    }

    public string GetBusinessABN(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"ABN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var abn = faker.Random.Long(10000000000L, 99999999999L).ToString();
            return $"{abn[..2]} {abn[2..5]} {abn[5..8]} {abn[8..]}";
        });
    }

    public string GetBusinessACN(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"ACN_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var acn = faker.Random.Number(100000000, 999999999).ToString();
            return $"{acn[..3]} {acn[3..6]} {acn[6..]}";
        });
    }

    public string GetAddress(string originalValue, string? customSeed = null)
    {
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
        });
    }

    public string GetGPSCoordinate(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"GPS_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var latitude = faker.Random.Double(-44.0, -10.0);
            var longitude = faker.Random.Double(113.0, 154.0);
            return $"{latitude:F6},{longitude:F6}";
        });
    }

    public string GetRouteCode(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Route_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var routeTypes = new[] { "R", "SYD-MEL", "BNE-SYD", "MEL-ADL", "PER-ALB" };
            var type = faker.PickRandom(routeTypes);
            var number = faker.Random.Number(1, 999).ToString("D3");
            return $"{type}-{number}";
        });
    }

    public string GetDepotLocation(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Depot_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var depotTypes = new[] { "Distribution Centre", "Transport Hub", "Logistics Facility", "Service Depot" };
            var type = faker.PickRandom(depotTypes);
            var location = faker.Address.City();
            var state = faker.PickRandom(AustralianStates);
            return $"{type} - {location}, {state}";
        });
    }

    public string GetCreditCard(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"CreditCard_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Finance.CreditCardNumber();
        });
    }

    private string GetOrCreateMapping(string key, string? customSeed, Func<string> generator)
    {
        var finalKey = customSeed != null ? $"{key}_{customSeed}" : key;
        
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
            
            var json = System.Text.Json.JsonSerializer.Serialize(mappings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved {Count} mappings to {FilePath}", mappings.Count, filePath);
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
                foreach (var mapping in mappings)
                {
                    _mappingCache.TryAdd(mapping.Key, mapping.Value);
                }
                
                _logger.LogInformation("Loaded {Count} mappings from {FilePath}", mappings.Count, latestFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mappings from directory: {Directory}", directory);
            throw;
        }
    }
}