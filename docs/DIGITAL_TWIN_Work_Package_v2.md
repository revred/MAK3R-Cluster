# PLCO‑Twin Work Package v0.2 — Phases & 10‑Minute Tasks (Repo‑Aware)

> Objective: Transform **MAK3R‑Cluster** into a Progressive, Live, Chatty & Opinionated Digital Twin that ingests “breadcrumbs” (PDFs/CSVs/XLSX + telemetry), asks the *fewest, highest‑value* questions, and surfaces grounded insights (incl. SPOF risks) within one week — **aligned with the current repo (v0.2)**.

---

## A) Repo Baseline (Detected)

**Solution structure (summarized)**
```
MAK3R-Cluster/
├─ apps/
│  └─ MAK3R.PWA/                        # Blazor WebAssembly PWA
├─ services/
│  ├─ MAK3R.Api/                        # ASP.NET Core API with JWT auth
│  └─ MAK3R.Simulators/                 # OPC UA simulators
├─ libs/
│  ├─ MAK3R.Core/                       # Domain primitives
│  ├─ MAK3R.Data/                       # EF Core, DbContext, migrations
│  ├─ MAK3R.DigitalTwin/                # Twin orchestrator & models
│  ├─ MAK3R.Shared/                     # DTOs & contracts
│  └─ MAK3R.UI/                         # Shared Blazor components
├─ mcps/
│  ├─ MAK3R.Connectors.Abstractions/    # Connector contracts
│  ├─ MAK3R.Connectors/                 # Hub & registry
│  ├─ MAK3R.Connectors.Shopify/
│  ├─ MAK3R.Connectors.NetSuite/
│  └─ MAK3R.Connectors.OPCUA/
└─ tests/
   ├─ MAK3R.UnitTests/
   ├─ MAK3R.IntegrationTests/
   └─ MAK3R.PlaywrightTests/
```

**Status highlights (v0.2)**
- Connector framework (MCP‑like) + management APIs; SignalR integration; machine wall UI; anomaly workbench; file ingestion w/ schema inference; shopfront builder; CI; ~60+ unit tests; JWT auth.

**Implication**: We **extend** rather than duplicate. New components are added where missing (KG/Evidence/Q&A/TestRig/OEE depth/SPOF), and existing features are upgraded (Files, Anomalies, PWA flows).

---

## B) Proposed Additions (Fit to Baseline)

- `libs/MAK3R.KG/` — EAV + Relations + Evidence + Event ledger (append‑only)  
- `libs/MAK3R.Validation/` — Rule packs (doc chain, temporal, cross‑silo)  
- `libs/MAK3R.OEE/` — Availability/Performance + smoothing + micro‑stop clustering  
- `libs/MAK3R.SPOF/` — Critical asset analysis (centrality, ELT, RaR)  
- `services/MAK3R.QnA/` — Info‑gain question planner, ACL‑aware routing  
- `services/MAK3R.Files/` — (If not present as a separate service) adapters + mapping to Facts/Evidence  
- `tests/MAK3R.TestRig/` — Scenario DSL runner, KPIs, PDF reports  
- `tests/datasets/v0.1/` — Simulated multi‑BU breadcrumbs + event streams  
- `work/` & `docs/` — Work package, APIs, designs, playbooks

> Note: If an equivalent already exists (e.g., Files/Anomalies), **augment in‑place** instead of creating a new service.

---

## C) Phases (1‑Week MVP)

- **P0 — Enablement & Guardrails**  
- **P1 — Digital Spine & Evidence (KG)**  
- **P2 — File→Facts Pipeline**  
- **P3 — Validators→Anomaly Inbox**  
- **P4 — Spreadsheet Workbench**  
- **P5 — Q&A Planner**  
- **P6 — Streaming Slice & OEE**  
- **P7 — SPOF Detector & Insights**  
- **P8 — Test Rig & KPIs**  
- **P9 — Privacy & Security**  
- **P10 — Docs & UX Polish**  
- **P11 — CI/CD & Packaging**  
- **P12 — Connector Extensions**  
- **P13 — Data Simulators**  
- **P14 — Insights Feed**  
- **P15 — Twin Merge & Entity Resolution**  
- **P16 — Metrics & Telemetry**  
- **P17 — Release Ops**

---

## D) Functional Expectations (Phase Acceptance)

