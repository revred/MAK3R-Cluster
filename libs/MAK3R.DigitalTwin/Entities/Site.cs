using MAK3R.Core;

namespace MAK3R.DigitalTwin.Entities;

public class Site : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid CompanyId { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }
    public string? Description { get; private set; }

    public List<Machine> Machines { get; private set; } = new();

    private Site() : base() { }

    public Site(string name, Guid companyId, string? address = null, string? city = null, string? country = null) : base()
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        CompanyId = Guard.NotEmpty(companyId);
        Address = address;
        City = city;
        Country = country;
    }

    public void UpdateDetails(string name, string? address, string? city, string? country, string? description)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Address = address;
        City = city;
        Country = country;
        Description = description;
        UpdateVersion();
    }

    public Machine AddMachine(string name, string? make = null, string? model = null, string? serialNumber = null)
    {
        var machine = new Machine(name, Id, make, model, serialNumber);
        Machines.Add(machine);
        UpdateVersion();
        return machine;
    }

    public void RemoveMachine(Guid machineId)
    {
        var machine = Machines.FirstOrDefault(m => m.Id == machineId);
        if (machine != null)
        {
            Machines.Remove(machine);
            UpdateVersion();
        }
    }
}