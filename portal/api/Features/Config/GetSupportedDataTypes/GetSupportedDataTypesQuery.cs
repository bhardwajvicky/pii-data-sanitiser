using MediatR;
using Microsoft.EntityFrameworkCore;
using DAL;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace API.Features.Config.GetSupportedDataTypes;

public sealed record GetSupportedDataTypesQuery() : IRequest<IReadOnlyList<string>>;

public sealed class GetSupportedDataTypesQueryHandler : IRequestHandler<GetSupportedDataTypesQuery, IReadOnlyList<string>>
{
    private readonly PortalDbContext _db;

    public GetSupportedDataTypesQueryHandler(PortalDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> Handle(GetSupportedDataTypesQuery request, CancellationToken cancellationToken)
    {
        // Use a lightweight ADO.NET query to avoid adding the entity to EF model
        var results = new List<string>();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM SupportedDataTypes WHERE IsActive = 1 ORDER BY Name";
        cmd.CommandType = CommandType.Text;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            results.Add(name);
        }

        return results;
    }
}

// Simple query type to map to the existing table without adding it to DAL models
// Retained for reference but not used by the handler
[Table("SupportedDataTypes")]
internal sealed class SupportedDataTypeEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}


