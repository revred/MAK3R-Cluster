# CLAUDE.md

> Purpose: Hands-on build instructions for the coding assistant. Follow these steps precisely to scaffold, code, and style the MAK3R investor PoC as a production-ready foundation that can evolve to full product in 3 months.

## Guardrails & Principles
- **Stack**: .NET 8 LTS (Blazor Web App + WASM for PWA), ASP.NET Core Hosted API, optional MAUI Blazor Hybrid shell. Keep code .NET 8-compatible; enable upgrade path to .NET 9 later.
- **Architecture**: Clean Architecture + Ports/Adapters + Connector SDK. **Zero throwaway**; PoC code must be productionizable.
- **PWA First**: Offline capture, service worker, app manifest, installable. IndexedDB cache for client data; API sync queue with background retry.
- **Styling**: Palantir-inspired (dark slate, crisp grids, high-contrast typography). Use open fonts: **Inter** (primary), **IBM Plex Mono** (code). Avoid proprietary assets.
- **Security**: ASP.NET Core Identity + JWT bearer for SPA; secret management via `dotnet user-secrets` in dev. HTTPS only. CORS locked to app domain.
- **DX**: Fast restore/build, solution filters, EditorConfig, analyzers, StyleCop, `dotnet format`, nullable enabled.
- **Tests**: xUnit for unit tests, Verify for snapshots, Playwright for E2E, WireMock for connector stubs.

## Solution Layout (create exactly):
```
MAK3R-Cluster.sln
apps/
  MAK3R.PWA/                # Blazor Web App (WASM PWA, ASP.NET Core hosted)
  MAK3R.Hybrid/             # MAUI Blazor Hybrid shell (optional)
services/
  MAK3R.Api/                # Minimal APIs + SignalR + Connector host
  MAK3R.ConnectorHost/      # Worker service for connectors (optional in PoC)
  MAK3R.Simulators/         # OPC UA/MQTT simulators for demos
libs/
  MAK3R.Core/               # Domain primitives, Result, Guard, Errors
  MAK3R.Identity/           # Identity abstractions
  MAK3R.Data/               # EF Core, DbContexts, migrations
  MAK3R.DigitalTwin/        # Twin model + builder/orchestrator
  MAK3R.Anomalies/          # Rule engine + detectors (NRules or custom)
  MAK3R.Content/            # Product content & site builder
  MAK3R.Ingestion/          # File dropzone → schema inference → staging
  MAK3R.Messaging/          # Abstraction for queues/bus (in-proc in PoC)
  MAK3R.Shared/             # DTOs, contracts, web models
  MAK3R.UI/                 # Shared Razor components, theme, layout
mcps/
  MAK3R.Connectors.Abstractions/ # IConnector, schemas, contracts
  MAK3R.Connectors/         # Connector hub and registry implementation
  MAK3R.Connectors.Shopify/ # Products import (Admin REST via server proxy)
  MAK3R.Connectors.NetSuite/# ERP PoC (items/customers), mock mode supported
  MAK3R.Connectors.OPCUA/   # Machine data via OPCFoundation .NET Standard
tests/
  MAK3R.UnitTests/
  MAK3R.IntegrationTests/
  MAK3R.PlaywrightTests/

.build/
  pipelines/ (GitHub Actions)
.editorconfig
Directory.Packages.props
global.json
```

## Commands to Scaffold
1) **Create solution**
```
dotnet new sln -n MAK3R-Cluster
mkdir apps services libs tests .build/pipelines
```
2) **Blazor PWA (hosted)**
```
cd apps
# Use hosted WASM for true PWA + API in one template
 dotnet new blazorwasm -n MAK3R.PWA -ho -au Individual -o MAK3R.PWA --https-redir true
cd ..
```
3) **API split project** (adjust template’s Server to `services/MAK3R.Api`)
- Move `Server` project from hosted template to `services/MAK3R.Api` and rename accordingly.
- Update `MAK3R.PWA` Client to point to `/` API base.

4) **Class libraries**
```
for %p in (Core Identity Data DigitalTwin Anomalies Content Ingestion Messaging Shared UI) do dotnet new classlib -n MAK3R.%p -o libs/MAK3R.%p
# MCP Server components
mkdir mcps
for %p in (Connectors.Abstractions Connectors Connectors.Shopify Connectors.NetSuite Connectors.OPCUA) do dotnet new classlib -n MAK3R.%p -o mcps/MAK3R.%p
```
5) **Tests**
```
dotnet new xunit -n MAK3R.UnitTests -o tests/MAK3R.UnitTests
 dotnet new xunit -n MAK3R.IntegrationTests -o tests/MAK3R.IntegrationTests
 dotnet new mstest -n MAK3R.PlaywrightTests -o tests/MAK3R.PlaywrightTests
```
6) **Add refs** (sample; wire all as needed)
```
dotnet sln add apps/MAK3R.PWA/MAK3R.PWA.Client.csproj
# add API and libs, tests similarly
```

