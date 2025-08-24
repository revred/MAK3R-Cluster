using MAK3R.Core;

namespace MAK3R.Data.Entities;

/// <summary>
/// DigitalTwin2 Entity Relation - typed relationships between entities with evidence
/// Supports complex relationship graphs with confidence scoring and audit trails
/// </summary>
public class EntityRelation
{
    public string Id { get; private set; }
    public string SourceEntityId { get; private set; }
    public string TargetEntityId { get; private set; }
    public string RelationshipType { get; private set; }
    public double Confidence { get; private set; }
    public string? EvidenceId { get; private set; }
    public Dictionary<string, object> Properties { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public int Version { get; private set; }

    protected EntityRelation() 
    { 
        Properties = new Dictionary<string, object>();
    }

    public EntityRelation(string sourceEntityId, string targetEntityId, string relationshipType, double confidence = 1.0, string? evidenceId = null) : this()
    {
        Guard.NotNullOrWhiteSpace(sourceEntityId);
        Guard.NotNullOrWhiteSpace(targetEntityId);
        Guard.NotNullOrWhiteSpace(relationshipType);
        Guard.NotNull(confidence);

        Id = UlidGenerator.NewId();
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        RelationshipType = relationshipType;
        Confidence = confidence;
        EvidenceId = evidenceId;
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
        Version = 1;
    }

    /// <summary>
    /// Add or update relation property
    /// </summary>
    public void SetProperty(string key, object value)
    {
        Guard.NotNullOrWhiteSpace(key);
        
        Properties[key] = value;
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Get typed property value
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (!Properties.TryGetValue(key, out var value) || value == null)
            return default;

        if (value is T directValue)
            return directValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Update confidence and evidence
    /// </summary>
    public void UpdateConfidence(double confidence, string? evidenceId = null)
    {
        Guard.NotNull(confidence);

        Confidence = confidence;
        EvidenceId = evidenceId;
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }
}