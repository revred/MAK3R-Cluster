using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using MAK3R.Shared.DTOs;
using Microsoft.Extensions.Logging;
using RestSharp;
using System.Text.Json;

namespace MAK3R.Connectors.NetSuite;

public class NetSuiteConnector : IConnector
{
    private readonly ILogger<NetSuiteConnector> _logger;
    private readonly RestClient? _client;
    private readonly NetSuiteConfig _config;
    private readonly bool _isMockMode;

    public string Id => "netsuite-connector";
    public string Name => "NetSuite";
    public string Type => "netsuite";
    public Dictionary<string, object> Metadata { get; } = new();

    public NetSuiteConnector(ILogger<NetSuiteConnector> logger, NetSuiteConfig config)
    {
        _logger = logger;
        _config = config;
        _isMockMode = string.IsNullOrEmpty(_config.AccountId) || _config.IsMockMode;

        if (!_isMockMode)
        {
            var options = new RestClientOptions($"https://{_config.AccountId}.suitetalk.api.netsuite.com")
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _client = new RestClient(options);
        }
        
        Metadata["AccountId"] = _config.AccountId;
        Metadata["IsMockMode"] = _isMockMode;
        Metadata["ApiVersion"] = _config.ApiVersion;
    }

    public async ValueTask<ConnectorCheck> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking NetSuite connector health (Mock Mode: {MockMode})", _isMockMode);

            if (_isMockMode)
            {
                // Simulate health check in mock mode
                await Task.Delay(100, cancellationToken);
                return new ConnectorCheck(
                    true, 
                    "Mock mode - NetSuite connector healthy",
                    new Dictionary<string, object>
                    {
                        { "MockMode", true },
                        { "ResponseTime", 100 }
                    }
                );
            }

            var request = new RestRequest("/services/rest/query/v1/suiteql", Method.Get);
            request.AddHeader("Authorization", $"Bearer {_config.AccessToken}");
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("q", "SELECT id FROM customer FETCH FIRST 1 ROWS ONLY");

            var response = await _client!.ExecuteAsync(request, cancellationToken);

