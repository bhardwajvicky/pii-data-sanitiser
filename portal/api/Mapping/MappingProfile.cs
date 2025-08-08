using AutoMapper;
using Contracts.Models;
using Contracts.DTOs;
using API.Features.Products.GetProductMappings;

namespace API.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Product mappings
        CreateMap<Product, ProductDto>();
        CreateMap<Product, ProductMappingsDto>()
            .ForMember(dest => dest.Tables, opt => opt.MapFrom(src => src.DatabaseSchemas));

        // DatabaseSchema mappings
        CreateMap<DatabaseSchema, DatabaseSchemaDto>();
        CreateMap<DatabaseSchema, TableMappingDto>()
            .ForMember(dest => dest.Columns, opt => opt.MapFrom(src => src.TableColumns));

        // TableColumn mappings
        CreateMap<TableColumn, TableColumnDto>();
        CreateMap<TableColumn, ColumnMappingDto>()
            .ForMember(dest => dest.ObfuscationMapping, opt => opt.MapFrom(src => src.ColumnObfuscationMapping));

        // ColumnObfuscationMapping mappings
        CreateMap<ColumnObfuscationMapping, ColumnObfuscationMappingDto>();
        CreateMap<ColumnObfuscationMapping, ObfuscationMappingDto>();
    }
}
