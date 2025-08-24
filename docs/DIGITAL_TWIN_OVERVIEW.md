# PLCO‑Twin MVP Test Plan v0.1 (Simulated Datasets)

> **Goal**: Prove that the Progressive, Live, Chatty & Opinionated Digital Twin (PLCO‑Twin) can start from scraps, ask the *fewest, highest‑value* questions, and converge to correct, actionable insights—including identifying single points of failure (SPOFs)—within one week.

---

## 1) Scope & Principles

- **Systems under test (SUT)**: File Ingestion Service, Knowledge Graph (KG) & Event Ledger, Q&A Planner, RAG Orchestrator, Validator Pack, Anomaly Inbox, Spreadsheet Workbench, Connector layer (simulated MTConnect/OPC UA), Twin Merge Console, Insights Feed.
- **Proof posture**: Thriller‑style *mystery arcs*. Minor details (breadcrumbs) are dispersed across documents and events. The SUT must:
  1) find contradictions or gaps,
  2) ask the right people the right questions,
  3) stitch a coherent world‑model,
  4) propose solutions/alternatives grounded in evidence.
- **Data posture**: Privacy‑aware. Facts and questions are routed by ACL (dataroom_id) with cross‑BU aggregates allowed but raw detail restricted.

---

## 2) Success outcomes & KPIs

**Insight Quality & Efficiency**
- **TTAI (Time‑to‑Actionable Insight)**: minutes from first ingest to first validated insight (<= 60 min target for seed scenarios).
- **QBR (Question Budget Ratio)**: #questions asked / #candidate questions in oracle plan (<= 0.4 target).
- **IG@K (Information Gain at K)**: entropy reduction on top‑K unknowns after each question (≥ 60% by K=10).
- **ECR (Evidence Coverage Ratio)**: % of insights with at least one linked evidence span (>= 95%).
- **CIV (Consistency Integrity Violations)**: unresolved contradictions after resolution pass (<= 2 per scenario).

**SPOF & Risk Detection**
- **SPOF‑Recall**: proportion of true critical assets flagged (>= 0.9 in designed scenarios).
- **SPOF‑TTD (Time‑to‑Detect)**: from outage injection to visible alert (< 5 min with streaming; < 15 min batch).
- **Loss Projection Error**: |predicted loss – oracle loss| / oracle loss (<= 20%).

**Human Loop**
- **Spreadsheet Resolution Rate**: anomalies resolved via Workbench within a session (>= 70%).
- **Annotation Agreement**: user agrees with model suggestion on first try (>= 65%).

**Privacy & Safety**
- **Question Misrouting Rate**: questions sent to unauthorized users (0).
- **Cross‑BU Leakage**: raw facts from other BUs shown in UI (0).

---

## 3) Simulation Overview

We ship a deterministic **Test Rig** with scripted scenarios, seeded RNG, and *oracle truths*. Each scenario:
- Provides initial **scraps** (PDFs/CSVs/XLSX) + **telemetry** (events),
- Encodes hidden truths (ground truth graph),
- Sprinkles misleading or partial clues in documents,
- Defines an **oracle question plan** (minimal set of questions to reach truth),
- Defines **oracle insights** (expected conclusions & remediations),
- Enforces ACL routing for privacy tests.

**Harness flow**
1) Seed KG with zero or minimal priors → ingest docs → run validators → produce anomalies.
2) Run Q&A Planner to propose questions ranked by info gain; simulate human answers.
3) Iterate until **stop criteria** (confidence ≥ θ OR question budget exhausted OR oracle convergence).
4) Compute KPIs vs oracle.

---

## 4) Dataset Anatomy & Generators

**Data packs (per scenario)**
- `/docs/finance/` PDFs: Invoices, POs, Delivery Notes (scanned+native mix), Credit Notes.
- `/docs/mfg/` XLSX/CSV: Job Cards, Work Orders, Routings, BOM fragments.
- `/docs/hr/` CSV: Roster (shifts, roles), absentee flags.
- `/docs/sales/` CSV: Orders, Quotes, Pricelists, Top customers.
- `/events/mtconnect/` JSONL: sample streams (EXECUTION, ALARM, PART_COUNT, FEEDRATE).
- `/events/opcua/` JSONL: tags (run/stop, cycle start, spindle load, temp).
- `/metadata/` YAML: oracle truths, oracle questions, ACLs, merge policies.