            if (response.IsSuccessful)
            {
                return new ConnectorCheck(
                    true,
                    "Connected to NetSuite successfully",
                    new Dictionary<string, object>
                    {
                        { "ResponseTime", 150 }, // Mock response time since RestSharp doesn't expose ResponseTime
                        { "MockMode", false }
                    }
                );
            }
            else
            {
                _logger.LogWarning("NetSuite health check failed: {StatusCode} - {Content}", 
                    response.StatusCode, response.Content);
                
                return new ConnectorCheck(false, $"Health check failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NetSuite health check");
            return new ConnectorCheck(false, $"Health check error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting NetSuite pull since {Since} (Mock Mode: {MockMode})", since, _isMockMode);

        if (_isMockMode)
        {
            // Generate mock data
            await foreach (var mockEvent in GenerateMockDataAsync(since, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return mockEvent;
            }
        }
        else
        {
            // Pull from real NetSuite API
            await foreach (var customer in PullCustomersAsync(since, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return customer;
            }

            await foreach (var item in PullItemsAsync(since, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return item;
            }

            await foreach (var transaction in PullTransactionsAsync(since, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return transaction;
            }
        }
    }

    public ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync()
    {
        var config = new ConnectorConfiguration(
            Id,
            "netsuite",
            new Dictionary<string, object>
            {
                { "AccountId", _config.AccountId },
                { "AccessToken", "***" }, // Mask sensitive data
                { "ApiVersion", _config.ApiVersion },
                { "IsMockMode", _config.IsMockMode }
            }
        );
        
        return ValueTask.FromResult(config);
    }

    private async IAsyncEnumerable<UpsertEvent> GenerateMockDataAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating mock NetSuite data");

        // Mock customers
        var mockCustomers = new[]
        {
            new { id = 1001, name = "Acme Manufacturing", email = "orders@acme-mfg.com", type = "customer" },
            new { id = 1002, name = "Global Tech Solutions", email = "billing@globaltech.com", type = "customer" },
            new { id = 1003, name = "Precision Industries", email = "sales@precision.com", type = "customer" }
        };

        foreach (var customer in mockCustomers)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var customerData = new Dictionary<string, object>
            {
                { "id", customer.id },
                { "name", customer.name },
                { "email", customer.email },
                { "type", customer.type },
                { "lastModified", DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)) }
            };

            yield return new UpsertEvent(
                "Customer",
                customer.id.ToString(),
                JsonSerializer.SerializeToElement(customerData),
                DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
            );

            await Task.Delay(50, cancellationToken); // Simulate API delay
        }

        // Mock items
        var mockItems = new[]
        {
            new { id = 2001, name = "Precision Gear Assembly", sku = "PGA-001", price = 1250.00m },
            new { id = 2002, name = "Industrial Bearing Set", sku = "IBS-002", price = 875.50m },
            new { id = 2003, name = "Custom Machined Part", sku = "CMP-003", price = 2100.00m }
        };

        foreach (var item in mockItems)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var itemData = new Dictionary<string, object>
            {
                { "id", item.id },
                { "name", item.name },
                { "sku", item.sku },
                { "price", item.price },
                { "type", "inventoryItem" },
                { "lastModified", DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 15)) }
            };

            yield return new UpsertEvent(
                "Item",
                item.id.ToString(),
                JsonSerializer.SerializeToElement(itemData),
                DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 15))
            );

            await Task.Delay(50, cancellationToken);
        }

        // Mock transactions
        var mockTransactions = new[]
        {
            new { id = 3001, customerId = 1001, amount = 15750.00m, type = "salesOrder" },
            new { id = 3002, customerId = 1002, amount = 8250.75m, type = "invoice" },
            new { id = 3003, customerId = 1003, amount = 22100.50m, type = "salesOrder" }
        };

        foreach (var transaction in mockTransactions)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var transactionData = new Dictionary<string, object>
            {
                { "id", transaction.id },
                { "customerId", transaction.customerId },
                { "amount", transaction.amount },
                { "type", transaction.type },
                { "status", "pending" },
                { "createdDate", DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 7)) },
                { "lastModified", DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 5)) }
            };

            yield return new UpsertEvent(
                "Transaction",
                transaction.id.ToString(),
                JsonSerializer.SerializeToElement(transactionData),
                DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 5))
            );

            await Task.Delay(75, cancellationToken);
        }
    }

    private async IAsyncEnumerable<UpsertEvent> PullCustomersAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = $"SELECT id, entityid, companyname, email, datecreated, lastmodified FROM customer WHERE lastmodified >= '{since:yyyy-MM-dd}'";
        var request = new RestRequest("/services/rest/query/v1/suiteql", Method.Get);
        request.AddHeader("Authorization", $"Bearer {_config.AccessToken}");
        request.AddParameter("q", query);

        RestResponse response;
        try
        {
            response = await _client!.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling NetSuite customers");
            yield break;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogWarning("Failed to fetch NetSuite customers: {StatusCode}", response.StatusCode);
            yield break;
        }

        var jsonDoc = JsonDocument.Parse(response.Content!);
        var items = jsonDoc.RootElement.GetProperty("items");

        foreach (var customerElement in items.EnumerateArray())
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var customerData = new Dictionary<string, object>
            {
                { "id", customerElement.GetProperty("id").GetString() ?? "" },
                { "entityid", customerElement.GetProperty("entityid").GetString() ?? "" },
                { "companyname", customerElement.GetProperty("companyname").GetString() ?? "" },
                { "email", customerElement.GetProperty("email").GetString() ?? "" },
                { "datecreated", customerElement.GetProperty("datecreated").GetString() ?? "" },
                { "lastmodified", customerElement.GetProperty("lastmodified").GetString() ?? "" }
            };

            yield return new UpsertEvent(
                "Customer",
                customerElement.GetProperty("id").GetString() ?? "",
                JsonSerializer.SerializeToElement(customerData),
                DateTime.Parse(customerElement.GetProperty("lastmodified").GetString()!)
            );
        }
    }

    private async IAsyncEnumerable<UpsertEvent> PullItemsAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = $"SELECT id, itemid, displayname, baseprice, lastmodified FROM item WHERE lastmodified >= '{since:yyyy-MM-dd}'";
        var request = new RestRequest("/services/rest/query/v1/suiteql", Method.Get);
        request.AddHeader("Authorization", $"Bearer {_config.AccessToken}");
        request.AddParameter("q", query);

        RestResponse response;
        try
        {
            response = await _client!.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling NetSuite items");
            yield break;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogWarning("Failed to fetch NetSuite items: {StatusCode}", response.StatusCode);
            yield break;
        }

        var jsonDoc = JsonDocument.Parse(response.Content!);
        var items = jsonDoc.RootElement.GetProperty("items");

        foreach (var itemElement in items.EnumerateArray())
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var itemData = new Dictionary<string, object>
            {
                { "id", itemElement.GetProperty("id").GetString() ?? "" },
                { "itemid", itemElement.GetProperty("itemid").GetString() ?? "" },
                { "displayname", itemElement.GetProperty("displayname").GetString() ?? "" },
                { "baseprice", itemElement.GetProperty("baseprice").GetString() ?? "0" },
                { "lastmodified", itemElement.GetProperty("lastmodified").GetString() ?? "" }
            };

            yield return new UpsertEvent(
                "Item",
                itemElement.GetProperty("id").GetString() ?? "",
                JsonSerializer.SerializeToElement(itemData),
                DateTime.Parse(itemElement.GetProperty("lastmodified").GetString()!)
            );
        }
    }

    private async IAsyncEnumerable<UpsertEvent> PullTransactionsAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = $"SELECT id, tranid, entity, total, status, trandate, lastmodified FROM transaction WHERE lastmodified >= '{since:yyyy-MM-dd}'";
        var request = new RestRequest("/services/rest/query/v1/suiteql", Method.Get);
        request.AddHeader("Authorization", $"Bearer {_config.AccessToken}");
        request.AddParameter("q", query);

        RestResponse response;
        try
        {
            response = await _client!.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling NetSuite transactions");
            yield break;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogWarning("Failed to fetch NetSuite transactions: {StatusCode}", response.StatusCode);
            yield break;
        }

        var jsonDoc = JsonDocument.Parse(response.Content!);
        var items = jsonDoc.RootElement.GetProperty("items");

        foreach (var transactionElement in items.EnumerateArray())
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var transactionData = new Dictionary<string, object>
            {
                { "id", transactionElement.GetProperty("id").GetString() ?? "" },
                { "tranid", transactionElement.GetProperty("tranid").GetString() ?? "" },
                { "entity", transactionElement.GetProperty("entity").GetString() ?? "" },
                { "total", transactionElement.GetProperty("total").GetString() ?? "0" },
                { "status", transactionElement.GetProperty("status").GetString() ?? "" },
                { "trandate", transactionElement.GetProperty("trandate").GetString() ?? "" },
                { "lastmodified", transactionElement.GetProperty("lastmodified").GetString() ?? "" }
            };

            yield return new UpsertEvent(
                "Transaction",
                transactionElement.GetProperty("id").GetString() ?? "",
                JsonSerializer.SerializeToElement(transactionData),
                DateTime.Parse(transactionElement.GetProperty("lastmodified").GetString()!)
            );
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

public class NetSuiteConfig
{
    public string AccountId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string ApiVersion { get; set; } = "v1";
    public bool IsMockMode { get; set; } = true;
}