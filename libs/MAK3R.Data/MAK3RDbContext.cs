using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MAK3R.DigitalTwin.Entities;
using MAK3R.Data.Entities;
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

    // DigitalTwin2 Knowledge Graph
    public DbSet<KnowledgeEntity> KnowledgeEntities => Set<KnowledgeEntity>();
    public DbSet<EntityAttribute> EntityAttributes => Set<EntityAttribute>();
    public DbSet<EntityRelation> EntityRelations => Set<EntityRelation>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<EventLedger> EventLedger => Set<EventLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCompany(modelBuilder);
        ConfigureSite(modelBuilder);
        ConfigureMachine(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureConnectorConfiguration(modelBuilder);

        // DigitalTwin2 Knowledge Graph
        ConfigureKnowledgeGraph(modelBuilder);
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

    private static void ConfigureKnowledgeGraph(ModelBuilder modelBuilder)
    {
        // Configure KnowledgeEntity
        var entityBuilder = modelBuilder.Entity<KnowledgeEntity>();
        entityBuilder.HasKey(e => e.Id);
        entityBuilder.Property(e => e.Id).HasMaxLength(50);
        entityBuilder.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entityBuilder.Property(e => e.DataRoomId).IsRequired().HasMaxLength(50);
        entityBuilder.HasIndex(e => new { e.Type, e.DataRoomId });
        entityBuilder.HasIndex(e => e.DataRoomId);

        // Configure EntityAttribute
        var attributeBuilder = modelBuilder.Entity<EntityAttribute>();
        attributeBuilder.HasKey(a => a.Id);
        attributeBuilder.Property(a => a.Id).HasMaxLength(50);
        attributeBuilder.Property(a => a.EntityId).IsRequired().HasMaxLength(50);
        attributeBuilder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        attributeBuilder.Property(a => a.ValueType).HasMaxLength(50);
        attributeBuilder.Property(a => a.EvidenceId).HasMaxLength(50);
        attributeBuilder.HasIndex(a => new { a.EntityId, a.Name });
        attributeBuilder.HasIndex(a => a.EvidenceId);
        
        // Store Value as JSON for flexibility
        attributeBuilder.Property(a => a.Value)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<object>(v, (JsonSerializerOptions?)null)
            );

        // Configure EntityRelation
        var relationBuilder = modelBuilder.Entity<EntityRelation>();
        relationBuilder.HasKey(r => r.Id);
        relationBuilder.Property(r => r.Id).HasMaxLength(50);
        relationBuilder.Property(r => r.SourceEntityId).IsRequired().HasMaxLength(50);
        relationBuilder.Property(r => r.TargetEntityId).IsRequired().HasMaxLength(50);
        relationBuilder.Property(r => r.RelationshipType).IsRequired().HasMaxLength(100);
        relationBuilder.Property(r => r.EvidenceId).HasMaxLength(50);
        relationBuilder.HasIndex(r => new { r.SourceEntityId, r.RelationshipType });
        relationBuilder.HasIndex(r => new { r.TargetEntityId, r.RelationshipType });
        relationBuilder.HasIndex(r => r.EvidenceId);

        relationBuilder.Property(r => r.Properties)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );

        // Configure Evidence
        var evidenceBuilder = modelBuilder.Entity<Evidence>();
        evidenceBuilder.HasKey(e => e.Id);
        evidenceBuilder.Property(e => e.Id).HasMaxLength(50);
        evidenceBuilder.Property(e => e.SourceType).IsRequired().HasMaxLength(50);
        evidenceBuilder.Property(e => e.SourceId).IsRequired().HasMaxLength(200);
        evidenceBuilder.Property(e => e.SourcePath).IsRequired().HasMaxLength(1000);
        evidenceBuilder.Property(e => e.BoundingBox).HasMaxLength(100);
        evidenceBuilder.Property(e => e.TextSpan).HasMaxLength(5000);
        evidenceBuilder.Property(e => e.ExtractionMethod).IsRequired().HasMaxLength(50);
        evidenceBuilder.Property(e => e.DataRoomId).IsRequired().HasMaxLength(50);
        evidenceBuilder.Property(e => e.CorrelationId).IsRequired().HasMaxLength(50);
        evidenceBuilder.HasIndex(e => new { e.SourceType, e.SourceId });
        evidenceBuilder.HasIndex(e => e.DataRoomId);
        evidenceBuilder.HasIndex(e => e.CorrelationId);

        evidenceBuilder.Property(e => e.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );

        // Configure EventLedger
        var eventBuilder = modelBuilder.Entity<EventLedger>();
        eventBuilder.HasKey(e => e.Id);
        eventBuilder.Property(e => e.Id).HasMaxLength(50);
        eventBuilder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        eventBuilder.Property(e => e.SourceId).IsRequired().HasMaxLength(200);
        eventBuilder.Property(e => e.SourceType).IsRequired().HasMaxLength(50);
        eventBuilder.Property(e => e.DataRoomId).IsRequired().HasMaxLength(50);
        eventBuilder.Property(e => e.CorrelationId).IsRequired().HasMaxLength(50);
        eventBuilder.HasIndex(e => new { e.EventType, e.SourceId });
        eventBuilder.HasIndex(e => new { e.DataRoomId, e.EventTimestamp });
        eventBuilder.HasIndex(e => e.SequenceNumber).IsUnique();
        eventBuilder.HasIndex(e => e.EventTimestamp);

        eventBuilder.Property(e => e.EventData)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
            );
    }
}