using DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Products.UpdateGlobalSettings;

public sealed record UpdateGlobalSettingsCommand(Guid ProductId, UpdateGlobalSettingsRequest Body) : IRequest<bool>;

public sealed class UpdateGlobalSettingsRequest
{
    public string? ConnectionString { get; set; }
    public string? GlobalSeed { get; set; }
    public int? BatchSize { get; set; }
    public int? SqlBatchSize { get; set; }
    public int? ParallelThreads { get; set; }
    public int? MaxCacheSize { get; set; }
    public int? CommandTimeoutSeconds { get; set; }
    public string? MappingCacheDirectory { get; set; }
}

public sealed class UpdateGlobalSettingsCommandHandler : IRequestHandler<UpdateGlobalSettingsCommand, bool>
{
    private readonly PortalDbContext _db;
    public UpdateGlobalSettingsCommandHandler(PortalDbContext db) { _db = db; }

    public async Task<bool> Handle(UpdateGlobalSettingsCommand request, CancellationToken cancellationToken)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
        if (p == null) return false;

        var b = request.Body;
        if (b.ConnectionString != null) p.ConnectionString = b.ConnectionString;
        if (b.GlobalSeed != null) p.GlobalSeed = b.GlobalSeed;
        if (b.BatchSize.HasValue) p.BatchSize = b.BatchSize.Value;
        if (b.SqlBatchSize.HasValue) p.SqlBatchSize = b.SqlBatchSize.Value;
        if (b.ParallelThreads.HasValue) p.ParallelThreads = b.ParallelThreads.Value;
        if (b.MaxCacheSize.HasValue) p.MaxCacheSize = b.MaxCacheSize.Value;
        if (b.CommandTimeoutSeconds.HasValue) p.CommandTimeoutSeconds = b.CommandTimeoutSeconds.Value;
        if (b.MappingCacheDirectory != null) p.MappingCacheDirectory = b.MappingCacheDirectory;

        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = "Portal";
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}


