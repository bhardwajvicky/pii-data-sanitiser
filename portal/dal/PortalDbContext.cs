using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace DAL;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<DatabaseSchema> DatabaseSchemas { get; set; }
    public DbSet<TableColumn> TableColumns { get; set; }
    public DbSet<ColumnObfuscationMapping> ColumnObfuscationMappings { get; set; }
    public DbSet<ObfuscationConfiguration> ObfuscationConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DatabaseTechnology).IsRequired().HasMaxLength(50);
            entity.Property(e => e.GlobalSeed).HasMaxLength(255);
            entity.Property(e => e.MappingCacheDirectory).HasMaxLength(500);
            entity.Property(e => e.ConnectionString).IsRequired();
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);

            // Inform EF Core that this table has a trigger so it avoids using a plain OUTPUT clause
            entity.ToTable(tb => tb.HasTrigger("tr_Products_Audit"));
        });

        // DatabaseSchema configuration
        modelBuilder.Entity<DatabaseSchema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SchemaName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TableName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FullTableName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PrimaryKeyColumns);
            entity.Property(e => e.RowCount).HasColumnType("bigint");
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.DatabaseSchemas)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.FullTableName);
            entity.HasIndex(e => e.IsAnalyzed);
        });

        // TableColumn configuration
        modelBuilder.Entity<TableColumn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ColumnName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.SqlDataType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DefaultValue);
            entity.Property(e => e.CharacterSet).HasMaxLength(100);
            entity.Property(e => e.Collation).HasMaxLength(100);
            
            entity.HasOne(d => d.DatabaseSchema)
                .WithMany(p => p.TableColumns)
                .HasForeignKey(d => d.DatabaseSchemaId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.DatabaseSchemaId);
            entity.HasIndex(e => e.ColumnName);
            entity.HasIndex(e => e.IsPrimaryKey);
        });

        // ColumnObfuscationMapping configuration
        modelBuilder.Entity<ColumnObfuscationMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ObfuscationDataType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DetectionReasons);
            entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(3,2)");
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ColumnObfuscationMappings)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(d => d.TableColumn)
                .WithOne(p => p.ColumnObfuscationMapping)
                .HasForeignKey<ColumnObfuscationMapping>(d => d.TableColumnId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.TableColumnId);
            entity.HasIndex(e => e.IsEnabled);
        });

        // ObfuscationConfiguration configuration
        modelBuilder.Entity<ObfuscationConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ConfigurationJson).IsRequired();
            
            entity.HasOne(d => d.Product)
                .WithMany(p => p.ObfuscationConfigurations)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsDefault);
        });
    }
}