## NuGet Packages (add to respective projects)
- **UI/PWA**: `Microsoft.AspNetCore.Components.WebAssembly`, `Microsoft.AspNetCore.Components.WebAssembly.Authentication`, `Blazored.LocalStorage`, `Toolbelt.Blazor.HttpClientInterceptor`.
- **API**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Swashbuckle.AspNetCore`, `SignalR`, `EFCore.Sqlite`, `EFCore.Design`, `FluentValidation`, `NRules`.
- **OPC UA**: `OPCFoundation.NetStandard.Opc.Ua`
- **Connectors**: `RestSharp`, `AngleSharp` (for shopfront scrape), `CsvHelper`.
- **Testing**: `FluentAssertions`, `Verify.Xunit`, `WireMock.Net`, `Microsoft.Playwright`.

## UI & Styling (Palantir-inspired)
- **Fonts**: Include Google Fonts: Inter (400,500,600,700), IBM Plex Mono (400,600).
- **Palette**:
  - Background: `#0B0F14` (ink) / panels `#121821`
  - Accent: `#3DA8FF` (electric blue), Hover `#5CC1FF`
  - Success `#2BD99F`, Warning `#FFC857`, Danger `#FF6B6B`
  - Text: primary `#E6EDF3`, secondary `#9FB0C1`, grid lines `#1E2631`
- **Components**: AppShell (left rail), TopBar (env badge), DataGrid (dense), StatTiles, Timeline, Stepper, TagPills, CodeBlocks.
- **Pages**:
  - `/onboard` → Client onboarding wizard (progressive digital twin)
  - `/twin` → Twin Console (assets graph, sites, machines, products)
  - `/shopfront` → Shopfront Builder (ingest products, CMS cards, publish)
  - `/connectors` → Connector Hub (ERP, Shopify, OPC UA, Files)
  - `/anomalies` → Anomaly Workbench (rules, detections, triage)
  - `/machines` → Machine Wall (live metrics via SignalR)
  - `/admin` → Settings, API keys, users

## Core Interfaces (sketch)
```csharp
public interface IConnector
{
    string Id { get; }
    string Name { get; }
    string Type { get; }
    ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct);
    IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, CancellationToken ct);
    ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync();
}

public record UpsertEvent(string EntityType, string ExternalId, JsonElement Payload, DateTime Timestamp);
public record ConnectorCheck(bool IsHealthy, string? Message, Dictionary<string, object>? Metadata = null);
```

## Digital Twin Model (minimum viable)
- Entities: Company, Site, Department, Machine, Sensor, Product, Part, BOM, Process, WorkOrder, Metric, Anomaly, Document, Contact.
- All entities have: `Id`, `ExternalRefs[]`, `Version`, `Tags[]`, `CreatedUtc`, `UpdatedUtc`.

## Anomaly Rules (example YAML)
```yaml
- id: missing-product-price
  when: entity=="Product" && (payload.Price==null || payload.Price<=0)
  then:
    severity: High
    action: "Prompt owner to set pricing or map to price list"
```

## PWA Essentials
- `manifest.webmanifest` with icons, theme color `#0B0F14`.
- `service-worker.published.js` → cache critical shell, API offline queue using BackgroundSync (fallback: IndexedDB retry loop).
- Offline screens: read-only Twin, staged edits in IndexedDB; sync banner when back online.

## Minimum Demo Data Paths
- Seed `Contoso Gears Pvt Ltd` with skeletal info (GST/Company No placeholders), 3 machines, 8 products, 2 sites.
- Simulate OPC UA via `services/MAK3R.Simulators` pushing telemetry to SignalR hub.

---

# Vision.md

## MAK3R.ai Investor PoC — Vision
**Thesis**: Become the **single pane** where a manufacturer’s fragmented data (ERP, shopfloor, files, web catalog) is unified into a living **Digital Twin** that powers onboarding, anomaly management, and revenue via instant **shopfront**.

### What success looks like (PoC)
- Onboard a new client in minutes with skeletal data.
- Auto-build a first-pass digital twin from files/ERP/website.
- Show a live machine wall via OPC UA sim.
- Spotlight anomalies/missing info and drive fixes.
- One-click **Shopfront** publish of products with clean content.

