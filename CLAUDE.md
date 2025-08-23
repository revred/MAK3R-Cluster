# CLAUDE.md

## MAK3R-Cluster Project Context

### Project Overview
**MAK3R-Cluster** is a production-ready Blazor WebAssembly PWA demonstrating a manufacturing digital twin platform with MCP (Model Context Protocol) like connector architecture. This is an investor PoC that must be productionizable.

### Current Status (✅ COMPLETED)
- **Architecture**: Complete MCP-like connector system implemented
- **Frontend**: Blazor WebAssembly PWA with Palantir-inspired dark theme
- **Backend**: ASP.NET Core 8 API with minimal APIs pattern
- **Authentication**: ASP.NET Core Identity + JWT tokens
- **Database**: Entity Framework Core with SQLite
- **Connectors**: Shopify, NetSuite (mock), OPC UA with factory pattern
- **Documentation**: Comprehensive README and product documentation
- **Version Control**: Committed to GitHub at https://github.com/revred/MAK3R-Cluster

### Key Architectural Decisions Made

#### 1. MCP-like Connector Architecture
- **Location**: Moved connectors from `libs/` to `mcps/` folder for clarity
- **Pattern**: Factory pattern with `IConnectorRegistry` and `IConnectorFactory`
- **Extensibility**: String-based connector types instead of enums for dynamic registration
- **API**: RESTful connector management endpoints (`/api/connectors/*`)

#### 2. Project Structure
```
MAK3R-Cluster/
├── apps/MAK3R.PWA/                    # Blazor WebAssembly PWA
├── services/
│   ├── MAK3R.Api/                     # ASP.NET Core API
│   └── MAK3R.Simulators/              # OPC UA simulators  
├── libs/                              # Core business libraries
│   ├── MAK3R.Core/                    # Result patterns, primitives
│   ├── MAK3R.Data/                    # EF Core, DbContext
│   ├── MAK3R.DigitalTwin/            # Twin orchestrator
│   ├── MAK3R.Shared/                 # DTOs, contracts
│   └── MAK3R.UI/                     # Shared Blazor components
├── mcps/                             # MCP Server Components
│   ├── MAK3R.Connectors.Abstractions/ # Core interfaces
│   ├── MAK3R.Connectors/             # Hub implementation
│   ├── MAK3R.Connectors.Shopify/    # Shopify connector
│   ├── MAK3R.Connectors.NetSuite/   # NetSuite connector (mock mode)
│   └── MAK3R.Connectors.OPCUA/      # OPC UA connector
└── tests/                            # Test projects
```

#### 3. Key Technologies
- **.NET 8 LTS**: Core framework
- **Blazor WebAssembly**: Client-side SPA with PWA capabilities
- **ASP.NET Core**: Backend API with minimal APIs
- **Entity Framework Core**: Data access with SQLite
- **ASP.NET Core Identity**: Authentication system
- **Serilog**: Structured logging
- **RestSharp**: HTTP client for connectors
- **OPC Foundation .NET Standard**: OPC UA connectivity

#### 4. Connector Interface (Current Implementation)
```csharp
public interface IConnector
{
    string Id { get; }
    string Name { get; }
    string Type { get; }  // String, not enum for extensibility
    ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct);
    IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, CancellationToken ct);
    ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync();
}

public record UpsertEvent(string EntityType, string ExternalId, JsonElement Payload, DateTime Timestamp);
public record ConnectorCheck(bool IsHealthy, string? Message, Dictionary<string, object>? Metadata = null);
```

### Current Running Applications

#### API Server
- **URL**: http://localhost:5137
- **Health**: GET /api/health
- **Swagger**: Available in development
- **Database**: SQLite with seed data for "Contoso Gears"
- **Logging**: Console + file logging to `logs/` folder

#### PWA Client  
- **URLs**: 
  - https://localhost:7228 (HTTPS)
  - http://localhost:5223 (HTTP)
- **Features**: Installable PWA, offline capable, dark theme
- **Authentication**: JWT token based

### API Endpoints (Current)

#### Health & Core
- `GET /api/health` - System health check
- `GET /api/demo/contoso-gears` - Demo data endpoint

#### Authentication
- `POST /api/auth/register` - User registration  
- `POST /api/auth/login` - User login

#### Digital Twin
- `POST /api/onboard` - Create digital twin from wizard
- `GET /api/twin/{companyId}` - Get company twin
- `GET /api/twin/{companyId}/validate` - Validate twin

#### Connector Management (MCP-like)
- `GET /api/connectors/types` - Discover connector types
- `GET /api/connectors/instances` - List active connectors
- `POST /api/connectors/` - Create connector instance
- `GET /api/connectors/instances/{id}/health` - Connector health
- `POST /api/connectors/instances/{id}/sync` - Trigger sync
- `GET /api/connectors/health` - All connector health

#### Data Access (Paginated)
- `GET /api/companies?page=0&size=20` - Companies
- `GET /api/companies/{id}/sites` - Company sites
- `GET /api/companies/{id}/machines` - Company machines  
- `GET /api/companies/{id}/products` - Company products

### Completed Features

#### ✅ Core Infrastructure
- Solution structure with proper dependency management
- Global NuGet package management via Directory.Packages.props
- EditorConfig and code quality settings
- Git repository with proper .gitignore