**Generators**
- PDF synth with layout tables & noisy OCR (10–15% char noise), inconsistent date and number formats.
- MTConnect/OPC UA event simulators with bursty signals, jitter, missing heartbeats.
- Fakers for vendors/customers/products with deliberate near‑duplicate strings.

---

## 5) Mystery Arc Library (Scenarios)

### A. “The Hidden Bottleneck Lathe” (Manufacturing BU)
- **Truth**: One 2‑axis lathe (ASSET‑L3) produces 42% of high‑margin SKUs due to unbalanced routing. Downtime risk is existential.
- **Breadcrumbs**:
  - Job cards show atypically high routing step frequency for OP20 with same machine ID (subtle).
  - Invoice line‑items concentrate margin in SKUs linked to OP20.
  - MTConnect reveals frequent micro‑stops (ALARM codes) but high utilization.
- **Injected noise**: Duplicate machine names (“L3”, “Lathe‑03”), mixed units in cycle times, one PO references outdated alt routing.
- **Expected insights**: SPOF detection → recommend load balancing (mirror routing on L2), preventive maintenance schedule, spare parts kit.

### B. “The Phantom Supplier Switch” (Sourcing BU)
- **Truth**: Purchasing quietly switched a key bearing supplier to a cheaper vendor; failure rates up, warranty returns spike.
- **Breadcrumbs**:
  - PO vendor code mismatch vs Invoice vendor alias; delivery notes mention alternate part code suffix “‑B”.
  - Warranty claims (CSV) show RMA rise 8% in SKUs using that bearing.
- **Noise**: OCR misreads supplier address; one invoice backdated.
- **Expected insights**: Detect vendor substitution → ask for CoA/docs → recommend revert and quarantine lots.

### C. “The Missing Operator” (HR + MFG)
- **Truth**: Senior operator’s sick leave aligns with throughput drop and scrap rate rise.
- **Breadcrumbs**: HR roster CSV with leave entries; job card rework flags; machine ALARM acknowledges longer changeovers.
- **Expected insights**: Skill bottleneck → cross‑train backup, adjust shift schedule.

### D. “Revenue Mirage” (Sales + Finance)
- **Truth**: Channel partner advanced purchase inflated recognized revenue; cash collection lag.
- **Breadcrumbs**: Invoices on last day of month; credit terms extended; delivery notes indicate split shipment.
- **Expected insights**: Revenue quality warning; cash conversion risk; propose AR prioritization.

### E. “The Subcontractor Black‑Hole” (MFG + Sourcing)
- **Truth**: Heat‑treat subcontractor introduces 2‑day delay causing cascade WIP build‑up.
- **Breadcrumbs**: Job card timestamps vs subcontractor delivery notes; MTConnect shows idle downstream machine.
- **Expected insights**: Critical path identification; buffer or alternate supplier.

### F. “ERP Twins Collide” (Merge & Privacy)
- **Truth**: Two BUs share the same customer with slightly different names; pricing conflicts violate policy.
- **Breadcrumbs**: CRM exports with near‑duplicate names; price list variance; ACL prevents cross‑exposure of line‑item detail.
- **Expected insights**: Entity resolution with privacy‑safe projection; unified price policy recommendation.

### G. “Additive Over‑Promise” (AM Service BU)
- **Truth**: Quote engine underestimates support removal time for a new alloy.
- **Breadcrumbs**: Historic job cards show longer post‑processing times; testimonials reference rough surface finish.
- **Expected insights**: Update quoting model; flag training data gap.

### H. “Warehouse Drift” (Inventory)
- **Truth**: Cycle counts diverge from system; shrinkage concentrated on high‑value spares.
- **Breadcrumbs**: Delivery note inconsistencies; picker IDs cluster; CCTV time windows (metadata only).
- **Expected insights**: Audit recommendation; layout change.

---