### Why now
- Post-ERP sprawl + IoT fragmentation → decision gaps. Palantir proved the value of **model-first orchestration**. MAK3R operationalizes it for SMEs/OMEs: **fewer consultants, more productized adapters**.

### Product pillars
1. **Progressive Digital Twin** (assets, products, processes) with validation loops.
2. **Connector Hub** (ERP, web, files, machines) with a strict contract.
3. **Anomaly & Variance** workbench to guide attention.
4. **Shopfront** that monetizes immediately.

---

# Readme.md

## MAK3R PWA PoC
A Blazor Web App (WASM PWA + ASP.NET Core hosted) demonstrating progressive digital twin onboarding, shopfront builder, connector hub (ERP + Shopify), and OPC UA machine telemetry.

### Quick Start
```bash
# prerequisites: .NET 8 SDK, Node 18+ (for Playwright install), SQLite 3
 git clone <repo>
 cd MAK3R-Cluster
 dotnet restore
 dotnet build
 dotnet run --project services/MAK3R.Api/MAK3R.Api.csproj
# in another shell
 dotnet run --project apps/MAK3R.PWA/MAK3R.PWA.Client.csproj
```

Open https://localhost:5001 (API) and https://localhost:5173 (PWA) depending on ports.

### Credentials
- Dev default user: `admin@mak3r.local` / `Passw0rd!` (dev only). Change via secrets.

### Features in PoC V1
- Onboarding wizard → Company → Sites → Machines → Products → Users
- Connector Hub → add Shopify & NetSuite credentials (mock mode works)
- OPC UA simulator streaming to Machine Wall
- Anomaly Workbench with rule editor
- Shopfront Builder: scrape/import products; publish preview

### Tech
- Blazor WebAssembly PWA (installable)
- ASP.NET Core Minimal APIs + SignalR
- EF Core (SQLite for PoC)
- OPC UA via OPC Foundation .NET Standard

---

# License.md

**MAK3R.ai Proprietary License (PoC Grant)**

Copyright (c) 2025 MAK3R.ai and its affiliates. All rights reserved.

Permission is hereby granted to the Licensee to internally evaluate and demonstrate the MAK3R software (the "Software") solely for the purpose of assessing its fitness for the Licensee’s business and for investor demonstrations. This license is non-exclusive, non-transferable, revocable, and limited to non-production use.

Restrictions: The Licensee shall not (a) sublicense, sell, or distribute the Software; (b) use the Software in production; (c) reverse engineer except as permitted by law; (d) remove notices. Title remains with Licensor. The Software is provided **as is** without warranty. Liability is limited to the amount paid (if any) for the PoC license.

For commercial production use, a separate commercial agreement is required.

---

# POC_V1.md

## Definition of Done (Investor PoC)

### 1) Onboarding → Progressive Twin
- [ ] Wizard captures: Company, Sites, Machines, Products (basic), People, Files.
- [ ] Twin Orchestrator builds entities, assigns confidence, logs gaps.
- [ ] IndexedDB stores drafts; sync to API when online.

### 2) Connector Hub
- [ ] **Shopify**: pull products (title, media, price) via server proxy; map → Product entities.
- [ ] **NetSuite (PoC)**: mock or sandbox — pull Items & Customers; map → Product/Contact.
- [ ] **File Ingestion**: CSV/Excel dropzone → schema inference → staging → mapping UI.

### 3) Machine Wall
- [ ] Connect to OPC UA **sim**; show live metrics (RPM, Temp, State) via SignalR; per-machine timeline.

### 4) Anomaly Workbench
- [ ] Rules: missing price, orphan machine, mismatched BOM quantities, stale sensor.
- [ ] Triage board with severity + action suggestions.

### 5) Shopfront Builder
- [ ] Scrape/import product pages; normalize images; generate SEO-ready cards.
- [ ] Preview site (static build) with brand theme; export JSON for CMS.

### 6) PWA polish
- [ ] Installable, offline banner, background sync, skeleton loaders, app icons, dark theme.

### 7) Demo Scripts
- [ ] 7-minute investor path; 20-minute deep dive.

---

# ColdStart.md

## Context for Coding Assistant
- **Name**: MAK3R PWA PoC
- **Goal**: Build a productionizable investor demo in 14 days.
- **Non-negotiables**: PWA installable; onboarding wizard; connector hub with Shopify + NetSuite mock; OPC UA live sim; anomaly workbench; shopfront preview.

