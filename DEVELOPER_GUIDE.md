# MAK3R-Cluster Developer Guide

## Overview
MAK3R-Cluster is a manufacturing digital twin platform built with Blazor WebAssembly PWA and ASP.NET Core 8. This guide covers the development workflow, architecture patterns, and best practices.

## Development Environment Setup

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# extension
- Node.js 20+ (for Playwright tests)
- Git
- Optional: Docker Desktop (for containerized databases)

### Getting Started
```bash
# Clone the repository
git clone <repository-url>
cd MAK3R-Cluster

# Restore packages and build
dotnet restore
dotnet build

# Run tests to verify setup
dotnet test
```

## Architecture Deep Dive

### Progressive Digital Twin Concept
MAK3R implements a progressive digital twin approach:

1. **Digital Spine**: Basic data model and key process mapping (PoC phase)
2. **Digital Skeleton**: Connected systems with basic data flow (adoption phase)
3. **Digital Twin**: Full real-time representation with analytics (maturity phase)

### Component Architecture

#### Frontend (apps/MAK3R.PWA)
- **Landing Page**: Marketing site with progressive messaging
- **Machine Wall**: Real-time machine monitoring dashboard
- **Shopfront Builder**: Product content management system
- **File Ingestion**: Intelligent data import with schema inference
- **Anomaly Workbench**: Data quality and anomaly detection

#### Backend Services
- **MAK3R.Api**: Main API server with authentication and business logic
- **MAK3R.Simulators**: OPC UA simulators for demo data

#### Core Libraries
- **MAK3R.Core**: Domain primitives, Result patterns, Guard clauses
- **MAK3R.Data**: EF Core context, migrations, and repositories
- **MAK3R.DigitalTwin**: Twin orchestration and lifecycle management
- **MAK3R.Shared**: DTOs and contracts shared between frontend/backend
- **MAK3R.UI**: Reusable Blazor components with CSS isolation

#### Connector System (mcps/)
MCP-like architecture for external system integration:
- **Abstractions**: Core contracts and interfaces
- **Registry**: Dynamic connector discovery and management
- **Implementations**: Shopify, NetSuite, and OPC-UA connectors

### Data Flow Patterns

#### File Ingestion Workflow
```
Upload File → Analyze Structure → Infer Schema → Map Fields → Import Data → Update Digital Twin
```

#### Digital Twin Lifecycle
```
None → Skeleton → Partial → Complete → Enhanced
```

## Development Workflows

### Adding New Components

#### 1. Blazor Component
```csharp
// Components/MyComponent.razor
@using MAK3R.PWA.Models
@using MAK3R.PWA.Services

<div class="my-component">
    <!-- Component markup -->
</div>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Inject] public IMyService MyService { get; set; } = default!;
    
    protected override async Task OnInitializedAsync()
    {
        // Component initialization
    }
}
```

#### 2. CSS Isolation
```css
/* Components/MyComponent.razor.css */
.my-component {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}
```

### Adding New Services

#### 1. Interface Definition
```csharp
// Services/IMyService.cs
public interface IMyService
{
    Task<Result<MyModel>> GetDataAsync(string id);
    Task<Result> SaveDataAsync(MyModel model);
}
```

#### 2. Implementation
```csharp
// Services/MyService.cs
public class MyService : IMyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MyService> _logger;
    
    public MyService(HttpClient httpClient, ILogger<MyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<Result<MyModel>> GetDataAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<MyModel>($"/api/my-data/{id}");
            return Result<MyModel>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data for {Id}", id);
            return Result<MyModel>.Failure($"Failed to get data: {ex.Message}");
        }
    }
}
```

#### 3. Registration
```csharp
// Program.cs
builder.Services.AddScoped<IMyService, MyService>();
```

### Testing Patterns

#### Unit Testing Services
```csharp
public class MyServiceTests
{
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<ILogger<MyService>> _loggerMock;
    private readonly MyService _service;
    
    public MyServiceTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _loggerMock = new Mock<ILogger<MyService>>();
        _service = new MyService(_httpClientMock.Object, _loggerMock.Object);
    }
    
    [Fact]
    public async Task GetDataAsync_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var expectedModel = new MyModel { Id = "test", Name = "Test Item" };
        // Mock setup...
        
        // Act
        var result = await _service.GetDataAsync("test");
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedModel);
    }
}
```

