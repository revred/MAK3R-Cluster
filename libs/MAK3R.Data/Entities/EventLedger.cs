using MAK3R.Core;

namespace MAK3R.Data.Entities;

/// <summary>
/// DigitalTwin2 Event Ledger - immutable append-only event store
/// Captures all system events with temporal ordering for SPOF analysis
/// </summary>
public class EventLedger
{
    public string Id { get; private set; }
    public string EventType { get; private set; }
    public string SourceId { get; private set; }
    public string SourceType { get; private set; }
    public string DataRoomId { get; private set; }
    public string CorrelationId { get; private set; }
    public Dictionary<string, object> EventData { get; private set; }
    public DateTime EventTimestamp { get; private set; }
    public DateTime IngestedUtc { get; private set; }
    public long SequenceNumber { get; private set; }

    protected EventLedger() 
    { 
        EventData = new Dictionary<string, object>();
    }

    public EventLedger(
        string eventType,
        string sourceId,
        string sourceType,
        string dataRoomId,
        string correlationId,
        DateTime eventTimestamp,
        long sequenceNumber) : this()
    {
        Guard.NotNullOrWhiteSpace(eventType);
        Guard.NotNullOrWhiteSpace(sourceId);
        Guard.NotNullOrWhiteSpace(sourceType);
        Guard.NotNullOrWhiteSpace(dataRoomId);
        Guard.NotNullOrWhiteSpace(correlationId);

        Id = UlidGenerator.NewId();
        EventType = eventType;
        SourceId = sourceId;
        SourceType = sourceType;
        DataRoomId = dataRoomId;
        CorrelationId = correlationId;
        EventTimestamp = eventTimestamp;
        SequenceNumber = sequenceNumber;
        IngestedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Set event data payload
    /// </summary>
    public void SetEventData(Dictionary<string, object> data)
    {
        Guard.NotNull(data);
        EventData = new Dictionary<string, object>(data);
    }

    /// <summary>
    /// Add event data property
    /// </summary>
    public void AddEventData(string key, object value)
    {
        Guard.NotNullOrWhiteSpace(key);
        EventData[key] = value;
    }

    /// <summary>
    /// Get typed event data value
    /// </summary>
    public T? GetEventData<T>(string key)
    {
        if (!EventData.TryGetValue(key, out var value) || value == null)
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
    /// Machine status events for SPOF analysis
    /// </summary>
    public static EventLedger MachineStatusEvent(string machineId, string status, string dataRoomId, string correlationId, DateTime timestamp, long sequenceNumber)
    {
        var evt = new EventLedger("machine.status", machineId, "Machine", dataRoomId, correlationId, timestamp, sequenceNumber);
        evt.AddEventData("status", status);
        evt.AddEventData("previousStatus", null);
        return evt;
    }

    /// <summary>
    /// Production events for throughput analysis
    /// </summary>
    public static EventLedger ProductionEvent(string machineId, string productId, int quantity, string dataRoomId, string correlationId, DateTime timestamp, long sequenceNumber)
    {
        var evt = new EventLedger("production.completed", machineId, "Machine", dataRoomId, correlationId, timestamp, sequenceNumber);
        evt.AddEventData("productId", productId);
        evt.AddEventData("quantity", quantity);
        evt.AddEventData("cycleTime", null);
        return evt;
    }
}