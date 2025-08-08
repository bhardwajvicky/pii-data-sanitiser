using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync("/api/products");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var products = JsonSerializer.Deserialize<List<Contracts.DTOs.ProductDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return View(products);
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            ViewBag.Error = "Unable to load products. Please try again later.";
        }
        
        return View(new List<Contracts.DTOs.ProductDto>());
    }

    public async Task<IActionResult> ProductMappings(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"/api/products/{id}/mappings");
            var typesResponse = await client.GetAsync("/api/products/supported-data-types");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var mappings = JsonSerializer.Deserialize<Contracts.DTOs.ProductMappingsDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (typesResponse.IsSuccessStatusCode)
                {
                    var typesJson = await typesResponse.Content.ReadAsStringAsync();
                    var types = JsonSerializer.Deserialize<string[]>(typesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<string>();
                    ViewBag.SupportedTypes = types;
                }
                ViewBag.ProductId = id;
                ViewBag.ApiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:7001/";
                
                return View(mappings);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            ViewBag.Error = "Unable to load product mappings. Please try again later.";
        }
        
        return View(null);
    }

    [HttpPost]
    public async Task<IActionResult> RefreshSchema(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            await client.PostAsync($"/api/products/{id}/refresh-schema", null);
        }
        catch { }
        return RedirectToAction(nameof(ProductMappings), new { id });
    }

    public IActionResult Error()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateMapping(Guid productId, Guid columnId, string? obfuscationDataType, bool isEnabled, bool preserveLength)
    {
        bool ok = false;
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var normalizedType = string.IsNullOrWhiteSpace(obfuscationDataType) || obfuscationDataType == "None" ? null : obfuscationDataType;
            var payload = new
            {
                ObfuscationDataType = normalizedType,
                IsEnabled = isEnabled,
                PreserveLength = preserveLength,
                IsManuallyConfigured = true
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"/api/products/{productId}/columns/{columnId}/mapping", content);
            ok = resp.IsSuccessStatusCode;
            return Ok(new { success = ok, mapped = normalizedType != null && isEnabled, obfuscationDataType = normalizedType });
        }
        catch
        {
            return Ok(new { success = ok, mapped = false, obfuscationDataType = (string?)null });
        }
    }
}
