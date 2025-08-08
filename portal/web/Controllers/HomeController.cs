using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var mappings = JsonSerializer.Deserialize<Contracts.DTOs.ProductMappingsDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
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

    public IActionResult Error()
    {
        return View();
    }
}
