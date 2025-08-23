using MAK3R.Connectors.Abstractions;

namespace MAK3R.DigitalTwin.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public List<ExternalRef> ExternalRefs { get; protected set; } = new();
    public int Version { get; protected set; } = 1;
    public List<string> Tags { get; protected set; } = new();
    public DateTime CreatedUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; protected set; } = DateTime.UtcNow;

    protected BaseEntity() { }

    protected BaseEntity(Guid id)
    {
        Id = id;
    }

    public void AddExternalRef(string connectorId, string externalId, string entityType)
    {
        var existing = ExternalRefs.FirstOrDefault(x => x.ConnectorId == connectorId && x.EntityType == entityType);
        if (existing != null)
        {
            ExternalRefs.Remove(existing);
        }

        ExternalRefs.Add(new ExternalRef(connectorId, externalId, entityType));
        UpdateVersion();
    }

    public void AddTag(string tag)
    {
        if (!Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            Tags.Add(tag);
            UpdateVersion();
        }
    }

    public void RemoveTag(string tag)
    {
        if (Tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            UpdateVersion();
        }
    }

    protected void UpdateVersion()
    {
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }
}