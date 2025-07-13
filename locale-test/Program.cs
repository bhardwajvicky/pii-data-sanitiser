using Bogus;

Console.WriteLine("=== ADDRESS COMPONENT ANALYSIS ===\n");

// Test Australian address components
var auFaker = new Faker("en_AU");
Console.WriteLine("🇦🇺 AUSTRALIAN ADDRESS COMPONENTS:");
Console.WriteLine($"FullAddress: {auFaker.Address.FullAddress()}");
Console.WriteLine($"StreetAddress: {auFaker.Address.StreetAddress()}");
Console.WriteLine($"StreetAddress(useFullAddress: true): {auFaker.Address.StreetAddress(useFullAddress: true)}");
Console.WriteLine($"BuildingNumber: {auFaker.Address.BuildingNumber()}");
Console.WriteLine($"StreetName: {auFaker.Address.StreetName()}");
Console.WriteLine($"StreetSuffix: {auFaker.Address.StreetSuffix()}");
Console.WriteLine($"SecondaryAddress: {auFaker.Address.SecondaryAddress()}");
Console.WriteLine($"City: {auFaker.Address.City()}");
Console.WriteLine($"State: {auFaker.Address.State()}");
Console.WriteLine($"StateAbbr: {auFaker.Address.StateAbbr()}");
Console.WriteLine($"ZipCode: {auFaker.Address.ZipCode()}");
Console.WriteLine($"Country: {auFaker.Address.Country()}");

// Test UK address components
var ukFaker = new Faker("en_GB");
Console.WriteLine("\n🇬🇧 UK ADDRESS COMPONENTS:");
Console.WriteLine($"FullAddress: {ukFaker.Address.FullAddress()}");
Console.WriteLine($"StreetAddress: {ukFaker.Address.StreetAddress()}");
Console.WriteLine($"StreetAddress(useFullAddress: true): {ukFaker.Address.StreetAddress(useFullAddress: true)}");
Console.WriteLine($"BuildingNumber: {ukFaker.Address.BuildingNumber()}");
Console.WriteLine($"StreetName: {ukFaker.Address.StreetName()}");
Console.WriteLine($"StreetSuffix: {ukFaker.Address.StreetSuffix()}");
Console.WriteLine($"SecondaryAddress: {ukFaker.Address.SecondaryAddress()}");
Console.WriteLine($"City: {ukFaker.Address.City()}");
Console.WriteLine($"State: {ukFaker.Address.State()}");
Console.WriteLine($"StateAbbr: {ukFaker.Address.StateAbbr()}");
Console.WriteLine($"ZipCode: {ukFaker.Address.ZipCode()}");
Console.WriteLine($"Country: {ukFaker.Address.Country()}");

// Test other locales for comparison
Console.WriteLine("\n🌍 OTHER LOCALES:");
var usFaker = new Faker("en_US");
Console.WriteLine($"US State: {usFaker.Address.State()}");
Console.WriteLine($"US StateAbbr: {usFaker.Address.StateAbbr()}");
Console.WriteLine($"US ZipCode: {usFaker.Address.ZipCode()}");

Console.WriteLine("\n📋 COMPONENT MAPPING NEEDS:");
Console.WriteLine("Required address components for databases:");
Console.WriteLine("- AddressLine1 (street number + name)");
Console.WriteLine("- AddressLine2 (apartment, unit, suite - optional)");  
Console.WriteLine("- City/Suburb");
Console.WriteLine("- State/Province");
Console.WriteLine("- PostCode/ZipCode");
Console.WriteLine("- Country");
Console.WriteLine("- FullAddress (all combined)");

Console.WriteLine("\n✅ Analysis complete!");