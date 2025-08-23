using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MAK3R.DigitalTwin.Entities;
using MAK3R.Connectors.Abstractions;
using System.Text.Json;

namespace MAK3R.Data;

public class MAK3RDbContext : IdentityDbContext<IdentityUser>
{
    public MAK3RDbContext(DbContextOptions<MAK3RDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ConnectorConfiguration> ConnectorConfigurations => Set<ConnectorConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCompany(modelBuilder);
        ConfigureSite(modelBuilder);
        ConfigureMachine(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureConnectorConfiguration(modelBuilder);
    }

    private static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        var companyEntity = modelBuilder.Entity<Company>();
        
        companyEntity.HasKey(c => c.Id);
        companyEntity.Property(c => c.Name).IsRequired().HasMaxLength(200);
        companyEntity.Property(c => c.RegistrationId).HasMaxLength(100);
        companyEntity.Property(c => c.TaxId).HasMaxLength(100);
        companyEntity.Property(c => c.Industry).HasMaxLength(100);
        companyEntity.Property(c => c.Website).HasMaxLength(500);
        companyEntity.Property(c => c.Address).HasMaxLength(1000);

        // Configure complex properties as JSON
        companyEntity.Property(c => c.ExternalRefs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ExternalRef>>(v, (JsonSerializerOptions?)null) ?? new List<ExternalRef>()
            );

        companyEntity.Property(c => c.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

        // Configure relationships
        companyEntity.HasMany(c => c.Sites)
            .WithOne()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        companyEntity.HasMany(c => c.Products)
            .WithOne()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSite(ModelBuilder modelBuilder)
    {
        var siteEntity = modelBuilder.Entity<Site>();
        
        siteEntity.HasKey(s => s.Id);
        siteEntity.Property(s => s.Name).IsRequired().HasMaxLength(200);
        siteEntity.Property(s => s.Address).HasMaxLength(1000);
        siteEntity.Property(s => s.City).HasMaxLength(100);
        siteEntity.Property(s => s.Country).HasMaxLength(100);
        siteEntity.Property(s => s.Description).HasMaxLength(2000);

        // Configure complex properties as JSON
        siteEntity.Property(s => s.ExternalRefs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ExternalRef>>(v, (JsonSerializerOptions?)null) ?? new List<ExternalRef>()
            );

        siteEntity.Property(s => s.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

        // Configure relationships
        siteEntity.HasMany(s => s.Machines)
            .WithOne()
            .HasForeignKey(m => m.SiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureMachine(ModelBuilder modelBuilder)
    {
        var machineEntity = modelBuilder.Entity<Machine>();
        
        machineEntity.HasKey(m => m.Id);
        machineEntity.Property(m => m.Name).IsRequired().HasMaxLength(200);
        machineEntity.Property(m => m.Make).HasMaxLength(100);
        machineEntity.Property(m => m.Model).HasMaxLength(100);
        machineEntity.Property(m => m.SerialNumber).HasMaxLength(100);
        machineEntity.Property(m => m.OpcUaNode).HasMaxLength(500);

        // Configure complex properties as JSON
        machineEntity.Property(m => m.ExternalRefs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ExternalRef>>(v, (JsonSerializerOptions?)null) ?? new List<ExternalRef>()
            );

        machineEntity.Property(m => m.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

        machineEntity.Property(m => m.CurrentMetrics)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        var productEntity = modelBuilder.Entity<Product>();
        
        productEntity.HasKey(p => p.Id);
        productEntity.Property(p => p.Name).IsRequired().HasMaxLength(200);
        productEntity.Property(p => p.Sku).HasMaxLength(100);
        productEntity.Property(p => p.Currency).HasMaxLength(10);
        productEntity.Property(p => p.Description).HasMaxLength(2000);
        productEntity.Property(p => p.Category).HasMaxLength(100);
        productEntity.Property(p => p.ImageUrl).HasMaxLength(1000);
        productEntity.Property(p => p.Manufacturer).HasMaxLength(200);

        // Configure complex properties as JSON
        productEntity.Property(p => p.ExternalRefs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ExternalRef>>(v, (JsonSerializerOptions?)null) ?? new List<ExternalRef>()
            );

        productEntity.Property(p => p.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

        productEntity.Property(p => p.Attributes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );
    }

    private static void ConfigureConnectorConfiguration(ModelBuilder modelBuilder)
    {
        var connectorEntity = modelBuilder.Entity<ConnectorConfiguration>();
        
        connectorEntity.HasKey(c => c.ConnectorId);
        connectorEntity.Property(c => c.ConnectorId).HasMaxLength(100);

        connectorEntity.Property(c => c.Settings)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );
    }
}