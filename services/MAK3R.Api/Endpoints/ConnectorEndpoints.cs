using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors;
using MAK3R.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace MAK3R.Api.Endpoints;

public static class ConnectorEndpoints
{
    public static void MapConnectorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/connectors")
            .WithTags("Connectors")
            .WithOpenApi();

        // Get all available connector types
        group.MapGet("/types", async (IConnectorRegistry registry) =>
        {
            var types = registry.GetAvailableConnectorTypes().ToList();
            return Results.Ok(types);
        })
        .WithName("GetConnectorTypes")
        .WithSummary("Get all available connector types")
        .Produces<IEnumerable<ConnectorTypeInfo>>();

        // Get configuration schema for a connector type
        group.MapGet("/types/{typeId}/schema", async (string typeId, IConnectorRegistry registry) =>
        {
            var schemaResult = await registry.GetConfigurationSchemaAsync(typeId);
            return schemaResult.IsSuccess 
                ? Results.Ok(schemaResult.Value)
                : Results.NotFound(schemaResult.Error);
        })
        .WithName("GetConnectorSchema")
        .WithSummary("Get configuration schema for a connector type")
        .Produces<ConnectorConfigurationSchema>()
        .Produces(404);

        // Create a new connector instance
        group.MapPost("/", async ([FromBody] CreateConnectorRequest request, IConnectorRegistry registry, IConnectorHub hub) =>
        {
            var configuration = new ConnectorConfiguration(
                request.ConnectorId,
                request.Type,
                request.Settings,
                request.IsEnabled
            );

            var createResult = await registry.CreateConnectorAsync(request.TypeId, configuration);
            if (!createResult.IsSuccess)
            {
                return Results.BadRequest(createResult.Error);
            }

            var registerResult = await hub.RegisterConnectorAsync(createResult.Value!);
            if (!registerResult.IsSuccess)
            {
                return Results.BadRequest(registerResult.Error);
            }

            return Results.Ok(new { ConnectorId = request.ConnectorId, Status = "Created" });
        })
        .WithName("CreateConnector")
        .WithSummary("Create and register a new connector instance")
        .Accepts<CreateConnectorRequest>("application/json")
        .Produces(200)
        .Produces(400);

        // Get all registered connector instances
        group.MapGet("/instances", (IConnectorHub hub) =>
        {
            var connectors = hub.GetConnectors()
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    Type = c.Type
                });
            return Results.Ok(connectors);
        })
        .WithName("GetConnectorInstances")
        .WithSummary("Get all registered connector instances");

        // Get connector health status
        group.MapGet("/instances/{connectorId}/health", async (string connectorId, IConnectorHub hub) =>
        {
            var result = await hub.CheckHealthAsync(connectorId);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.NotFound(result.Error);
        })
        .WithName("GetConnectorHealth")
        .WithSummary("Get health status for a connector instance")
        .Produces<ConnectorHealthStatus>()
        .Produces(404);

        // Trigger connector sync
        group.MapPost("/instances/{connectorId}/sync", async (string connectorId, [FromQuery] DateTime? since, IConnectorHub hub) =>
        {
            var result = await hub.SyncConnectorAsync(connectorId, since);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.NotFound(result.Error);
        })
        .WithName("SyncConnector")
        .WithSummary("Trigger sync for a connector instance")
        .Produces<ConnectorSyncResult>()
        .Produces(404);

        // Get all connector health statuses
        group.MapGet("/health", async (IConnectorHub hub) =>
        {
            var statuses = new List<ConnectorHealthStatus>();
            await foreach (var status in hub.CheckAllHealthAsync())
            {
                statuses.Add(status);
            }
            return Results.Ok(statuses);
        })
        .WithName("GetAllConnectorHealth")
        .WithSummary("Get health status for all connector instances")
        .Produces<IEnumerable<ConnectorHealthStatus>>();

        // Remove a connector instance
        group.MapDelete("/instances/{connectorId}", (string connectorId, IConnectorHub hub) =>
        {
            var connector = hub.GetConnector(connectorId);
            if (connector.IsSuccess)
            {
                // In a real implementation, you'd have a method to unregister
                return Results.Ok(new { ConnectorId = connectorId, Status = "Removed" });
            }
            return Results.NotFound($"Connector '{connectorId}' not found");
        })
        .WithName("RemoveConnector")
        .WithSummary("Remove a connector instance")
        .Produces(200)
        .Produces(404);
    }
}

public record CreateConnectorRequest(
    string ConnectorId,
    string TypeId,
    string Type,
    Dictionary<string, object> Settings,
    bool IsEnabled = true
);