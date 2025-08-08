using MediatR;
using DAL;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Products.UpdateColumnMapping;

public record UpdateColumnMappingCommand(Guid ProductId, Guid ColumnId, string? ObfuscationDataType, bool IsEnabled, bool PreserveLength, bool IsManuallyConfigured) : IRequest<bool>;

public class UpdateColumnMappingCommandHandler : IRequestHandler<UpdateColumnMappingCommand, bool>
{
    private readonly PortalDbContext _db;

    public UpdateColumnMappingCommandHandler(PortalDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(UpdateColumnMappingCommand request, CancellationToken cancellationToken)
    {
        // Validate column belongs to the product
        var column = await _db.TableColumns
            .Include(c => c.DatabaseSchema)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId, cancellationToken);
        if (column == null || column.DatabaseSchema == null || column.DatabaseSchema.ProductId != request.ProductId)
            return false;

        var existing = await _db.ColumnObfuscationMappings
            .FirstOrDefaultAsync(m => m.ProductId == request.ProductId && m.TableColumnId == request.ColumnId, cancellationToken);

        // If no type provided or 'None' -> remove mapping if exists
        if (string.IsNullOrWhiteSpace(request.ObfuscationDataType) || string.Equals(request.ObfuscationDataType, "None", StringComparison.OrdinalIgnoreCase))
        {
            if (existing != null)
            {
                _db.ColumnObfuscationMappings.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        if (existing == null)
        {
            existing = new Contracts.Models.ColumnObfuscationMapping
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                TableColumnId = request.ColumnId,
                ObfuscationDataType = request.ObfuscationDataType!,
                IsEnabled = request.IsEnabled,
                PreserveLength = request.PreserveLength,
                IsManuallyConfigured = request.IsManuallyConfigured,
                ConfidenceScore = 1.0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ColumnObfuscationMappings.Add(existing);
        }
        else
        {
            existing.ObfuscationDataType = request.ObfuscationDataType!;
            existing.IsEnabled = request.IsEnabled;
            existing.PreserveLength = request.PreserveLength;
            existing.IsManuallyConfigured = request.IsManuallyConfigured;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