- **Spine**: EAV + evidence supports partial facts with lineage; versioned, non‑destructive.
- **Files**: Uploaded docs → typed fields + evidence (page/bbox) + facts with confidence.
- **Anomalies**: Rule hits produce actionable cards (impact, suggested owner) feeding Workbench.
- **Workbench**: Edits create new facts with reasons (no destructive overwrite).
- **Q&A**: ≤10 questions to converge on BU truths; answers stored as evidence + ACL‑routed.
- **Streaming**: Live machine states in UI <5s (simulated); late/out‑of‑order handled.
- **SPOF**: Top‑N critical assets with loss projections & mitigations.
- **Test Rig**: Scenario pass/fail with KPIs; nightly CI artifact.

---

## E) 200 Atomic 10‑Minute Tasks (IDs → Phase‑Task#)

### P0 — Enablement & Guardrails (10)
- [P0‑01] Add `work/` tree & READMEs.
- [P0‑02] Create `docs/` stubs (ARCH, API, DATA, VALIDATION, QNA, SPOF, PRIVACY, UI).
- [P0‑03] Makefile targets: `run-api`, `run-pwa`, `run-rig`.
- [P0‑04] Serilog request IDs + correlation middleware.
- [P0‑05] Add `dataroom_id` claim to JWT & middleware extraction.
- [P0‑06] ULID/Guid64 helper in `MAK3R.Core`.
- [P0‑07] Postman/Bruno collection seed.
- [P0‑08] Add `scripts/dev-seed.ps1` to reset DB & seed demo.
- [P0‑09] Add `work/ROLES.md` & staffing plan.
- [P0‑10] Ensure `global.json` pins .NET SDK matching CI.

### P1 — Digital Spine & Evidence (16)
- [P1‑01] Create `libs/MAK3R.KG` project.
- [P1‑02] Define tables: `entity`, `attribute`, `relation`.
- [P1‑03] Add `event` (append‑only ledger) + `evidence` (doc spans, bbox, page, hash).
- [P1‑04] Add columns: `confidence`, `source_id`, `valid_from/to`.
- [P1‑05] Seed demo entities (Company/BU/Site/Asset/Product).
- [P1‑06] EAV repository + unit tests.
- [P1‑07] REST: `POST /api/kg/facts` (bulk insert) in `MAK3R.Api`.
- [P1‑08] REST: `GET /api/kg/entities/{id}` (with relations & latest facts).
- [P1‑09] Event append endpoint `POST /api/kg/events`.
- [P1‑10] Evidence upload/store/link (object store + DB rows).
- [P1‑11] Lineage visualizer DTOs for PWA.
- [P1‑12] Migrations + EF configuration for EAV.
- [P1‑13] Add `MAK3R.Shared` DTOs: Fact, Evidence, Event.
- [P1‑14] Sample query perf test (100k facts).
- [P1‑15] Policy: non‑destructive upserts (version rows; no overwrite).
- [P1‑16] KG README with ERD diagram.

### P2 — File→Facts Pipeline (16)
- [P2‑01] Create or extend `services/MAK3R.Files` (if not separate, add module in Api).
- [P2‑02] `POST /api/files` (multipart) to store raw file & metadata.
- [P2‑03] Classifier stub: pdf/csv/xlsx/jobcard/invoice/po/delivery.
- [P2‑04] Parser adapters interface (IExtractor) + DI.
- [P2‑05] CSV/XLSX loader using `ExcelDataReader`/`CsvHelper`.
- [P2‑06] PDF table extractor stub (hook to Python microservice later).
- [P2‑07] Mapper: doc→facts with page/bbox + evidence rows.
- [P2‑08] Confidence mapping (OCR 0.6, native 0.9), configurable.
- [P2‑09] Source hashing & de‑duplication.
- [P2‑10] Emit `DocumentIngested` event.
- [P2‑11] Unit tests for mixed currencies & multi‑page tables.
- [P2‑12] PWA Upload UI: drop‑zone + parsed preview.
- [P2‑13] Error surfacing: per‑field extraction errors in preview.
- [P2‑14] Retry policy & poison queue for bad docs.
- [P2‑15] Telemetry counters (docs/min, extraction latency).
- [P2‑16] README with mapping conventions.

