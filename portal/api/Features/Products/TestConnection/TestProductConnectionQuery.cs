using DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace API.Features.Products.TestConnection;

public sealed record TestProductConnectionQuery(Guid ProductId) : IRequest<(bool ok, string message)>;

public sealed class TestProductConnectionQueryHandler : IRequestHandler<TestProductConnectionQuery, (bool ok, string message)>
{
    private readonly PortalDbContext _db;
    public TestProductConnectionQueryHandler(PortalDbContext db) { _db = db; }

    public async Task<(bool ok, string message)> Handle(TestProductConnectionQuery request, CancellationToken cancellationToken)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
        if (p == null) return (false, "Product not found");
        if (string.IsNullOrWhiteSpace(p.ConnectionString)) return (false, "Connection string is empty");

        try
        {
            switch ((p.DatabaseTechnology ?? "").ToLowerInvariant())
            {
                case "sqlserver":
                    await using (var conn = new SqlConnection(p.ConnectionString))
                    {
                        await conn.OpenAsync(cancellationToken);
                        await conn.CloseAsync();
                    }
                    break;
                default:
                    return (false, $"Unsupported technology: {p.DatabaseTechnology}");
            }
            return (true, "Connection successful");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}