## 6) Oracle Plans & Stop Criteria

- **Oracle question plan**: minimal set of questions and their ideal recipients that guarantees convergence.
- **Stop criteria**:
  - Posterior confidence ≥ 0.85 on target insights, **and**
  - No unresolved hard contradictions, **or**
  - Question budget exhausted (then mark as partial/fail).

---

## 7) Questioning Engine Evaluation

**Scoring**
- **Precision@K (Questions)**: fraction of top‑K asked that are in oracle plan.
- **Recall@Budget**: fraction of oracle questions asked within budget B.
- **Average IG per Question**: Δ entropy across tracked unknowns.
- **Routing Accuracy**: % questions delivered only to authorized users.

**Ablations**
- RAG off vs on; validators off vs on; evidence link off vs on; budget halved.

**Challenge sets**
- Adversarial: misleading but plausible clues (e.g., vendor alias with similar address); OCR noise; time‑zone skew in timestamps.

---

## 8) Evidence & Grounding Tests

- Every surfaced claim must include **evidence** (doc span, page, coordinates, or event IDs).
- **Metrics**: ECR, average #evidence per insight, dead‑link rate (0), evidence latency.
- **Manual spot checks**: randomly sample 10% insights to verify evidence quality.

---

## 9) Anomaly Inbox & Spreadsheet Workbench

**Flow**
1) Validators emit anomalies (e.g., PO≠Invoice totals, negative quantities, route/date gaps).
2) Inbox groups anomalies by theme and impact; user selects a batch to open Workbench.
3) Workbench highlights suspect columns/rows; user annotates *reason* and *fix*.

**Metrics**
- Resolution Rate; Time per anomaly; Suggestion Acceptance; Reopen Rate.

**Edge cases**
- Conflicting edits by two users → CRDT merge; audit trail preserved.

---

## 10) OEE & Shopfloor Slice (Streaming)

- Simulated MTConnect/OPC UA feeds with EXECUTION, ALARM, PART_COUNT.
- Compute **Availability** (A) and **Performance** (P) initially; add **Quality** (Q) when scrap signals exist.
- Trigger **micro‑stop clustering** to detect chronic minor stops.

**Tests**
- Heartbeat loss → Edge buffer → Late arrival ordering.
- State flapping → hysteresis smoothing.

**KPIs**: SPOF‑TTD, Availability variance vs oracle, alert dedupe rate.

---

## 11) SPOF & Critical Asset Identification

**Method**
- Build bipartite graph: {Products ↔ Routings ↔ Machines} with weights from revenue mix and cycle time.
- Compute centrality (betweenness, flow) and **marginal contribution to throughput**.
- Simulate downtime shocks per asset; compute **Expected Lost Throughput (ELT)**, **Revenue at Risk (RaR)**.

**Tests**
- Inject outage of ASSET‑L3 for 8h mid‑shift.
- Check alerting, loss projection, and recommendations (load balance, preventive maintenance, spare inventory, subcontract).

**KPIs**: SPOF‑Recall, SPOF‑TTD, Loss Projection Error.

---

## 12) Merge & Conflict Resolution (Privacy‑Safe)

- Near‑duplicate detection on vendors/customers (name, address, tax ID, email domain).
- **Policy**: source priority + recency + confidence; conflicts → Merge Draft.

**Tests**
- Colliding customers across BUs with price policy contradictions.
- Verify cross‑BU view shows *aggregated KPIs* but not raw line items when ACL forbids.

**KPIs**: Merge Precision/Recall, Time‑to‑Merge, Leakage incidents (0).

---

## 13) Metrics & Formulas