### P3 — Validators→Anomaly Inbox (14)
- [P3‑01] Create `libs/MAK3R.Validation`.
- [P3‑02] Rule: PO≠Invoice totals.
- [P3‑03] Rule: negative qty / improbable dates.
- [P3‑04] Rule: jobcard vs cycle counts mismatch.
- [P3‑05] Rule: stale vendor masters (>180d no activity).
- [P3‑06] Rule: split shipment vs revenue timing.
- [P3‑07] `GET /api/anomalies?bu=...` with paging & filters.
- [P3‑08] `POST /api/anomalies/{id}/resolve` with resolution note.
- [P3‑09] Impact scoring (High/Med/Low) + suggested owner.
- [P3‑10] PWA Inbox list with triage filters.
- [P3‑11] Inbox→Workbench deep link (selected rows/cols).
- [P3‑12] Audit trail of resolutions stored as facts.
- [P3‑13] Unit tests for each rule (happy/adversarial).
- [P3‑14] README: authoring new rules.

### P4 — Spreadsheet Workbench (14)
- [P4‑01] Grid component scaffold in PWA (reuse existing components if any).
- [P4‑02] Load curated table via `/api/anomalies/{id}/grid`.
- [P4‑03] Highlight suspect columns/rows.
- [P4‑04] Inline annotate → reasoned fact to KG.
- [P4‑05] Versioned edits (no overwrite) + author attribution.
- [P4‑06] Bulk accept/reject suggestions.
- [P4‑07] Keyboard shortcuts for speed.
- [P4‑08] Export to CSV/XLSX for offline review.
- [P4‑09] Import annotated CSV back to facts.
- [P4‑10] Conflict handling (CRDT style last‑writer‑wins + audit).
- [P4‑11] Row‑level ACL (masking forbidden columns).
- [P4‑12] Grid performance test (50k rows synthetic).
- [P4‑13] Accessibility pass (tab order, ARIA).
- [P4‑14] README: reviewer playbook.

### P5 — Q&A Planner (16)
- [P5‑01] New `services/MAK3R.QnA` project.
- [P5‑02] Unknowns model (entropy bucket for each BU truth).
- [P5‑03] Question templates with slot‑fills (e.g., “How many CNC machines in BU‑X?”).
- [P5‑04] Info‑gain estimator (prior/posterior reduction proxy).
- [P5‑05] `GET /api/qna/questions?bu=...` (ranked).
- [P5‑06] `POST /api/qna/answers` (routes to KG as facts/evidence).
- [P5‑07] ACL routing by `dataroom_id` & role.
- [P5‑08] Budgeting (max N questions/interval).
- [P5‑09] PWA Q&A stream with "Agree/Disagree" quick actions.
- [P5‑10] Evidence preview for question rationale.
- [P5‑11] Unit tests: IG sorting; routing; budget.
- [P5‑12] Adversarial tests: misleading clues.
- [P5‑13] Telemetry: IG@K, QBR counters.
- [P5‑14] README: authoring question templates.
- [P5‑15] Caching of recently answered questions.
- [P5‑16] SLA guard: throttle if misrouted attempt.

### P6 — Streaming Slice & OEE (12)
- [P6‑01] Extend `services/MAK3R.Simulators` to emit EXECUTION/ALARM/PART_COUNT.
- [P6‑02] Ingest to `event` ledger (ordered by ts; tolerate late events).
- [P6‑03] OEE availability calculation per asset/shift.
- [P6‑04] Micro‑stop clustering (threshold + hysteresis).
- [P6‑05] Machine wall card (reuse SignalR hooks already present).
- [P6‑06] Alert rules: heartbeat loss >N sec.
- [P6‑07] Unit tests for state transitions & flapping.
- [P6‑08] Backfill from batch events.
- [P6‑09] PWA timeline sparkline per asset.
- [P6‑10] KPI endpoint `/api/oee/summary`.
- [P6‑11] Perf test with 100 simulated machines.
- [P6‑12] README: simulator usage.

### P7 — SPOF Detector & Insights (10)
- [P7‑01] Build product↔routing↔machine bipartite graph.
- [P7‑02] Compute flow/ betweenness centrality.
- [P7‑03] Marginal throughput loss per asset.
- [P7‑04] Revenue‑at‑Risk estimator from invoices.
- [P7‑05] SPOF score = norm(ELT)×norm(RaR)×norm(centrality).
- [P7‑06] Outage simulation for top‑5 assets.
- [P7‑07] Recommendations (balance routes, PM, spares, subcontract).
- [P7‑08] PWA SPOF panel with ranked list.
- [P7‑09] Unit tests vs oracle datasets.
- [P7‑10] README: method + assumptions.

