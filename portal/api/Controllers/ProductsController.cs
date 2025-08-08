using MediatR;
using Microsoft.AspNetCore.Mvc;
using API.Features.Products.GetProducts;
using API.Features.Products.GetProductMappings;
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

    [HttpGet("{id:guid}/mappings")]
    public async Task<ActionResult<ProductMappingsDto>> GetProductMappings(Guid id)
    {
        var query = new GetProductMappingsQuery(id);
        var mappings = await _mediator.Send(query);
        
        if (mappings == null)
            return NotFound();
            
        return Ok(mappings);
    }
}
