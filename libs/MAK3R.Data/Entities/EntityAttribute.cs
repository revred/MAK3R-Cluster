using MAK3R.Core;

namespace MAK3R.Data.Entities;

/// <summary>
/// DigitalTwin2 Entity Attribute - EAV model attribute with evidence tracking
/// Every attribute has confidence and optional evidence linking for full traceability
/// </summary>
public class EntityAttribute
{
    public string Id { get; private set; }
    public string EntityId { get; private set; }
    public string Name { get; private set; }
    public object Value { get; private set; }
    public string ValueType { get; private set; }
    public double Confidence { get; private set; }
    public string? EvidenceId { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public int Version { get; private set; }

    protected EntityAttribute() { }

    public EntityAttribute(string entityId, string name, object value, double confidence, string? evidenceId = null)
    {
        Guard.NotNullOrWhiteSpace(entityId);
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(confidence);

        Id = UlidGenerator.NewId();
        EntityId = entityId;
        Name = name;
        Value = value;
        ValueType = value?.GetType().Name ?? "null";
        Confidence = confidence;
        EvidenceId = evidenceId;
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
        Version = 1;
    }

    /// <summary>
    /// Update attribute value with new confidence and evidence
    /// </summary>
    public void UpdateValue(object newValue, double confidence, string? evidenceId = null)
    {
        Guard.NotNull(confidence);

        Value = newValue;
        ValueType = newValue?.GetType().Name ?? "null";
        Confidence = confidence;
        EvidenceId = evidenceId;
        Version++;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Get typed value with optional conversion
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value == null) return default;

        if (Value is T directValue)
            return directValue;

        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}