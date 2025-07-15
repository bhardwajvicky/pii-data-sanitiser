namespace Common.DataTypes;

/// <summary>
/// Centralized definition of all supported data types for both schema-analyzer and data-obfuscation projects.
/// This ensures consistency between PII detection and obfuscation implementation.
/// </summary>
public static class SupportedDataTypes
{
    #region Core Personal Data Types
    
    /// <summary>First names, given names only (NOT full names)</summary>
    public const string FirstName = "FirstName";
    
    /// <summary>Last names, surnames only (NOT full names)</summary>
    public const string LastName = "LastName";
    
    /// <summary>Full names, display names, complete personal names</summary>
    public const string FullName = "FullName";
    
    #endregion

    #region Contact Information
    
    /// <summary>Email addresses</summary>
    public const string Email = "Email";
    
    /// <summary>Phone numbers, contact numbers</summary>
    public const string Phone = "Phone";
    
    #endregion

    #region Address Components
    
    /// <summary>Full address (all components combined)</summary>
    public const string FullAddress = "FullAddress";
    
    /// <summary>Primary address line (street number + name)</summary>
    public const string AddressLine1 = "AddressLine1";
    
    /// <summary>Secondary address line (apartment, unit, suite - optional)</summary>
    public const string AddressLine2 = "AddressLine2";
    
    /// <summary>City/Suburb/Town</summary>
    public const string City = "City";
    
    /// <summary>Alternative name for City (commonly used in Australia)</summary>
    public const string Suburb = "Suburb";
    
    /// <summary>State/Province/County</summary>
    public const string State = "State";
    
    /// <summary>State abbreviation (NSW, VIC, etc.)</summary>
    public const string StateAbbr = "StateAbbr";
    
    /// <summary>Postal code/ZIP code</summary>
    public const string PostCode = "PostCode";
    
    /// <summary>Alternative name for PostCode</summary>
    public const string ZipCode = "ZipCode";
    
    /// <summary>Country name</summary>
    public const string Country = "Country";
    
    /// <summary>Legacy address type - use specific components instead</summary>
    [Obsolete("Use specific address components (AddressLine1, City, State, PostCode) instead")]
    public const string Address = "Address";
    
    #endregion

    #region Financial Information
    
    /// <summary>Credit card numbers</summary>
    public const string CreditCard = "CreditCard";
    
    /// <summary>UK National Insurance Numbers</summary>
    public const string NINO = "NINO";
    
    /// <summary>Alternative name for NINO</summary>
    public const string NationalInsuranceNumber = "NationalInsuranceNumber";
    
    /// <summary>UK bank sort codes</summary>
    public const string SortCode = "SortCode";
    
    /// <summary>Alternative name for SortCode</summary>
    public const string BankSortCode = "BankSortCode";
    
    #endregion

    #region Identification & Licenses
    
    /// <summary>License numbers, permit IDs</summary>
    public const string LicenseNumber = "LicenseNumber";
    
    #endregion

    #region Business Information
    
    /// <summary>Company/operator names</summary>
    public const string CompanyName = "CompanyName";
    
    /// <summary>Australian Business Numbers</summary>
    public const string BusinessABN = "BusinessABN";
    
    /// <summary>Australian Company Numbers</summary>
    public const string BusinessACN = "BusinessACN";
    
    #endregion

    #region Vehicle Information
    
    /// <summary>Vehicle registration plates</summary>
    public const string VehicleRegistration = "VehicleRegistration";
    
    /// <summary>Vehicle identification numbers</summary>
    public const string VINNumber = "VINNumber";
    
    /// <summary>Vehicle make and model information</summary>
    public const string VehicleMakeModel = "VehicleMakeModel";
    
    /// <summary>Engine identification numbers</summary>
    public const string EngineNumber = "EngineNumber";
    
    #endregion

    #region Location & Geographic
    
    /// <summary>GPS coordinates, location data</summary>
    public const string GPSCoordinate = "GPSCoordinate";
    
    /// <summary>Route identifiers</summary>
    public const string RouteCode = "RouteCode";
    
    /// <summary>Depot/facility locations</summary>
    public const string DepotLocation = "DepotLocation";
    
    #endregion

    #region UK-Specific Types
    
    /// <summary>UK postal codes (SW1A 1AA format)</summary>
    public const string UKPostcode = "UKPostcode";
    
    #endregion

    /// <summary>
    /// All supported data types as a HashSet for validation
    /// </summary>
    public static readonly HashSet<string> AllSupportedTypes = new()
    {
        // Core Personal
        FirstName, LastName, FullName,
        
        // Contact
        Email, Phone,
        
        // Address Components
        FullAddress, AddressLine1, AddressLine2, City, Suburb, State, StateAbbr, 
        PostCode, ZipCode, Country, Address,
        
        // Financial
        CreditCard, NINO, NationalInsuranceNumber, SortCode, BankSortCode,
        
        // Identification
        LicenseNumber,
        
        // Business
        CompanyName, BusinessABN, BusinessACN,
        
        // Vehicle
        VehicleRegistration, VINNumber, VehicleMakeModel, EngineNumber,
        
        // Location
        GPSCoordinate, RouteCode, DepotLocation,
        
        // UK-Specific
        UKPostcode
    };

    /// <summary>
    /// Data types that are considered address components
    /// </summary>
    public static readonly HashSet<string> AddressComponentTypes = new()
    {
        FullAddress, AddressLine1, AddressLine2, City, Suburb, State, StateAbbr,
        PostCode, ZipCode, Country, Address, UKPostcode
    };

    /// <summary>
    /// Data types specific to Australian context
    /// </summary>
    public static readonly HashSet<string> AustralianSpecificTypes = new()
    {
        BusinessABN, BusinessACN, Suburb
    };

    /// <summary>
    /// Data types specific to UK context
    /// </summary>
    public static readonly HashSet<string> UKSpecificTypes = new()
    {
        NINO, NationalInsuranceNumber, SortCode, BankSortCode, UKPostcode
    };

    /// <summary>
    /// Data types related to financial information
    /// </summary>
    public static readonly HashSet<string> FinancialTypes = new()
    {
        CreditCard, NINO, NationalInsuranceNumber, SortCode, BankSortCode
    };

    /// <summary>
    /// Check if a data type is supported
    /// </summary>
    /// <param name="dataType">The data type to check</param>
    /// <returns>True if supported, false otherwise</returns>
    public static bool IsSupported(string dataType)
    {
        return AllSupportedTypes.Contains(dataType);
    }

    /// <summary>
    /// Get all supported data types as a formatted string for error messages
    /// </summary>
    /// <returns>Comma-separated list of supported types</returns>
    public static string GetAllSupportedTypesString()
    {
        return string.Join(", ", AllSupportedTypes.OrderBy(t => t));
    }

    /// <summary>
    /// Get address component types as a formatted string
    /// </summary>
    /// <returns>Comma-separated list of address component types</returns>
    public static string GetAddressComponentTypesString()
    {
        return string.Join(", ", AddressComponentTypes.OrderBy(t => t));
    }
}