## Coding Conventions
- C# 12, `nullable enable`, `file-scoped namespaces`, `required` members where applicable.
- `Result<T>` pattern for service returns; exceptions only at boundaries.
- `IMediator` optional; keep PoC light with domain services + handlers.
- Razor components: one component per folder; partials for subparts; `@code` with small methods; move logic to services.
- **Dependency graph**: UI → Shared DTOs → API → libs. No UI references to EF.

## File Map (high level)
- `libs/MAK3R.UI/` → `AppShell.razor`, `StepWizard.razor`, `DataGrid.razor`, `StatTile.razor`, `Timeline.razor`.
- `apps/MAK3R.PWA/wwwroot/` → `manifest.webmanifest`, `service-worker.published.js`, icons.
- `services/MAK3R.Api/Program.cs` → minimal APIs, auth, Swagger, SignalR `/hubs/machines`.
- `libs/MAK3R.Connectors.*` → typed clients; each exposes `IConnector`.

## Seeds
- `SeedData.cs` creates Contoso Gears with sample products/machines.

## Style Tokens (CSS vars)
```css
:root{
 --bg:#0B0F14;--panel:#121821;--text:#E6EDF3;--muted:#9FB0C1;--grid:#1E2631;
 --accent:#3DA8FF;--accentHover:#5CC1FF;--success:#2BD99F;--warn:#FFC857;--danger:#FF6B6B;
 --radius:16px;--shadow:0 10px 30px rgba(0,0,0,0.35);--spacing:12px;
}
```

---

# Priorities.md

## Build Order (investor impact × reusability)
1. **App Shell & Theme** (professional look from day 1)
2. **Onboarding Wizard** (progressive twin scaffolding)
3. **Connector Hub** (Shopify + NetSuite mock) → visible integrations
4. **Machine Wall** (OPC UA sim + SignalR) → "boardroom meets shopfloor"
5. **Anomaly Workbench** (rules + triage)
6. **Shopfront Builder** (preview site export)
7. **Offline/PWA polish** (service worker, background sync)
8. **File ingestion + schema inference** (CSV/Excel)

Parallel track: **API hardening, Identity, Data model, Tests**.

---

# TestStrategy.md

## Scope
- **Unit**: domain services (twin builder, mapping), rule engine, connectors in mock mode.
- **Integration**: API endpoints with SQLite; Connector calls against WireMock stubs.
- **E2E**: Playwright journeys: Onboard, Add connectors, Stream machines, Fix anomalies, Publish shopfront.

## Tools & Patterns
- xUnit + FluentAssertions; Verify snapshots for DTOs; Bogus for data; WireMock.Net for external APIs; Testcontainers (optional) not needed for SQLite.
- Coverage target: **70%** PoC; critical paths >= **85%**.

## CI
- GitHub Actions: build, unit + integration, Playwright headed on Ubuntu; upload HTML reports as artifacts.

---

# QualityPlan.md

## Code Quality
- Enforce `.editorconfig`, `dotnet format`, StyleCop.
- Analyzers: warnings as errors in libs; relaxed in UI.
- PR checks: build, tests, lint, size guard (< 5MB WASM payload target).

## Performance Budgets
- PWA TTI < 2.5s on mid laptop; initial payload < 2.0 MB gz.
- Web Vitals: CLS < 0.1, LCP < 2.5s (dev laptop).

## Security
- HTTPS only, JWT cookies HttpOnly/SameSite, CORS allowlist, no secrets in repo.

## Observability
- Structured logging (Serilog), correlation Ids, basic metrics (requests, hub connections).

---

# ActionPlan.md

## Week 1 (Days 1–7)
- Scaffold solution; theme + AppShell.
- Implement Onboarding Wizard (client-side forms + IndexedDB draft store).
- Seed data + Twin Orchestrator MVP.
- API identity (dev only) + Swagger.

## Week 2 (Days 8–14)
- Connector Hub + Shopify (server proxy) + NetSuite (mock).
- OPC UA simulator + SignalR Machine Wall.
- Anomaly Workbench (4 rules) + triage board.
- PWA polish (manifest, SW, offline queue).
- E2E Playwright scripts; investor demo script.

## Week 3–12 (Post-PoC)
- File ingestion inference; Shopfront export; ERP sandbox integration; advanced anomalies; role-based access; performance passes.

---

# Milestones.md