#### ✅ Authentication & Security
- ASP.NET Core Identity integration
- JWT token authentication for SPA
- CORS configuration for PWA
- Development user secrets management

#### ✅ Digital Twin System
- Progressive onboarding wizard (UI components ready)
- Twin orchestrator for data mapping
- Entity models: Company, Site, Machine, Product
- External reference tracking for connector data lineage

#### ✅ MCP Connector Architecture
- Dynamic connector type registration
- Factory pattern for connector instantiation  
- String-based typing for extensibility
- Health monitoring and sync capabilities
- Server-side proxy pattern for external API calls

#### ✅ Specific Connectors
- **Shopify**: Products import via Admin REST API
- **NetSuite**: Mock mode with switchable real integration
- **OPC UA**: Machine data connectivity (simulator ready)

#### ✅ UI/UX System
- Palantir-inspired dark theme with CSS custom properties
- Responsive grid layout and components
- Progressive loading with skeleton screens
- PWA manifest and service worker
- Installable on mobile and desktop

#### ✅ Data Layer
- Entity Framework Core with SQLite
- Automatic database creation and seeding
- Migrations support for schema evolution
- Result<T> pattern for error handling

### Pending Implementation (Next Phase)

#### 🔄 Real-time Features
- SignalR hub for machine telemetry
- Live machine wall UI with charts
- Real-time connector status updates

#### 🔄 Advanced Features  
- Anomaly detection workbench with rule engine
- File ingestion with schema inference
- Shopfront builder for product catalogs
- Advanced twin validation and gap detection

#### 🔄 Testing Infrastructure
- Unit tests with xUnit and FluentAssertions
- Integration tests with WireMock for connector stubs
- E2E tests with Playwright
- Test coverage reporting

#### 🔄 Production Readiness
- Health checks and metrics
- Containerization with Docker
- CI/CD pipeline with GitHub Actions
- Production database configuration
- Security hardening and secrets management

### Demo Data Available
- **Company**: Contoso Gears Pvt Ltd
- **Products**: 8 sample products with pricing
- **Machines**: 3 sample machines with OPC UA nodes
- **Sites**: 2 sample manufacturing sites

### Development Commands

#### Start Applications
```bash
# Terminal 1: Start API
cd C:\code\MAK3R-Cluster
dotnet run --project services/MAK3R.Api

# Terminal 2: Start PWA  
cd C:\code\MAK3R-Cluster
dotnet run --project apps/MAK3R.PWA
```

#### Build and Test
```bash
cd C:\code\MAK3R-Cluster
dotnet restore
dotnet build
dotnet test
```

#### Database Operations
- Database auto-creates on first run
- SQLite file: `services/MAK3R.Api/mak3r.db`
- Seed data runs automatically

### Key Design Patterns Used

#### Result<T> Pattern
```csharp
public static class Result<T>
{
    public static Result<T> Success(T value) => new(value, true, null);
    public static Result<T> Failure(string error) => new(default, false, error);
}
```

#### Factory Pattern for Connectors
```csharp
public interface IConnectorFactory<T> where T : class, IConnector
{
    ValueTask<Result<T>> CreateAsync(ConnectorConfiguration config, CancellationToken ct = default);
    ValueTask<Result<ConnectorConfigurationSchema>> GetConfigurationSchemaAsync();
}
```

### Styling System (Palantir-inspired)

#### CSS Variables
```css
:root {
  --bg: #0B0F14;           /* Ink black background */
  --panel: #121821;        /* Panel background */  
  --text: #E6EDF3;         /* Primary text */
  --muted: #9FB0C1;        /* Secondary text */
  --grid: #1E2631;         /* Grid lines */
  --accent: #3DA8FF;       /* Electric blue accent */
  --accentHover: #5CC1FF;  /* Hover state */
  --success: #2BD99F;      /* Success green */
  --warn: #FFC857;         /* Warning amber */
  --danger: #FF6B6B;       /* Danger red */
  --radius: 16px;          /* Border radius */
  --shadow: 0 10px 30px rgba(0,0,0,0.35); /* Box shadow */
  --spacing: 12px;         /* Base spacing unit */
}
```

### Important Notes for Continuation

1. **Both Applications Must Be Running**: API on 5137, PWA on 7228/5223
2. **No External Dependencies**: Everything works offline with mock data
3. **Connector Registration**: Currently empty - connectors need to be registered via DI
4. **Database State**: Persists between sessions in SQLite file
5. **Logging**: Check `services/MAK3R.Api/logs/` for detailed logs
6. **Git State**: All code committed to GitHub, only logs remain uncommitted

### Troubleshooting

#### Port Conflicts
If ports are in use, kill processes:
```bash
# Find process using port
netstat -ano | findstr :5137
# Kill by PID (Windows)
taskkill /F /PID <process_id>
```

#### Build Issues
- Ensure .NET 8 SDK is installed
- Run `dotnet restore` if packages are missing
- Check NuGet package versions in Directory.Packages.props

#### Database Issues
- Delete `mak3r.db` file to reset database
- Database recreates automatically with seed data
- Check EF Core logs for migration issues

### Security Considerations
- JWT keys are development-only defaults
- CORS is configured for localhost development
- User secrets used for sensitive configuration
- Production deployment requires proper key management

This context should provide everything needed to continue development seamlessly.