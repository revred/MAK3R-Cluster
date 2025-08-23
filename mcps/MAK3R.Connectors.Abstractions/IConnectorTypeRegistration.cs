namespace MAK3R.Connectors.Abstractions;

/// <summary>
/// Marker interface for connector type registrations
/// Used to track which connector types have been registered in DI
/// </summary>
public interface IConnectorTypeRegistration
{
    string TypeId { get; }
}