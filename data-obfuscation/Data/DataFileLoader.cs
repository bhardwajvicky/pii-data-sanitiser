using System.Reflection;

namespace DataObfuscation.Data;

public static class DataFileLoader
{
    private static readonly Dictionary<string, string[]> _cache = new();
    private static readonly string _dataPath;

    static DataFileLoader()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(assemblyLocation) ?? "";
        _dataPath = Path.Combine(directory, "Data");
    }

    public static string[] LoadDataFile(string country, string fileName)
    {
        var key = $"{country}/{fileName}";
        
        if (_cache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var filePath = Path.Combine(_dataPath, country, $"{fileName}.txt");
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Data file not found: {filePath}");
        }

        var data = File.ReadAllLines(filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();

        _cache[key] = data;
        return data;
    }

    public static class AU
    {
        public static string[] Cities => LoadDataFile("AU", "Cities");
        public static string[] States => LoadDataFile("AU", "States");
        public static string[] StreetTypes => LoadDataFile("AU", "StreetTypes");
        public static string[] StreetPrefixes => LoadDataFile("AU", "StreetPrefixes");
        public static string[] CitySuffixes => LoadDataFile("AU", "CitySuffixes");
        public static string[] CompanySuffixes => LoadDataFile("AU", "CompanySuffixes");
        public static string[] VehicleMakes => LoadDataFile("AU", "VehicleMakes");
        public static string[] VehicleModels => LoadDataFile("AU", "VehicleModels");
    }

    public static class UK
    {
        public static string[] Countries => LoadDataFile("UK", "Countries");
        public static string[] CompanySuffixes => LoadDataFile("UK", "CompanySuffixes");
        public static string[] EmailDomains => LoadDataFile("UK", "EmailDomains");
    }
}