Let \(H(X)\) be entropy of unknown set X. For each question q:
- **IG(q)** = \(H(X) - H(X|q)\) estimated via posterior updates.
- **IG@K** = mean IG of first K questions.
- **QBR** = \(#asked / #oracle\).
- **ECR** = \(#insights_with_evidence / #insights_total\).
- **SPOF‑Score(asset)** = normalized ELT × RaR × centrality.

Reports include **waterfall plots** of entropy reduction and a **questions → insights** Sankey.

---

## 14) Test Rig Tooling & DSL

**Project**: `tests/MAK3R.TestRig`
- Runner (C#) spins up SUT containers, seeds datasets, streams events, records logs.
- **Scenario DSL (YAML)** to declare:

```yaml
id: A_hidden_bottleneck_lathe
acl:
  bu: MFG-A
  viewers:
    - role: MfgLead
    - role: Finance (aggregate_only: true)
seeds:
  docs:
    - path: docs/mfg/jobcards_aug.xlsx
    - path: docs/finance/invoices_aug.pdf
  events:
    - path: events/mtconnect/lathe_area.jsonl
truth:
  critical_assets: [ASSET-L3]
  insights:
    - id: spof_l3
      text: Lathe L3 creates 42% high-margin throughput; single-point-of-failure.
      evidence:
        - doc: invoices_aug.pdf
          page: 7
          span: "SKU HMX-220 margin concentration"
questions_oracle:
  - to: MfgLead
    text: "Confirm # of operational lathes and routing for OP20?"
stop:
  confidence: 0.85
  max_questions: 12
```

**Event format (JSONL)**
```json
{"ts":"2025-08-24T09:01:02Z","asset":"ASSET-L3","type":"EXECUTION","value":"ACTIVE"}
{"ts":"2025-08-24T09:05:11Z","asset":"ASSET-L3","type":"ALARM","code":"SPDL-Over","severity":"LOW"}
{"ts":"2025-08-24T09:07:30Z","asset":"ASSET-L3","type":"PART_COUNT","value":1}
```

---

## 15) Runbook (One‑Week Cycle)

**Day 1**: Bring‑up + ingest smoke; verify evidence linking, anomaly emission.

**Day 2**: Scenario A + D. Measure TTAI, ECR, first IG@K curves.

**Day 3**: Scenario B + E (sourcing + subcontract). Evaluate Merge Drafts.

**Day 4**: Scenario C (HR) + streaming tests (heartbeat loss). SPOF‑TTD.

**Day 5**: Scenario F (privacy). Attempt cross‑BU aggregated insight; assert no leakage.

**Day 6**: Scenario G + H (AM + inventory). Quoting model fix suggestion surfaced.

**Day 7**: Consolidated report; ablations; green/yellow/red gates.

---

## 16) Acceptance Gates (MVP)

- **Green**: TTAI ≤ 60 min; IG@10 ≥ 60%; QBR ≤ 0.4; ECR ≥ 95%; SPOF‑Recall ≥ 0.9; Misrouting = 0.
- **Yellow**: misses one KPI by ≤ 10%.
- **Red**: ≥ 2 KPIs missed or any privacy breach.

Deliverables: PDF dashboard with KPI tables, entropy waterfall, SPOF map, and anomalies resolution log.

---

## 17) Manual Test Sheets (for Workbench)

- **PO vs Invoice tie‑out**: rows flagged; user accepts suggestion or annotates discrepancy reason.
- **Routing imputation**: missing OP times → user annotates typical cycle; model learns priors.
- **Vendor merge**: user confirms duplicate; policy updates.

Record: time per task, suggestion acceptance, subsequent anomaly decay.

---

## 18) Future Extensions

- **Counterfactual planners**: simulate “what if” labor/machine routing options under cost/throughput constraints.
- **Root‑cause graphs**: causal templates linking alarms → micro‑stops → throughput.
- **Learning to ask**: bandit over question templates with human reward.
- **Drift monitors**: detector on document formats and telemetry distributions.

---

### Packaging & Repo Integration

- Place datasets under `tests/datasets/v0.1/` with README and SHA256 hashes.
- Add `tests/MAK3R.TestRig` with CLI:

```
mak3r-testrig run --scenario A_hidden_bottleneck_lathe --report out/A.pdf
mak3r-testrig run --all --report out/weekly.pdf
```

- CI job: nightly runs on simulators; store artifacts (KPIs JSON, PDFs).

---

**This plan makes the twin earn trust like a good detective**—by asking less, proving more, and showing the receipts (evidence). It’s intentionally cross‑functional and privacy‑aware so you can demo value within days and scale safely across BUs.

