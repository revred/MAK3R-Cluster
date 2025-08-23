using MAK3R.Core;
using MAK3R.Shared.DTOs;

namespace MAK3R.DigitalTwin.Entities;

public class Machine : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid SiteId { get; private set; }
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? OpcUaNode { get; private set; }
    public MachineStatus Status { get; private set; } = MachineStatus.Unknown;
    public Dictionary<string, object> CurrentMetrics { get; private set; } = new();

    private Machine() : base() { }

    public Machine(string name, Guid siteId, string? make = null, string? model = null, string? serialNumber = null) : base()
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        SiteId = Guard.NotEmpty(siteId);
        Make = make;
        Model = model;
        SerialNumber = serialNumber;
    }

    public void UpdateDetails(string name, string? make, string? model, string? serialNumber, string? opcUaNode)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Make = make;
        Model = model;
        SerialNumber = serialNumber;
        OpcUaNode = opcUaNode;
        UpdateVersion();
    }

    public void UpdateStatus(MachineStatus status)
    {
        if (Status != status)
        {
            Status = status;
            UpdateVersion();
        }
    }

    public void UpdateMetric(string metricName, object value, string? unit = null)
    {
        Guard.NotNullOrWhiteSpace(metricName);
        Guard.NotNull(value);

        var metric = new
        {
            Value = value,
            Unit = unit,
            Timestamp = DateTime.UtcNow
        };

        CurrentMetrics[metricName] = metric;
        UpdateVersion();
    }

    public void UpdateMetrics(Dictionary<string, object> metrics)
    {
        foreach (var metric in metrics)
        {
            UpdateMetric(metric.Key, metric.Value);
        }
    }
}