### P8 — Test Rig & KPIs (20)
- [P8‑01] Create `tests/MAK3R.TestRig` (console app).
- [P8‑02] Scenario DSL (YAML) loader.
- [P8‑03] Dataset folder structure `tests/datasets/v0.1/*`.
- [P8‑04] Seed Scenario A — Hidden Bottleneck Lathe.
- [P8‑05] Seed Scenario B — Phantom Supplier Switch.
- [P8‑06] Seed Scenario C — Missing Operator.
- [P8‑07] Seed Scenario D — Revenue Mirage.
- [P8‑08] Seed Scenario E — Subcontractor Black‑Hole.
- [P8‑09] Seed Scenario F — ERP Twins Collide.
- [P8‑10] Seed Scenario G — Additive Over‑Promise.
- [P8‑11] Seed Scenario H — Warehouse Drift.
- [P8‑12] KPI calc: TTAI, QBR, IG@K, ECR, SPOF‑Recall, SPOF‑TTD, Loss error.
- [P8‑13] Runner CLI: `mak3r-testrig run --scenario X --report out/X.pdf`.
- [P8‑14] All‑scenarios run + weekly report.
- [P8‑15] Ablations: RAG off/on; validators off/on; budget halved.
- [P8‑16] Random seed support for reproducibility.
- [P8‑17] PDF report (tables + entropy waterfall + SPOF map).
- [P8‑18] Store JSON KPIs to `tests/reports/`.
- [P8‑19] GitHub Actions nightly job to run rig & upload artifacts.
- [P8‑20] README: authoring new scenarios.

### P9 — Privacy & Security (12)
- [P9‑01] ABAC policy: `dataroom_id` + role → filter facts.
- [P9‑02] Row/column‑level masking in Workbench.
- [P9‑03] Redact PII in logs.
- [P9‑04] Secrets via user‑secrets/env only.
- [P9‑05] JWT signing keys rotation note.
- [P9‑06] Cross‑BU projection views (aggregate KPIs only).
- [P9‑07] Q&A routing to entitled users only.
- [P9‑08] Security unit tests for unauthorized requests.
- [P9‑09] Pen‑test checklist (local).
- [P9‑10] Data retention policy doc.
- [P9‑11] Evidence access audit trail.
- [P9‑12] DLP checks for export endpoints.

### P10 — Docs & UX Polish (10)
- [P10‑01] Update README quick‑start with new services.
- [P10‑02] Add `docs/API-Contracts.md` (OpenAPI snapshots).
- [P10‑03] `docs/DATA-Model-Spine.md` with ERD.
- [P10‑04] `docs/QNA-Planner-Design.md`.
- [P10‑05] `docs/VALIDATION-Rules.md` with examples.
- [P10‑06] `docs/SPOF-Method.md` math & assumptions.
- [P10‑07] `docs/PRIVACY-ABAC.md`.
- [P10‑08] `docs/UI-Flows.md` with screen mocks.
- [P10‑09] Add Palantir‑inspired theme CSS notes.
- [P10‑10] Glossary of domain terms.

### P11 — CI/CD & Packaging (10)
- [P11‑01] GitHub Actions: build matrix for Api/PWA/libs.
- [P11‑02] Test Rig nightly (cron) + artifact upload.
- [P11‑03] Codecov/coverlet integration.
- [P11‑04] Lint (dotnet format) on PR.
- [P11‑05] Playwright E2E job (smoke).
- [P11‑06] Dockerfiles for Api + Simulators.
- [P11‑07] Release tags v0.3‑rc.* with changelog.
- [P11‑08] SBOM generation (dotnet sbom).
- [P11‑09] Vulnerability scan (Trivy) for images.
- [P11‑10] CI cache for NuGet & Playwright.

### P12 — Connector Extensions (10)
- [P12‑01] Connector health endpoint aggregation.
- [P12‑02] Retry/backoff policy per connector.
- [P12‑03] Shopify product ingest → KG facts.
- [P12‑04] NetSuite vendor/customer master → KG.
- [P12‑05] OPC UA tag map → MachineEvent normalize.
- [P12‑06] Connector registry UI (filters, search).
- [P12‑07] Synthetic connector for CSV drop‑folder.
- [P12‑08] Connector metrics (sync time, rows, errors).
- [P12‑09] Backfill job for historical pulls.
- [P12‑10] Docs: adding a new connector.

