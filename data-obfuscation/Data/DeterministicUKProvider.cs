using Bogus;
using Bogus.Extensions.UnitedKingdom;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DataObfuscation.Data;

public interface IDeterministicUKProvider
{
    string GetFirstName(string originalValue, string? customSeed = null);
    string GetLastName(string originalValue, string? customSeed = null);
    string GetFullName(string originalValue, string? customSeed = null);
    string GetEmail(string originalValue, string? customSeed = null);
    string GetPhone(string originalValue, string? customSeed = null);
    string GetAddress(string originalValue, string? customSeed = null);
    string GetCreditCard(string originalValue, string? customSeed = null);
    string GetNationalInsuranceNumber(string originalValue, string? customSeed = null);
    string GetBankSortCode(string originalValue, string? customSeed = null);
    string GetUKPostcode(string originalValue, string? customSeed = null);
    string GetCompanyName(string originalValue, string? customSeed = null);
    string GetVehicleRegistration(string originalValue, string? customSeed = null);
    
    Dictionary<string, string> GetAllMappings();
    void ClearCache();
    Task SaveMappingsAsync(string directory);
    Task LoadMappingsAsync(string directory);
}

public class DeterministicUKProvider : IDeterministicUKProvider
{
    private readonly ILogger<DeterministicUKProvider> _logger;
    private readonly string _globalSeed;
    private readonly ConcurrentDictionary<string, string> _mappingCache;
    
    private static readonly string[] UKCountries = { "England", "Scotland", "Wales", "Northern Ireland" };
    private static readonly string[] CompanySuffixes = { "Ltd", "Limited", "PLC", "& Co", "Group", "Holdings", "Services", "Solutions" };
    private static readonly string[] EmailDomains = { "btinternet.com", "sky.com", "gmail.com", "outlook.com", "yahoo.co.uk", "hotmail.co.uk" };

    public DeterministicUKProvider(ILogger<DeterministicUKProvider> logger, string globalSeed = "DefaultUKSeed2024")
    {
        _logger = logger;
        _globalSeed = globalSeed;
        _mappingCache = new ConcurrentDictionary<string, string>();
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

    public string GetFullName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"FullName_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Name.FullName();
        });
    }

    public string GetEmail(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Email_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var firstName = faker.Name.FirstName().ToLower();
            var lastName = faker.Name.LastName().ToLower();
            var domain = faker.PickRandom(EmailDomains);
            return $"{firstName}.{lastName}@{domain}";
        });
    }

    public string GetPhone(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Phone_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            // UK mobile format: +44 7xxx xxx xxx or 07xxx xxx xxx
            var isMobile = faker.Random.Bool(0.7f); // 70% mobile numbers
            
            if (isMobile)
            {
                return $"07{faker.Random.Number(100, 999)} {faker.Random.Number(100, 999)} {faker.Random.Number(100, 999)}";
            }
            else
            {
                // UK landline format: 01xxx xxx xxx or 02x xxxx xxxx
                var areaCode = faker.PickRandom(new[] { "0121", "0161", "0113", "0141", "0151", "0117", "0191" });
                return $"{areaCode} {faker.Random.Number(100, 999)} {faker.Random.Number(1000, 9999)}";
            }
        });
    }

    public string GetAddress(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Address_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var houseNumber = faker.Random.Number(1, 999);
            var street = faker.Address.StreetName();
            var city = faker.Address.City();
            var postcode = GetUKPostcode(originalValue, customSeed);
            return $"{houseNumber} {street}, {city} {postcode}";
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

    public string GetNationalInsuranceNumber(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"NINO_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Finance.Nino(); // UK-specific extension
        });
    }

    public string GetBankSortCode(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"SortCode_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            return faker.Finance.SortCode(); // UK-specific extension
        });
    }

    public string GetUKPostcode(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Postcode_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            // UK postcode format: XX99 9XX
            var letters1 = faker.Random.String2(2, "ABCDEFGHIJKLMNOPRSTUVWXYZ");
            var numbers1 = faker.Random.Number(10, 99);
            var number2 = faker.Random.Number(0, 9);
            var letters2 = faker.Random.String2(2, "ABDEFGHJLNPQRSTUWXYZ");
            return $"{letters1}{numbers1} {number2}{letters2}";
        });
    }

    public string GetCompanyName(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"Company_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            var baseName = faker.Company.CompanyName();
            var suffix = faker.PickRandom(CompanySuffixes);
            return $"{baseName} {suffix}";
        });
    }

    public string GetVehicleRegistration(string originalValue, string? customSeed = null)
    {
        return GetOrCreateMapping($"VehicleReg_{originalValue}", customSeed, () =>
        {
            var faker = CreateFaker(originalValue, customSeed);
            // UK registration plate with date range (2001 to current year)
            var fromDate = new DateTime(2001, 1, 1);
            var toDate = DateTime.Now;
            return faker.Vehicle.GbRegistrationPlate(fromDate, toDate);
        });
    }

    private Faker CreateFaker(string originalValue, string? customSeed)
    {
        var combinedSeed = customSeed ?? _globalSeed;
        var seedString = $"{combinedSeed}_{originalValue}";
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seedString));
        var seedValue = BitConverter.ToInt32(seedBytes, 0);
        
        return new Faker("en_GB") { Random = new Randomizer(seedValue) };
    }

    private string GetOrCreateMapping(string key, string? customSeed, Func<string> generator)
    {
        var finalKey = customSeed != null ? $"{key}_{customSeed}" : key;
        
        return _mappingCache.GetOrAdd(finalKey, _ =>
        {
            var generated = generator();
            _logger.LogDebug("Generated mapping: {Key} -> {Value}", finalKey, generated);
            return generated;
        });
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

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filePath = Path.Combine(directory, $"uk_mappings_{timestamp}.json");
            
            var mappings = GetAllMappings();
            var json = System.Text.Json.JsonSerializer.Serialize(mappings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved {Count} UK mappings to {FilePath}", mappings.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save UK mappings to directory: {Directory}", directory);
            throw;
        }
    }

    public async Task LoadMappingsAsync(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogInformation("UK mappings directory does not exist: {Directory}", directory);
                return;
            }

            var files = Directory.GetFiles(directory, "uk_mappings_*.json")
                                 .OrderByDescending(f => new FileInfo(f).CreationTime)
                                 .ToArray();

            if (files.Length == 0)
            {
                _logger.LogInformation("No UK mapping files found in directory: {Directory}", directory);
                return;
            }

            var latestFile = files[0];
            var json = await File.ReadAllTextAsync(latestFile);
            var mappings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (mappings != null)
            {
                foreach (var (key, value) in mappings)
                {
                    _mappingCache.TryAdd(key, value);
                }
                _logger.LogInformation("Loaded {Count} UK mappings from {FilePath}", mappings.Count, latestFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load UK mappings from directory: {Directory}", directory);
            throw;
        }
    }
}