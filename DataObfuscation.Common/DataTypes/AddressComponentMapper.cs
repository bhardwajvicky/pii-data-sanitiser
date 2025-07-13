namespace DataObfuscation.Common.DataTypes;

/// <summary>
/// Maps database column names to appropriate address component data types
/// </summary>
public static class AddressComponentMapper
{
    /// <summary>
    /// Common patterns for AddressLine1 columns
    /// </summary>
    public static readonly HashSet<string> AddressLine1Patterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Address", "Address1", "AddressLine1", "StreetAddress", "Street", "StreetLine1",
        "Addr1", "PrimaryAddress", "MainAddress", "HouseAddress", "StreetName"
    };

    /// <summary>
    /// Common patterns for AddressLine2 columns
    /// </summary>
    public static readonly HashSet<string> AddressLine2Patterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Address2", "AddressLine2", "StreetLine2", "Addr2", "SecondaryAddress", 
        "UnitNumber", "ApartmentNumber", "Suite", "Unit", "Apt", "Floor", "Level"
    };

    /// <summary>
    /// Common patterns for City/Suburb columns
    /// </summary>
    public static readonly HashSet<string> CitySuburbPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "City", "Suburb", "Town", "Municipality", "Locality", "CityName", "SuburbName"
    };

    /// <summary>
    /// Common patterns for State/Province columns
    /// </summary>
    public static readonly HashSet<string> StatePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "State", "Province", "Region", "County", "StateName", "ProvinceName"
    };

    /// <summary>
    /// Common patterns for State abbreviation columns
    /// </summary>
    public static readonly HashSet<string> StateAbbrPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "StateAbbr", "StateCode", "StateAbbreviation", "ProvinceCode", "RegionCode"
    };

    /// <summary>
    /// Common patterns for PostCode/ZipCode columns
    /// </summary>
    public static readonly HashSet<string> PostCodePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PostCode", "PostalCode", "ZipCode", "Zip", "PostCodeValue", "PC"
    };

    /// <summary>
    /// Common patterns for Country columns
    /// </summary>
    public static readonly HashSet<string> CountryPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Country", "CountryName", "CountryCode", "Nation"
    };

    /// <summary>
    /// Common patterns for full address columns
    /// </summary>
    public static readonly HashSet<string> FullAddressPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "FullAddress", "CompleteAddress", "FormattedAddress", "AddressFull", "FullAddr"
    };

    /// <summary>
    /// Map a column name to the most appropriate address component data type
    /// </summary>
    /// <param name="columnName">The database column name</param>
    /// <returns>The appropriate data type, or null if not an address component</returns>
    public static string? MapColumnToAddressType(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return null;

        // Check exact matches first
        if (FullAddressPatterns.Contains(columnName))
            return SupportedDataTypes.FullAddress;

        if (AddressLine1Patterns.Contains(columnName))
            return SupportedDataTypes.AddressLine1;

        if (AddressLine2Patterns.Contains(columnName))
            return SupportedDataTypes.AddressLine2;

        if (CitySuburbPatterns.Contains(columnName))
            return SupportedDataTypes.City;

        if (StateAbbrPatterns.Contains(columnName))
            return SupportedDataTypes.StateAbbr;

        if (StatePatterns.Contains(columnName))
            return SupportedDataTypes.State;

        if (PostCodePatterns.Contains(columnName))
            return SupportedDataTypes.PostCode;

        if (CountryPatterns.Contains(columnName))
            return SupportedDataTypes.Country;

        // Check for partial matches (contains)
        var lowerColumnName = columnName.ToLowerInvariant();

        if (lowerColumnName.Contains("address") && lowerColumnName.Contains("2"))
            return SupportedDataTypes.AddressLine2;

        if (lowerColumnName.Contains("address") && (lowerColumnName.Contains("1") || lowerColumnName.Contains("line1")))
            return SupportedDataTypes.AddressLine1;

        if (lowerColumnName.Contains("address") && (lowerColumnName.Contains("full") || lowerColumnName.Contains("complete")))
            return SupportedDataTypes.FullAddress;

        if (lowerColumnName.Contains("suburb"))
            return SupportedDataTypes.City; // In Australia, suburb is equivalent to city

        if (lowerColumnName.Contains("postcode") || lowerColumnName.Contains("zipcode"))
            return SupportedDataTypes.PostCode;

        if (lowerColumnName.Contains("country"))
            return SupportedDataTypes.Country;

        // Generic address - default to AddressLine1 if it just says "address"
        if (lowerColumnName == "address" || lowerColumnName == "addr")
            return SupportedDataTypes.AddressLine1;

        return null;
    }

    /// <summary>
    /// Check if a column name appears to be an address component
    /// </summary>
    /// <param name="columnName">The database column name</param>
    /// <returns>True if it appears to be an address component</returns>
    public static bool IsAddressComponent(string columnName)
    {
        return MapColumnToAddressType(columnName) != null;
    }

    /// <summary>
    /// Get all address component patterns for debugging/validation
    /// </summary>
    /// <returns>Dictionary of pattern type to patterns</returns>
    public static Dictionary<string, HashSet<string>> GetAllPatterns()
    {
        return new Dictionary<string, HashSet<string>>
        {
            { nameof(FullAddressPatterns), FullAddressPatterns },
            { nameof(AddressLine1Patterns), AddressLine1Patterns },
            { nameof(AddressLine2Patterns), AddressLine2Patterns },
            { nameof(CitySuburbPatterns), CitySuburbPatterns },
            { nameof(StatePatterns), StatePatterns },
            { nameof(StateAbbrPatterns), StateAbbrPatterns },
            { nameof(PostCodePatterns), PostCodePatterns },
            { nameof(CountryPatterns), CountryPatterns }
        };
    }
}