### P13 — Data Simulators (6)
- [P13‑01] PDF synth with table noise & OCR errors.
- [P13‑02] MTConnect JSONL generator.
- [P13‑03] OPC UA tag streamer (simulated).
- [P13‑04] Vendor/customer faker (near‑duplicates).
- [P13‑05] Warranty/RMA generator.
- [P13‑06] Inventory pick/pack drift simulator.

### P14 — Insights Feed (6)
- [P14‑01] `GET /api/insights` endpoint (paged, sorted by impact/confidence).
- [P14‑02] Card types: anomaly, SPOF, cash/AR risk, routing imbalance.
- [P14‑03] Evidence links (doc span/event timeline) in card.
- [P14‑04] Dismiss/snooze/assign actions.
- [P14‑05] Telemetry on insight lifecycle.
- [P14‑06] PWA feed panel.

### P15 — Twin Merge & Entity Resolution (6)
- [P15‑01] Deterministic keys (VAT/GST, serials, PO#).
- [P15‑02] Fuzzy merge (name/address/email domain).
- [P15‑03] Merge policy (source priority + recency + confidence).
- [P15‑04] Merge Draft UI & approval.
- [P15‑05] Privacy‑safe cross‑BU projections.
- [P15‑06] Unit tests on merge precision/recall.

### P16 — Metrics & Telemetry (6)
- [P16‑01] Prometheus counters: docs ingested, facts written, IG@K.
- [P16‑02] Histograms: extraction latency, validation time.
- [P16‑03] Gauges: anomalies open, questions outstanding.
- [P16‑04] OEE summary & SPOF counts.
- [P16‑05] Log enrichment: `source_id`, `dataroom_id`.
- [P16‑06] Dashboard JSON for Grafana.

### P17 — Release Ops (6)
- [P17‑01] Version bump to v0.3.
- [P17‑02] CHANGELOG.md entries per feature.
- [P17‑03] Release notes: MVP features & demo script.
- [P17‑04] Investor demo dataset bundle.
- [P17‑05] “From scraps to insight” demo script.
- [P17‑06] Post‑release retro checklist.

---

## F) Pseudo‑Code Stubs (Drop‑in)

### F.1 EAV + Evidence (C#)
```csharp
record Fact(string EntityId, string Key, object? Value, string DataType,
            string SourceId, double Confidence, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

record Evidence(string EvidenceId, string SourceId, string DocId, int Page,
                (float x,float y,float w,float h) BBox, string TextSpan, string Hash);
```

### F.2 File→Facts Pipeline (C#)
```csharp
// POST /api/files
var fileId = await _files.StoreAsync(file);
var kind = await _classifier.DetectAsync(fileId);
var fields = await _extractor.RunAsync(fileId, kind); // (key, value, page, bbox, span)
var mapped = _mapper.ToFacts(fields);
await _kg.AddFacts(mapped.Facts, mapped.Evidence);
_events.Emit(new DocumentIngested(fileId, kind));
```

### F.3 PO vs Invoice Rule (C#)
```csharp
IEnumerable<Anomaly> ValidatePoInvoice(IEnumerable<Fact> facts) { /* ... */ }
```

### F.4 Q&A Info‑Gain (C#)
```csharp
QuestionPlan Plan(IEnumerable<Unknown> U, int budget=10) { /* ... */ }
```

---

## G) Scenario DSL & Runner CLI (Test Rig)

- DSL YAML under `tests/datasets/v0.1/<scenario>/scenario.yaml` (see canvas Test Plan doc for samples).
- Console runner `tests/MAK3R.TestRig` with `run --scenario X --report out/X.pdf` and `--all`.
- KPIs persisted as JSON; PDF reports include entropy waterfall, SPOF map, anomaly resolution log.

---

## H) Acceptance Gates (MVP)

- **Green**: TTAI ≤ 60 min; IG@10 ≥ 60%; QBR ≤ 0.4; ECR ≥ 95%; SPOF‑Recall ≥ 0.9; Misrouting = 0.
- **Yellow**: misses one KPI by ≤ 10%.
- **Red**: ≥ 2 KPIs missed or any privacy breach.

---

### Notes
- Where current code already covers similar functionality (e.g., anomaly UI, machine wall, files), the above tasks say **extend** or **wire** the new layers (KG/Evidence/Q&A/SPOF/TestRig) to what exists. Keep namespaces coherent and avoid duplication.
