using Microsoft.EntityFrameworkCore;
using Contracts.Models;
using DAL;

namespace DAL.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly PortalDbContext _context;

    public ProductRepository(PortalDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetActiveAsync()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.DatabaseSchemas)
                .ThenInclude(ds => ds.TableColumns)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetByNameAsync(string name)
    {
        return await _context.Products
            .Include(p => p.DatabaseSchemas)
                .ThenInclude(ds => ds.TableColumns)
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        return product;
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        
        return product;
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Products.AnyAsync(p => p.Id == id);
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.Products.AnyAsync(p => p.Name == name);
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Products.CountAsync();
    }
}
