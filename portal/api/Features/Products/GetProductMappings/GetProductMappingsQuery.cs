using AutoMapper;
using Contracts.DTOs;
using DAL.Repositories;
using MediatR;

namespace API.Features.Products.GetProductMappings;

public record GetProductMappingsQuery(Guid ProductId) : IRequest<ProductMappingsDto?>;

public class GetProductMappingsQueryHandler : IRequestHandler<GetProductMappingsQuery, ProductMappingsDto?>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetProductMappingsQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<ProductMappingsDto?> Handle(GetProductMappingsQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId);
        if (product == null)
            return null;

        return _mapper.Map<ProductMappingsDto>(product);
    }
}
