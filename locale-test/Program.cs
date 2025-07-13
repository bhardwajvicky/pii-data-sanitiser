using Bogus;
using Bogus.Extensions.UnitedKingdom;
using Bogus.DataSets;

Console.WriteLine("=== BOGUS LOCALE TESTING ===\n");

// Test Australian locale
Console.WriteLine("🇦🇺 AUSTRALIAN LOCALE (en_AU) TESTING:");
var auFaker = new Faker("en_AU");
Console.WriteLine($"Name: {auFaker.Name.FullName()}");
Console.WriteLine($"Phone: {auFaker.Phone.PhoneNumber()}");
Console.WriteLine($"Address: {auFaker.Address.FullAddress()}");
Console.WriteLine($"Email: {auFaker.Internet.Email()}");
Console.WriteLine($"Credit Card: {auFaker.Finance.CreditCardNumber()}");
Console.WriteLine($"Company: {auFaker.Company.CompanyName()}");

// Test UK locale
Console.WriteLine("\n🇬🇧 UK LOCALE (en_GB) TESTING:");
var ukFaker = new Faker("en_GB");
Console.WriteLine($"Name: {ukFaker.Name.FullName()}");
Console.WriteLine($"Phone: {ukFaker.Phone.PhoneNumber()}");
Console.WriteLine($"Address: {ukFaker.Address.FullAddress()}");
Console.WriteLine($"Email: {ukFaker.Internet.Email()}");
Console.WriteLine($"Credit Card: {ukFaker.Finance.CreditCardNumber()}");
Console.WriteLine($"Company: {ukFaker.Company.CompanyName()}");

// UK-specific extensions
Console.WriteLine("\n🇬🇧 UK-SPECIFIC EXTENSIONS:");
try 
{
    Console.WriteLine($"NINO: {ukFaker.Finance.Nino()}");
    Console.WriteLine($"Sort Code: {ukFaker.Finance.SortCode()}");
    Console.WriteLine($"GB Reg Plate: {ukFaker.Vehicle.GbRegistrationPlate(new DateTime(2010, 1, 1), DateTime.Now)}");
    Console.WriteLine("✅ UK Extensions working!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ UK Extensions error: {ex.Message}");
}

// Test locale differences with same seed
Console.WriteLine("\n📊 LOCALE COMPARISON (Same Seed):");
Console.WriteLine("Australian vs UK Name Generation:");
for (int i = 0; i < 5; i++)
{
    var auFakerSeeded = new Faker("en_AU") { Random = new Randomizer(i) };
    var ukFakerSeeded = new Faker("en_GB") { Random = new Randomizer(i) };
    Console.WriteLine($"  Seed {i}: AU: {auFakerSeeded.Name.FullName()} | UK: {ukFakerSeeded.Name.FullName()}");
}

// Test different credit card types
Console.WriteLine("\n💳 CREDIT CARD GENERATION:");
Console.WriteLine($"Random Card: {ukFaker.Finance.CreditCardNumber()}");
Console.WriteLine($"Visa: {ukFaker.Finance.CreditCardNumber(CardType.Visa)}");
Console.WriteLine($"Mastercard: {ukFaker.Finance.CreditCardNumber(CardType.Mastercard)}");
Console.WriteLine($"Amex: {ukFaker.Finance.CreditCardNumber(CardType.AmericanExpress)}");

// Test all available locales
Console.WriteLine("\n🌍 AVAILABLE LOCALES:");
var availableLocales = new[] { "en", "en_AU", "en_GB", "en_US", "en_CA", "fr", "de", "es", "it", "pt_BR" };
foreach (var locale in availableLocales)
{
    try
    {
        var testFaker = new Faker(locale);
        Console.WriteLine($"  {locale}: {testFaker.Name.FullName()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {locale}: ❌ {ex.Message}");
    }
}

Console.WriteLine("\n✅ All locale tests completed!");