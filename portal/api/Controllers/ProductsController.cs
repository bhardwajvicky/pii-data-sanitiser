using MediatR;
using Microsoft.AspNetCore.Mvc;
using API.Features.Products.GetProducts;
using API.Features.Products.GetProductMappings;
using API.Features.Products.RefreshProductSchema;
using API.Features.Products.UpdateColumnMapping;
using API.Features.Config.GetSupportedDataTypes;
using API.Features.Products.ExportProductMapping;
using API.Features.Products.UpdateGlobalSettings;
using API.Features.Products.TestConnection;
using Contracts.DTOs;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Contracts.DTOs.ProductDto>>> GetProducts()
    {
        var query = new GetProductsQuery();
        var products = await _mediator.Send(query);
        return Ok(products);
    }

        [HttpPost]
        public async Task<ActionResult> CreateProduct([FromBody] Contracts.DTOs.CreateProductDto dto)
        {
            // Minimal creation using EF via repository not wired; implement inline using DAL context to keep scope small
            return StatusCode(501); // Not implemented in this scope
        }

    [HttpGet("{id:guid}/mappings")]
    public async Task<ActionResult<ProductMappingsDto>> GetProductMappings(Guid id)
    {
        var query = new GetProductMappingsQuery(id);
        var mappings = await _mediator.Send(query);
        
        if (mappings == null)
            return NotFound();
            
        return Ok(mappings);
    }

        [HttpGet("supported-data-types")]
        public async Task<ActionResult<IReadOnlyList<string>>> GetSupportedDataTypes()
        {
            var result = await _mediator.Send(new GetSupportedDataTypesQuery());
            return Ok(result);
        }

        [HttpPost("{id:guid}/refresh-schema")]
        public async Task<IActionResult> RefreshProductSchema(Guid id)
        {
            var cmd = new RefreshProductSchemaCommand(id);
            var ok = await _mediator.Send(cmd);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpPost("{id:guid}/columns/{columnId:guid}/mapping")]
        public async Task<IActionResult> UpdateColumnMapping(Guid id, Guid columnId, [FromBody] UpdateColumnMappingRequest body)
        {
            var cmd = new UpdateColumnMappingCommand(id, columnId, body.ObfuscationDataType, body.IsEnabled, body.PreserveLength, body.IsManuallyConfigured);
            var ok = await _mediator.Send(cmd);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpGet("{id:guid}/export-mapping")]
        public async Task<IActionResult> ExportMapping(Guid id)
        {
            var (fileName, json) = await _mediator.Send(new ExportProductMappingQuery(id));
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            return File(bytes, "application/json");
        }

        [HttpPost("{id:guid}/global-settings")]
        public async Task<IActionResult> UpdateGlobalSettings(Guid id, [FromBody] UpdateGlobalSettingsRequest body)
        {
            var ok = await _mediator.Send(new UpdateGlobalSettingsCommand(id, body));
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpGet("{id:guid}/test-connection")]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            var result = await _mediator.Send(new TestProductConnectionQuery(id));
            return Ok(new { success = result.ok, message = result.message });
        }
}

public class UpdateColumnMappingRequest
{
    public string? ObfuscationDataType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool PreserveLength { get; set; } = false;
    public bool IsManuallyConfigured { get; set; } = true;
}
