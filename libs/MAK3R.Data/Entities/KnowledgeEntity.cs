using MAK3R.Core;

namespace MAK3R.Data.Entities;

/// <summary>
/// DigitalTwin2 Knowledge Graph Entity - core of EAV model
/// Every piece of knowledge in the system is represented as an entity
/// with versioned attributes and evidence-linked facts
/// </summary>
public class KnowledgeEntity
{
    public string Id { get; private set; }
    public string Type { get; private set; }
    public string DataRoomId { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public int Version { get; private set; }
    public List<EntityAttribute> Attributes { get; private set; }
    public List<EntityRelation> Relations { get; private set; }

    protected KnowledgeEntity() 
    {
        Attributes = new List<EntityAttribute>();
        Relations = new List<EntityRelation>();
    }

    public KnowledgeEntity(string type, string dataRoomId) : this()
    {
        Guard.NotNullOrWhiteSpace(type);
        Guard.NotNullOrWhiteSpace(dataRoomId);

        Id = UlidGenerator.NewId();
        Type = type;
        DataRoomId = dataRoomId;
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
        Version = 1;
    }

    /// <summary>
    /// Set or update an attribute with evidence tracking
    /// </summary>
    public EntityAttribute SetAttribute(string name, object value, double confidence, string? evidenceId = null)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(confidence);

        var existing = Attributes.FirstOrDefault(a => a.Name == name);
        if (existing != null)
        {
            existing.UpdateValue(value, confidence, evidenceId);
        }
        else
        {
            var attribute = new EntityAttribute(Id, name, value, confidence, evidenceId);
            Attributes.Add(attribute);
            existing = attribute;
        }

        UpdateVersion();
        return existing;
    }

    /// <summary>
    /// Add a relation to another entity
    /// </summary>
    public EntityRelation AddRelation(string relationshipType, string targetEntityId, double confidence = 1.0, string? evidenceId = null)
    {
        Guard.NotNullOrWhiteSpace(relationshipType);
        Guard.NotNullOrWhiteSpace(targetEntityId);
        Guard.NotNull(confidence);

        var relation = new EntityRelation(Id, targetEntityId, relationshipType, confidence, evidenceId);
        Relations.Add(relation);
        
        UpdateVersion();
        return relation;
    }

    /// <summary>
    /// Get attribute value with optional type conversion
    /// </summary>
    public T? GetAttributeValue<T>(string name)
    {
        var attribute = Attributes.FirstOrDefault(a => a.Name == name);
        if (attribute?.Value == null) return default;

        if (attribute.Value is T directValue)
            return directValue;

        try
        {
            return (T)Convert.ChangeType(attribute.Value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Get all relations of a specific type
    /// </summary>
    public List<EntityRelation> GetRelations(string relationshipType)
    {
        return Relations.Where(r => r.RelationshipType == relationshipType).ToList();
    }

    private void UpdateVersion()
    {
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }
}