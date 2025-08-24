namespace MAK3R.Core.Models;

public enum MachineStatus
{
    Offline,
    Idle,
    Running,
    Maintenance,
    Error
}

public class MachineData
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public MachineStatus Status { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double VibrationLevel { get; set; }
    public int RPM { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public interface IMachineDataHub
{
    Task SendMachineData(MachineData data);
    Task UpdateStatus(string machineId, MachineStatus status);
}