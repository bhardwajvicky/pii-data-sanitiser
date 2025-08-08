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
            .ForMember(dest => dest.Tables, opt => opt.MapFrom(src => src.DatabaseSchemas))
            .ForMember(dest => dest.ConnectionString, opt => opt.MapFrom(src => src.ConnectionString))
            .ForMember(dest => dest.GlobalSeed, opt => opt.MapFrom(src => src.GlobalSeed))
            .ForMember(dest => dest.BatchSize, opt => opt.MapFrom(src => src.BatchSize))
            .ForMember(dest => dest.SqlBatchSize, opt => opt.MapFrom(src => src.SqlBatchSize))
            .ForMember(dest => dest.ParallelThreads, opt => opt.MapFrom(src => src.ParallelThreads))
            .ForMember(dest => dest.MaxCacheSize, opt => opt.MapFrom(src => src.MaxCacheSize))
            .ForMember(dest => dest.CommandTimeoutSeconds, opt => opt.MapFrom(src => src.CommandTimeoutSeconds))
            .ForMember(dest => dest.MappingCacheDirectory, opt => opt.MapFrom(src => src.MappingCacheDirectory));

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
