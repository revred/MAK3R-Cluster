# MAK3R Cluster - Manufacturing Digital Twin Platform

A production-ready Blazor WebAssembly PWA demonstrating progressive digital twin onboarding, MCP-like connector architecture, and real-time machine telemetry for manufacturing companies.

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+ (for Playwright, if running E2E tests)

### Running the Application

1. **Clone and restore dependencies:**
   ```bash
   git clone <repository-url>
   cd MAK3R-Cluster
   dotnet restore
   dotnet build
   ```

2. **Start the API server:**
   ```bash
   dotnet run --project services/MAK3R.Api
   ```
   API will be available at: `http://localhost:5137`

3. **Start the PWA client (in a new terminal):**
   ```bash
   dotnet run --project apps/MAK3R.PWA
   ```
   PWA will be available at: 
   - `https://localhost:7228` (HTTPS)
   - `http://localhost:5223` (HTTP)

### Health Check
Verify the API is running:
```bash
curl http://localhost:5137/api/health
```

## 🏗️ Architecture

### Solution Structure
```
MAK3R-Cluster/
├── apps/
│   └── MAK3R.PWA/                    # Blazor WebAssembly PWA
├── services/
│   ├── MAK3R.Api/                    # ASP.NET Core API with JWT auth
│   └── MAK3R.Simulators/             # OPC UA simulators
├── libs/                             # Core business libraries
│   ├── MAK3R.Core/                   # Domain primitives, Result patterns
│   ├── MAK3R.Data/                   # EF Core, DbContext, migrations
│   ├── MAK3R.DigitalTwin/           # Twin orchestrator and models
│   ├── MAK3R.Shared/                # DTOs and contracts
│   └── MAK3R.UI/                    # Shared Blazor components
├── mcps/                            # MCP-like Connector Servers
│   ├── MAK3R.Connectors.Abstractions/ # Core connector contracts
│   ├── MAK3R.Connectors/            # Hub and registry implementation
│   ├── MAK3R.Connectors.Shopify/   # Shopify product connector
│   ├── MAK3R.Connectors.NetSuite/  # NetSuite ERP connector
│   └── MAK3R.Connectors.OPCUA/     # OPC UA machine connector
└── tests/                           # Test projects
    ├── MAK3R.UnitTests/
    ├── MAK3R.IntegrationTests/
    └── MAK3R.PlaywrightTests/
```

### Key Features

#### 🔌 MCP-like Connector Architecture
- **Dynamic Discovery**: Connectors register without recompilation
- **Factory Pattern**: Schema-driven connector instantiation
- **String-based Types**: Extensible connector type system
- **RESTful Management**: Full connector lifecycle via API
- **Loose Coupling**: All connectors implement `IConnector` abstraction

#### 🏭 Digital Twin System
- **Progressive Onboarding**: Wizard-driven twin creation
- **Entity Management**: Companies, Sites, Machines, Products
- **External References**: Track data lineage from connectors
- **Validation Engine**: Detect and report data anomalies

#### 📱 PWA Capabilities
- **Installable**: Add to home screen on mobile/desktop
- **Offline Support**: Service worker with background sync
- **Progressive Loading**: Skeleton loaders and pagination
- **Palantir-inspired Theme**: Dark, professional interface

## 🔗 API Endpoints

### Health & Status
- `GET /api/health` - System health check

### Authentication
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User authentication

### Digital Twin
- `POST /api/onboard` - Create digital twin from wizard
- `GET /api/twin/{companyId}` - Get company digital twin
- `GET /api/twin/{companyId}/validate` - Validate twin data

### Connector Management
- `GET /api/connectors/types` - List available connector types
- `GET /api/connectors/instances` - List active connector instances
- `POST /api/connectors/` - Create new connector instance
- `GET /api/connectors/health` - Check all connector health
- `POST /api/connectors/instances/{id}/sync` - Trigger connector sync

### Data Access
- `GET /api/companies` - Paginated company list
- `GET /api/companies/{id}/sites` - Company sites
- `GET /api/companies/{id}/machines` - Company machines
- `GET /api/companies/{id}/products` - Company products

## 🛠️ Technology Stack

### Frontend
- **Blazor WebAssembly**: .NET 8 running in browser
- **PWA**: Service worker, offline support, installable
- **Authentication**: JWT bearer tokens
- **Styling**: CSS custom properties, Palantir-inspired theme
- **State Management**: Blazor component state + local storage

### Backend
- **ASP.NET Core**: Minimal APIs with OpenAPI
- **Authentication**: ASP.NET Core Identity + JWT
- **Database**: Entity Framework Core with SQLite
- **Logging**: Serilog with structured logging
- **Real-time**: SignalR (planned for machine data)

### Data Layer
- **ORM**: Entity Framework Core
- **Database**: SQLite (development), SQL Server ready
- **Migrations**: Code-first with automatic seeding
- **Patterns**: Repository pattern with Result<T>

## 🔧 Development

### Configuration
The application uses ASP.NET Core configuration with the following sources:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- User secrets - Sensitive development data
- Environment variables - Production configuration

### Logging
Structured logging with Serilog:
- Console output for development
- File logging: `logs/mak3r-api-{date}.log`
- Correlation IDs for request tracking

### Database
SQLite database with Entity Framework Core:
- Automatic database creation on startup
- Seed data for demo company "Contoso Gears"
- Migrations support for schema evolution

## 🧪 Testing Strategy

### Unit Tests
- **Framework**: xUnit with FluentAssertions
- **Coverage**: Domain logic and services
- **Mocking**: Mock connector implementations

### Integration Tests
- **API Testing**: Full HTTP pipeline tests
- **Database**: In-memory SQLite for isolation
- **Connectors**: WireMock for external API stubs

### E2E Tests (Planned)
- **Framework**: Playwright
- **Scenarios**: Onboarding flow, connector setup, twin management

## 🚢 Deployment

### Development
```bash
# Start both applications
dotnet run --project services/MAK3R.Api &
dotnet run --project apps/MAK3R.PWA
```

### Production Considerations
- Configure HTTPS certificates
- Set up proper CORS origins
- Use SQL Server or PostgreSQL
- Configure JWT signing keys
- Set up proper logging infrastructure
- Enable health checks and monitoring

## 📈 Roadmap

### Current Status (v0.1)
- ✅ Basic application shell and architecture
- ✅ MCP-like connector framework
- ✅ Digital twin data models
- ✅ PWA foundations
- ✅ JWT authentication
- ✅ Connector management APIs

### Next Phase (v0.2)
- 🔄 SignalR integration for real-time data
- 🔄 Machine wall UI with live telemetry
- 🔄 Anomaly detection workbench
- 🔄 File ingestion system
- 🔄 Comprehensive test suite

### Future (v1.0)
- Shopfront builder
- Advanced analytics
- Multi-tenant support
- Advanced connector ecosystem
- Production hardening

## 🤝 Contributing

This is currently a proprietary project for investor demonstration. For production use or licensing inquiries, please contact MAK3R.ai.

## 📄 License

Proprietary - See LICENSE.md for details.

---

*MAK3R.ai - Unifying Manufacturing Data Through Digital Twins*