using System.Text;
using System.Text.Json;
using DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Products.ExportProductMapping;

public sealed record ExportProductMappingQuery(Guid ProductId) : IRequest<(string FileName, string Json)>;

public sealed class ExportProductMappingQueryHandler : IRequestHandler<ExportProductMappingQuery, (string FileName, string Json)>
{
    private readonly PortalDbContext _db;

    public ExportProductMappingQueryHandler(PortalDbContext db)
    {
        _db = db;
    }

    public async Task<(string FileName, string Json)> Handle(ExportProductMappingQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product == null) throw new InvalidOperationException("Product not found");

        var schemas = await _db.DatabaseSchemas.AsNoTracking()
            .Where(s => s.ProductId == request.ProductId)
            .OrderBy(s => s.SchemaName).ThenBy(s => s.TableName)
            .Include(s => s.TableColumns)
                .ThenInclude(c => c.ColumnObfuscationMapping)
            .ToListAsync(cancellationToken);

        var tables = new List<object>();
        foreach (var s in schemas)
        {
            var pk = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(s.PrimaryKeyColumns))
                {
                    pk = JsonSerializer.Deserialize<List<string>>(s.PrimaryKeyColumns!) ?? new List<string>();
                }
            }
            catch { pk = new List<string>(); }

            var cols = s.TableColumns
                .OrderBy(c => c.OrdinalPosition)
                .Where(c => c.ColumnObfuscationMapping != null && c.ColumnObfuscationMapping.IsEnabled)
                .Select(c => new {
                    ColumnName = c.ColumnName,
                    DataType = c.ColumnObfuscationMapping!.ObfuscationDataType,
                    Enabled = c.ColumnObfuscationMapping!.IsEnabled,
                    IsNullable = c.IsNullable,
                    PreserveLength = c.ColumnObfuscationMapping!.PreserveLength
                })
                .ToList();

            if (cols.Count == 0) continue; // only include tables with at least one mapped column

            tables.Add(new {
                TableName = s.TableName,
                Schema = s.SchemaName,
                FullTableName = s.FullTableName,
                PrimaryKey = pk,
                Columns = cols
            });
        }

        var defaultConfig = await _db.ObfuscationConfigurations.AsNoTracking()
            .Where(o => o.ProductId == request.ProductId && o.IsDefault)
            .OrderByDescending(o => o.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var fileName = defaultConfig?.Name ?? ($"{product.Name}-mapping.json");

        var payload = new
        {
            Global = new
            {
                ConnectionString = product.ConnectionString,
                DatabaseTechnology = product.DatabaseTechnology,
                GlobalSeed = product.GlobalSeed,
                BatchSize = product.BatchSize,
                SqlBatchSize = product.SqlBatchSize,
                ParallelThreads = product.ParallelThreads,
                MaxCacheSize = product.MaxCacheSize,
                DryRun = false,
                PersistMappings = true,
                EnableValueCaching = true,
                CommandTimeoutSeconds = product.CommandTimeoutSeconds,
                MappingCacheDirectory = product.MappingCacheDirectory
            },
            DataTypes = new { },
            ReferentialIntegrity = new
            {
                Enabled = false,
                Relationships = Array.Empty<object>(),
                StrictMode = false,
                OnViolation = "warn"
            },
            PostProcessing = new
            {
                GenerateReport = true,
                ReportPath = $"reports/{product.Name}-obfuscation-{"{timestamp}"}.json",
                ValidateResults = true,
                BackupMappings = true
            },
            Tables = tables
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

        return (fileName, json);
    }
}


