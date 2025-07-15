using Common.DataTypes;

namespace DataObfuscation.Configuration;

public static class CacheConfiguration
{
    /// <summary>
    /// Data types that should always be cached due to low cardinality and high repetition
    /// </summary>
    public static readonly HashSet<string> AlwaysCacheDataTypes = new()
    {
        // Names - High repetition, low cardinality
        SupportedDataTypes.FirstName,
        SupportedDataTypes.LastName,
        SupportedDataTypes.FullName,
        
        // Geographic - Limited set of values
        SupportedDataTypes.City,
        SupportedDataTypes.Suburb,
        SupportedDataTypes.State,
        SupportedDataTypes.StateAbbr,
        SupportedDataTypes.Country,
        
        // Business Categories - Limited variations
        SupportedDataTypes.CompanyName,
        "Company", // Alternative name
        "OperatorName",
        "Department",
        "JobTitle",
        
        // Limited Sets
        "Gender",
        "Title",
        
        // Structured but limited variations
        SupportedDataTypes.PostCode,  // Limited postal codes per country
        SupportedDataTypes.ZipCode,   // Alternative name
        SupportedDataTypes.RouteCode,  // Limited route patterns
        SupportedDataTypes.DepotLocation, // Limited depot locations
        
        // Vehicle make/model (limited combinations)
        SupportedDataTypes.VehicleMakeModel
    };

    /// <summary>
    /// Data types that should never be cached due to high cardinality
    /// </summary>
    public static readonly HashSet<string> NeverCacheDataTypes = new()
    {
        // Addresses - Highly unique
        SupportedDataTypes.Address,
        SupportedDataTypes.FullAddress,
        SupportedDataTypes.AddressLine1,
        SupportedDataTypes.AddressLine2,
        SupportedDataTypes.GPSCoordinate,
        
        // Financial - Always unique
        SupportedDataTypes.CreditCard,
        "BankAccount",
        "IBAN",
        "BIC",
        SupportedDataTypes.SortCode,
        SupportedDataTypes.BankSortCode,
        
        // Identifiers - Always unique
        "SSN",
        SupportedDataTypes.NINO,
        SupportedDataTypes.NationalInsuranceNumber,
        "NationalID",
        "DriverLicense",
        SupportedDataTypes.LicenseNumber,
        "PassportNumber",
        "EmployeeID",
        "CustomerID",
        SupportedDataTypes.VINNumber,
        SupportedDataTypes.VehicleRegistration,
        SupportedDataTypes.EngineNumber,
        SupportedDataTypes.BusinessABN,
        SupportedDataTypes.BusinessACN,
        "ABN", // Alternative name
        "ACN", // Alternative name
        
        // Contact Info - Usually unique
        SupportedDataTypes.Email,
        SupportedDataTypes.Phone,
        "Mobile",
        "Fax",
        
        // Timestamps - Always unique
        "DateTime",
        "Date",
        "Time",
        
        // Free Text - Highly unique
        "Description",
        "Comment",
        "Note",
        "FreeText",
        
        // Other unique fields
        "URL",
        "IP",
        "MACAddress",
        "Username",
        "Password",
        "Numeric",
        "Decimal",
        "Money",
        "Percentage"
    };

    /// <summary>
    /// Determines if a data type should be cached based on its cardinality characteristics
    /// </summary>
    public static bool ShouldCache(string dataType)
    {
        // Explicit rules first
        if (AlwaysCacheDataTypes.Contains(dataType))
            return true;
            
        if (NeverCacheDataTypes.Contains(dataType))
            return false;
            
        // Default to not caching for any unknown types (conservative approach)
        return false;
    }
}