#### Integration Testing
```csharp
public class MyApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public MyApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task GetMyData_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/my-data/test");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Code Standards

### Naming Conventions
- **Classes**: PascalCase (e.g., `ProductService`)
- **Methods**: PascalCase (e.g., `GetProductAsync`)
- **Properties**: PascalCase (e.g., `ProductName`)
- **Fields**: camelCase with underscore prefix (e.g., `_httpClient`)
- **Parameters**: camelCase (e.g., `productId`)
- **Constants**: PascalCase (e.g., `MaxRetryAttempts`)

### Async Patterns
- Always use `async`/`await` for I/O operations
- Suffix async methods with `Async`
- Use `ConfigureAwait(false)` in library code
- Return `Task<Result<T>>` instead of throwing exceptions

### Error Handling
Use the Result pattern instead of exceptions for business logic:
```csharp
// ✅ Good
public async Task<Result<Product>> CreateProductAsync(Product product)
{
    if (string.IsNullOrEmpty(product.Name))
        return Result<Product>.Failure("Product name is required");
        
    // Business logic...
    return Result<Product>.Success(product);
}

// ❌ Avoid
public async Task<Product> CreateProductAsync(Product product)
{
    if (string.IsNullOrEmpty(product.Name))
        throw new ArgumentException("Product name is required");
        
    // Business logic...
    return product;
}
```

### Logging
Use structured logging with Serilog:
```csharp
_logger.LogInformation("Processing product import for {ProductCount} products", products.Count);
_logger.LogWarning("Failed to process product {ProductId}: {ErrorMessage}", productId, error);
_logger.LogError(ex, "Unexpected error during product processing");
```

## Build and Deployment

### Local Development
```bash
# Start API server
dotnet run --project services/MAK3R.Api

# Start PWA (in new terminal)
dotnet run --project apps/MAK3R.PWA

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MAK3R.UnitTests
```

### Production Build
```bash
# Build everything in Release mode
dotnet build -c Release

# Publish PWA for deployment
dotnet publish apps/MAK3R.PWA -c Release -o ./publish/pwa

# Publish API for deployment
dotnet publish services/MAK3R.Api -c Release -o ./publish/api
```

### Docker Support (Future)
```dockerfile
# Example Dockerfile for API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["services/MAK3R.Api/MAK3R.Api.csproj", "services/MAK3R.Api/"]
RUN dotnet restore "services/MAK3R.Api/MAK3R.Api.csproj"
COPY . .
WORKDIR "/src/services/MAK3R.Api"
RUN dotnet build "MAK3R.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MAK3R.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MAK3R.Api.dll"]
```

## Debugging Tips

### Blazor WebAssembly Debugging
- Enable browser developer tools
- Use `Console.WriteLine()` or `ILogger` for debugging
- Leverage browser's Network tab for API call inspection
- Use Blazor Server for complex debugging scenarios

### API Debugging
- Use Visual Studio debugger or VS Code
- Check logs in `logs/mak3r-api-{date}.log`
- Use Swagger UI at `/swagger` for API testing
- Leverage health check endpoint at `/api/health`

### Common Issues

#### Build Errors
- **Central Package Management**: Remove version from PackageReference, add to Directory.Packages.props
- **Missing Dependencies**: Check for proper service registration in Program.cs
- **CSS Isolation**: Ensure .razor.css files are properly named and structured

#### Runtime Errors
- **SignalR Connection**: Verify hub registration and client connection setup
- **Authentication**: Check JWT token validity and proper header inclusion
- **CORS Issues**: Verify allowed origins in API configuration

## Performance Guidelines

### Frontend Optimization
- Use CSS isolation to prevent style conflicts
- Implement lazy loading for large components
- Optimize images and use proper formats (WebP, AVIF)
- Leverage browser caching with proper cache headers

### Backend Optimization
- Use async/await consistently
- Implement proper database indexing
- Use pagination for large data sets
- Leverage EF Core query optimization techniques

### Database Performance
- Use appropriate indexes on frequently queried columns
- Implement proper database connection pooling
- Consider using compiled queries for hot paths
- Monitor query performance with EF Core logging

## Security Considerations

### Authentication & Authorization
- Use JWT tokens with proper expiration
- Implement proper CORS configuration
- Validate all user inputs
- Use HTTPS in production

### Data Protection
- Never log sensitive information
- Use proper encryption for sensitive data at rest
- Implement proper audit logging
- Follow GDPR/privacy guidelines

## Contributing Guidelines

### Pull Request Process
1. Create feature branch from `develop`
2. Implement changes with tests
3. Run full test suite
4. Update documentation if needed
5. Submit PR with proper description

### Code Review Checklist
- [ ] Code follows established patterns
- [ ] Tests cover new functionality
- [ ] No security vulnerabilities introduced
- [ ] Documentation updated
- [ ] Performance impact considered

## Resources

### Documentation Links
- [ASP.NET Core 8 Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Blazor WebAssembly Guide](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [xUnit Testing Framework](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)

### MAK3R-Specific Resources
- Architecture Decision Records (ADRs) - `docs/adr/`
- API Documentation - `/swagger` endpoint
- Component Library - `libs/MAK3R.UI/`
- Connector Development - `mcps/README.md`

---

*This guide is a living document. Please keep it updated as the codebase evolves.*