1. **M1 – Visual Shell** (Day 3): Branded shell, nav, stat tiles → screenshot-ready.
2. **M2 – Onboarding & Twin** (Day 6): End-to-end create → view twin graph.
3. **M3 – Connectors** (Day 10): Shopify live import + NetSuite mock mapping.
4. **M4 – Machines Live** (Day 12): OPC UA sim streaming in UI.
5. **M5 – Anomalies & PWA** (Day 14): Rules, triage, offline support. **Investor demo**.

---

# Detailed Specs (for implementation)

## Onboarding Wizard
- Steps: Company → Sites → Machines → Products → People → Review.
- Validation: FluentValidation rules per step.
- Storage: state in `IndexedDB` via `Blazored.LocalStorage` (key: `mak3r:onboard:v1`).
- Submit → `POST /api/onboard` to orchestrate twin creation.

## Twin Orchestrator
- Maps DTOs → domain; attaches `ExternalRef` for connectors; emits `Anomaly` if gaps.
- `IAnomalyDetector[]` run after each merge; results raised to UI via SignalR.

## Connector Hub (MCP Architecture)
- **Discovery**: Dynamic connector type registration without recompilation using IConnectorRegistry
- **Factory Pattern**: Create connector instances via IConnectorFactory with schema-driven configuration
- **String-based Types**: Connector types are strings (e.g., "shopify", "netsuite", "opcua") for extensibility
- **MCP Endpoints**: RESTful APIs for connector lifecycle management
  - `GET /api/connectors/types` - Discover available connector types
  - `POST /api/connectors/` - Create new connector instance
  - `GET /api/connectors/instances` - List active connectors
  - `GET /api/connectors/health` - Health status monitoring
- **Loose Coupling**: All connectors implement IConnector abstraction in mcps/ folder
- **Server Proxy**: Shopify uses server-side proxy to avoid CORS and secure tokens
- **Mock Support**: NetSuite includes mock mode for development without credentials

## OPC UA & Machine Wall
- Use `OPCFoundation.NetStandard.Opc.Ua` to read from `services/MAK3R.Simulators` server (or external sim).
- Stream to `/hubs/machines` with {machineId, ts, metrics{}}; UI charts live timeline; state badges (Running/Idle/Alarm).

## Anomaly Workbench
- Rule editor (YAML); test on sample entity; severity levels; assignments; status (Open, InProgress, Resolved).

## Shopfront Builder
- Import: paste URL → backend scrape with AngleSharp; extract title, images, price blocks, specs table.
- Normalize → `ProductCard` with images (WebP), SEO fields (title, meta, slug), attributes.
- Export: JSON bundle + static HTML preview (theme).

## File Ingestion
- Dropzone → parse CSV/Excel via `ExcelDataReader`/`CsvHelper`; infer columns; mapping UI to domain fields.

---

# Styling Guide (Palantir-inspired)

## Layout
- Left rail 72px, content max-width 1440px, 12-column grid.
- Tiles: 12/6/4 widths, dense gutters, soft shadows.

## Typography
- H1 28/36 semi-bold Inter; H2 22/32; Body 14/20; Mono for codes/ids.

## Micro-interactions
- Hover lifts by 2px; focus outlines with accent; subtle shimmer skeleton while loading.

---

# API Surface (PoC)

```
GET  /api/health
POST /api/onboard
GET  /api/twin/{companyId}
POST /api/connectors/{type}/test
POST /api/connectors/{type}/save
POST /api/connectors/{type}/pull?since=...
GET  /api/anomalies?companyId=...
POST /api/anomalies/{id}/assign
POST /api/shopfront/import
GET  /api/shopfront/export
```

---

# Example DTOs (Shared)

```csharp
public record CompanyDto(Guid Id, string Name, string? RegistrationId, string? TaxId);
public record ProductDto(Guid Id, string Name, string? Sku, decimal? Price, string? Currency, string? Description);
public record MachineDto(Guid Id, string Name, string? Make, string? Model, string? OpcUaNode, string SiteId);
public record AnomalyDto(Guid Id, string EntityType, string EntityId, string Severity, string Message, string Status);
```

---

# README for Developers (DX)

## Scripts
- `dotnet build` — with warnings as errors for libs.
- `dotnet test` — runs unit + integration.
- `pwsh .build/pipelines/dev-run.ps1` — launches API + PWA + simulator.

## Secrets
- `dotnet user-secrets set Shopify:Token "..." --project services/MAK3R.Api`

---

# Notes
- All integrations must be proxied via server to avoid CORS and keep tokens server-side.
- PoC **must** run without any external credentials using mocks/simulators.
- Keep payloads small; lazy-load product images; paginate grids.

