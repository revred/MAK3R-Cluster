# MAK3R.Edge — MachineConnect Spec (4‑Machine Cell)

**Purpose.** Define the hardware, network, and software architecture that lets MAK3R.Cluster listen to, normalize, and stream machine events from four heterogeneous CNCs into the MAK3R platform. This fills the current gap where the **MAK3R.Edge** component is not yet elaborated in the App spec.

---

## 1) Scope & Assumptions
- Cell of **4 machines** on the same shop-floor LAN:
  1) **FANUC** controller (Turning Centre)
  2) **Siemens SINUMERIK** controller (Turning Centre)
  3) **HAAS** milling machine (NGC)
  4) **Mazak** 5‑axis (Smooth series)
- **OT VLAN** exists or will be provisioned. MAK3R.Edge is on the same L2 domain as machines.
- Machines expose standard protocols (FOCAS/OPC UA/MTConnect). If not, fallbacks are defined.
- Upstream **MAK3R.Cluster** exposes a WebSocket/SignalR endpoint and MCP-compatible plugin registry as per repo layout.

---

## 2) Hardware — MAK3R.Edge BOM

### 2.1 Reference SKUs
Choose one profile per site; both are supported by the same software image.

**Edge‑µ (cost/compact, fanless)**
- CPU: Intel N100 / AMD 5625U or ARM Cortex‑A76 class
- RAM: 16 GB
- Storage: 512 GB NVMe (industrial grade)
- NICs: **2× 1/2.5 GbE** (Intel i225/i226)
- I/O: 4× USB 3.0 (barcode scanner, maintenance console), 1× RS‑232 (legacy DNC), optional GPIO (dry‑contact inputs)
- Power: 12–24 VDC wide‑range; inline **mini‑UPS** (5–10 min) for graceful shutdown
- Mount: **DIN‑rail** or VESA

**Edge‑Pro (performance, sensor fusion)**
- CPU: Intel i7/i9 (T‑series) or AMD Ryzen Pro
- RAM: 32–64 GB
- Storage: 1 TB NVMe + 1 TB SSD (local buffer + cold cache)
- NICs: **2–4× 1/10 GbE** (OT span/mirror port option)
- Add‑ons: mPCIe/NGFF Wi‑Fi 6 (air‑gap maintenance SSID), PoE injector for IP cams, isolated digital I/O module

### 2.2 Peripherals (optional)
- **2D barcode scanners** (USB HID or Ethernet) at each cell for job context
- **Panel mount beacon** (Edge‑controlled) for heartbeat/alert state
- **Industrial managed switch** (8–12 ports, VLAN, DHCP snooping, IGMP)

---

## 3) Network Topology

```
             ┌──────────────────────────────────────── MAK3R.Cluster (Cloud/DC) ───────────────────────────────────────┐
             │  API (HTTPS)  |  SignalR Hub  |  Connector Registry (MCP)  |  Object Store  |  Timeseries/Events      │
             └────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                        ▲                         ▲
                                        │ TLS 1.2+ (outbound only)│
                                        │                         │
                        ┌────────────────┴─────────────────────────┴─────────────────┐
                        │                                                            │
                ┌───────┴───────┐                                         ┌──────────┴──────────┐
                │  MAK3R.Edge   │                                         │  Site IT Services   │
                │  (Dual‑NIC)   │                                         │  (AD/DNS/NTP)       │
                └───────┬───────┘                                         └──────────┬──────────┘
                        │  NIC‑A (OT VLAN)                                             │ NIC‑B (IT VLAN)
                        │                                                              │
         ┌──────────────┼─────────────────────────── Shop‑floor OT Switch ─────────────┼──────────────┐
         │              │                                                                   ▲          │
         │              │                                                                   │          │
   ┌─────┴─────┐  ┌─────┴─────┐  ┌─────┴─────┐  ┌─────┴─────┐                              │          │
   │  FANUC    │  │ SIEMENS   │  │  HAAS     │  │  MAZAK     │                              │          │
   │  Lathe    │  │  Lathe    │  │  Mill     │  │  5‑Axis    │                              │          │
   └───────────┘  └───────────┘  └───────────┘  └────────────┘                              │          │
    FOCAS TCP       OPC UA           MTConnect        MTConnect                             │          │
    (Ethernet)      (4840)            (HTTP)            (HTTP)                              │          │
```

**Key rules**
- **Outbound‑only** from Edge to Cloud (no inbound pinhole). If remote access is needed, use reverse‑proxy tunnel initiated by Edge.
- **Edge is dual‑homed**: NIC‑A on **OT VLAN** (machines), NIC‑B on **IT VLAN** (to internet via site egress). **No L3 routing** between VLANs on Edge.
- **Time sync**: OT switch offers **NTP**; Edge relays time to connectors; all event timestamps are UTC + site TZ metadata.
- **IP plan**: Reserve static/DHCP‑reserved IPs for each CNC. Document in the Site Inventory (Appendix A).

---

## 4) Protocols & Machine Adapters

| Make   | Primary | Alt/Fallback | What we read | Notes |
|--------|---------|--------------|--------------|-------|
| FANUC  | **FOCAS over Ethernet** | MTConnect (via adapter) | estop, cycle start/stop, execution state, alarms, spindle speed, feed override, active program/sequence, part count | Requires FOCAS enabled; default TCP 8193; read interval ≥ 250 ms; throttle alarms to event‑driven.
| SIEMENS (840D/828D) | **OPC UA Server** | MTConnect adapter | Channel state, NC program, block number, spindle/feed, mode, tool, alarms | Use signed+encrypted OPC UA (Basic256Sha256); default listener 4840; per‑machine cert trust list.
| HAAS (NGC) | **MTConnect Agent/Adapter (HTTP)** | Legacy RS‑232 (basic) | availability, execution, controller mode, path feed/spindle, program, part count, alarms | NGC exposes MTConnect via HTTP on a configured TCP port; see commissioning for exact port.
| Mazak (Smooth) | **MTConnect Adapter/Agent** | Mazak API (when available) | availability, execution, mode, program, tool, feed/spindle, alarms, part count | Newer Smooth controls support MTConnect; some require licensed adapter.

---

## 5) Edge Software — Containerized Components

**Runtime:** Ubuntu LTS (x86_64) or Fedora IoT • Container engine: **Docker** (or Podman) • Orchestrator: **Docker Compose** (single node) or **k3s** (multi‑node edge cluster)

**Core containers (all names prefixed `mak3r-`)**
1. **edge-supervisor** — boots, validates config, brings up the stack, watches health.
2. **bus-mqtt** — Local MQTT broker (Mosquitto) for intra‑edge pub/sub decoupling.
3. **store-timeseries** — Lightweight retention DB (TimescaleDB/Influx/SQLite‑WAL) for buffer & offline replay.
4. **uplink** — Bridges events to MAK3R.Cluster via **SignalR** (client) and **MCP** action calls.
5. **normalizer** — Canonicalizes raw driver payloads → `KMachineEvent` schema, enriches with job/tool context.
6. **connectors** — One container per protocol/vendor:
   - `connector-fanuc-focas`
   - `connector-siemens-opcua`
   - `connector-haas-mtconnect`
   - `connector-mazak-mtconnect`
7. **barcode-ingest** — Listener for USB/Ethernet scanners; maps scans to `JobContext`.
8. **edge-admin** — Local web UI (read‑only in OT VLAN) for diagnostics.

**Why MQTT internally?** Simple, robust, and allows each connector to publish *events* and *state* independently of the uplink. The uplink handles store‑and‑forward and back‑pressure.

---

## 6) Canonical Event Model (KMachineEvent)
```json
{
  "siteId": "BLR-Plant-01",
  "machineId": "FANUC-TC-01",
  "ts": "2025-08-23T12:34:56.789Z",
  "source": { "vendor": "FANUC", "protocol": "FOCAS", "ip": "10.10.20.11" },
  "state": {
    "power": "ON|OFF",
    "availability": "AVAILABLE|UNAVAILABLE",
    "mode": "AUTO|MDI|JOG|EDIT|MANUAL",
    "execution": "READY|ACTIVE|INTERRUPTED|STOPPED|FEED_HOLD|ALARM|ESTOP",
    "program": { "name": "O1234", "block": 1523 },
    "tool": { "id": 7, "life": 83.4 },
    "overrides": { "feed": 0.95, "spindle": 1.0, "rapid": 0.8 },
    "metrics": { "spindleRPM": 4820, "feedrate": 1250.0, "partCount": 12 }
  },
  "event": {
    "type": "PROG_START|PROG_END|CYCLE_START|CYCLE_STOP|TOOL_CHANGE|ALARM|DOOR_OPEN|DOOR_CLOSED",
    "severity": "INFO|WARN|ERROR",
    "code": "(controller specific)",
    "message": "(normalized text)"
  },
  "context": {
    "job": { "id": "WO-2025-00871", "op": "20", "barcode": "WO-2025-00871-20" },
    "operator": { "badge": "E12345" },
    "workholding": { "type": "vise", "fixtureId": "FH-556" },
    "material": { "lot": "AL7075-L123" }
  }
}
```

**Event creation rules**
- **Edge‑side synthesis** allowed: e.g., translate `execution=ACTIVE→INTERRUPTED` transitions into `FEED_HOLD` events if feed override < 5% for >5 s.
- Part count increments produce a **`PART_COMPLETED`** event with `cycleTimeMs` based on last `CYCLE_START`.
- **Idempotency**: Every event has a deterministic `eventId = hash(machineId + ts + type + code)`.

---

## 7) Configuration — Single File (HJSON/YAML)

```yaml
site:
  id: BLR-Plant-01
  timezone: Asia/Kolkata
  uplink:
    signalR: "https://cluster.mak3r.ai/hubs/machines"
    apiBase: "https://cluster.mak3r.ai/api"
    auth:
      clientId: "edge-blr-01"
      clientSecret: "<vault>"
  mqtt:
    listen: 0.0.0.0:1883
    retain_days: 3
  storage:
    engine: sqlite
    path: /var/lib/mak3r/edge/events.db

machines:
  - id: FANUC-TC-01
    make: FANUC
    model: Series-0i-TF
    ip: 10.10.20.11
    connector: fanuc-focas
    focas:
      port: 8193
      poll_ms: 250
      alarms: true

  - id: SIEMENS-TC-02
    make: SIEMENS
    model: SINUMERIK-840D
    ip: 10.10.20.12
    connector: siemens-opcua
    opcua:
      endpoint: "opc.tcp://10.10.20.12:4840"
      security: Basic256Sha256
      auth: cert
      nodes:
        execution: ns=3;s=Channel/State
        program:   ns=3;s=Program/Name
        block:     ns=3;s=Program/Block

  - id: HAAS-MILL-03
    make: HAAS
    model: VF-2SS
    ip: 10.10.20.13
    connector: haas-mtconnect
    mtconnect:
      url: "http://10.10.20.13:8082/VF2SS"
      mode: sample
      sample_interval_ms: 500

  - id: MAZAK-5X-04
    make: MAZAK
    model: VARIAXIS i-700
    ip: 10.10.20.14
    connector: mazak-mtconnect
    mtconnect:
      url: "http://10.10.20.14:5000/MAZAK"
      sample_interval_ms: 500

barcode:
  device: auto
  mapping:
    regex: "WO-(?<wo>\d{4}-\d{5})-(?<op>\d{2})"
    set:
      context.job.id: "${wo}"
      context.job.op: "${op}"
```

> **Note:** HAAS and Mazak MTConnect ports vary by configuration. Confirm during commissioning and update `mtconnect.url` accordingly.

---

## 8) Data Flow
1. **Connector** polls/subscribes to the controller (FOCAS/OPC UA/MTConnect).
2. Emits raw payload on MQTT topic `edge/<site>/<machine>/raw`.
3. **Normalizer** maps vendor tags → `KMachineEvent` and publishes to `edge/<site>/<machine>/events`.
4. **Store** persists events; **Uplink** batches over **SignalR** (binary JSON/MessagePack) to Cluster.
5. **Cluster** fan‑out: write‑optimized TS store, real‑time dashboards (SignalR), anomaly rules, and MCP workflows.

---

## 9) MCP & SignalR Integration

- Each `connector-*` exposes a minimal **MCP server** (local) with tools:
  - `probe()` – capability and tag list
  - `sample()` – one‑shot polled snapshot
  - `subscribe(filters)` – stream handle (internally MQTT topic)
  - `set_tag(name, value)` – **optional, guarded** write for controls that allow it (default OFF)
- **MAK3R.Connectors.Hub** (already in repo) discovers/loads connectors by string type and registers them with the Edge Supervisor.
- **Uplink** maintains a single **SignalR** client connection to Cluster’s `MachineHub`. Topics are multiplexed per machine; back‑pressure → local queue; QoS: **at‑least‑once** with de‑dupe on `eventId`.

---

## 10) Security Model
- **Zero inbound** from Internet; only outbound TLS 1.2+ to Cluster.
- **Machine network isolation**: Edge has **no IP forwarding**; iptables drop L3 between NICs.
- **Per‑machine credentials**: OPC UA certificates (Siemens); MTConnect is read‑only; FOCAS read‑only unless write‑ops are explicitly enabled.
- **Secrets**: stored in Edge Vault (`/var/lib/mak3r/vault.json` with OS keyring), rotated every 90 days.
- **Audit**: immutable logs (journald → Loki/CloudWatch) with machineId correlation.

---

## 11) Commissioning Checklist (per machine)

**All machines**
- [ ] Static IP/DHCP reservation in OT VLAN
- [ ] NTP server reachable; clock within ±1 s
- [ ] Cable test & port security (no auto‑MDIX errors)

**FANUC**
- [ ] Enable **FOCAS Ethernet** option
- [ ] Set TCP port (default 8193), verify ping and socket accept
- [ ] Read test: program number, execution state, spindle RPM

**SIEMENS (840D/828D)**
- [ ] Enable **OPC UA** server
- [ ] Install Edge client cert on controller trust store
- [ ] Browse address space; bind nodes → config; verify encrypted session

**HAAS (NGC)**
- [ ] Enable **MTConnect** (option/license may be required)
- [ ] Confirm MTConnect HTTP port (8082/5051/ configured)
- [ ] `/probe`, `/current`, `/sample` reachable

**Mazak (Smooth)**
- [ ] Verify **MTConnect Adapter/Agent** availability & port
- [ ] `/probe` lists device; `/sample` streams data

---

## 12) Failure Modes & Self‑Healing
- **Cloud unreachable** → events buffered locally (size+age thresholds), automatic replay.
- **Connector down** → supervisor restarts container; health topic publishes `availability=UNAVAILABLE`.
- **Clock skew** → NTP resync; events stamped with `edgeReceivedAt` + `controllerTs`.
- **Burst storms** (e.g., alarm floods) → rate‑limit per type, maintain last state.

---

## 13) KPIs & Minimum Telemetry Set (v0.1)
- Machine **Availability** (per shift/day)
- **Execution State** timeline
- **Cycle Count** & **Cycle Time** distribution
- **Alarm Rate** (per 1k min)
- **OEE base** (Availability × Performance proxy via feed/spindle × Quality via scrap input when available)

---

## 14) Example Docker Compose (single‑node)
```yaml
version: "3.9"
services:
  supervisor:
    image: ghcr.io/mak3r/edge-supervisor:0.1
    network_mode: host
    volumes:
      - ./config:/etc/mak3r
      - ./data:/var/lib/mak3r
    restart: unless-stopped

  mqtt:
    image: eclipse-mosquitto:2
    network_mode: host
    volumes:
      - ./mosquitto.conf:/mosquitto/config/mosquitto.conf
      - ./data/mqtt:/mosquitto/data
    restart: unless-stopped

  store:
    image: ghcr.io/mak3r/edge-store-sqlite:0.1
    network_mode: host
    volumes:
      - ./data:/var/lib/mak3r
    restart: unless-stopped

  uplink:
    image: ghcr.io/mak3r/edge-uplink:0.1
    network_mode: host
    env_file: .env
    depends_on: [mqtt]
    restart: unless-stopped

  connector-fanuc-focas:
    image: ghcr.io/mak3r/connector-fanuc-focas:0.1
    network_mode: host
    depends_on: [mqtt]

  connector-siemens-opcua:
    image: ghcr.io/mak3r/connector-siemens-opcua:0.1
    network_mode: host
    depends_on: [mqtt]

  connector-haas-mtconnect:
    image: ghcr.io/mak3r/connector-haas-mtconnect:0.1
    network_mode: host
    depends_on: [mqtt]

  connector-mazak-mtconnect:
    image: ghcr.io/mak3r/connector-mazak-mtconnect:0.1
    network_mode: host
    depends_on: [mqtt]
```

---

## 15) Edge–Cluster Contract (SignalR)
- **Hub**: `/hubs/machines`
- **Client → Server**:
  - `PublishEvent(KMachineEvent e)` — returns `Ack(eventId)`
  - `SyncHeartbeat(SiteHeartbeat h)` — edge health & back‑pressure
- **Server → Client**:
  - `SetSampling(Guid machineId, int ms)` — temporary override
  - `RunMcp(Guid machineId, string tool, object args)` — calls connector MCP tool (e.g., `probe`)

**Delivery semantics**: At‑least‑once; edge de‑dupes acks; cluster de‑dupes by `eventId`.

---

## 16) Job/Barcode Context
- **Ingestion**: USB HID scanner posts plain text to `/dev/input/by-id/...`; listener parses via regex mapping.
- **Binding**: On scan → publish `context.job.*` on `edge/<machine>/context` with TTL 24h or until next scan.
- **Override**: Operator HMI (local Edge Admin) can select job from ERP/Shopify via MCP ERP connectors.

---

## 17) Fallbacks for Legacy/Non‑Networked Machines
- **Discrete I/O taps**: door, cycle start/complete (dry contact → isolated digital inputs on Edge)
- **Current clamp** on spindle motor for runtime proxy
- **Add‑on MTConnect appliance** (Memex/third‑party) bridged to Edge

---

## 18) Acceptance Tests (per site)
1. **Connectivity**: All four connectors report `availability=AVAILABLE` in dashboard.
2. **Clock**: Skew < 1 s vs NTP.
3. **Event fidelity**: Run a 10‑part batch on each machine; verify `PROG_START/END`, `PART_COMPLETED` counts, and cycle times ±3% vs stopwatch.
4. **Alarm capture**: Force a benign alarm; verify code/message.
5. **Offline replay**: Disconnect WAN 10 min; ensure buffered events are later visible, ordered.

---

## 19) Roadmap (Edge v0.2 → v1.0)
- **v0.2**: Secure write‑ops (tag‑writes) with role gates; OPC UA PubSub; basic anomaly rules on Edge.
- **v0.3**: Vision/camera plugin (Instrumental‑style variance); on‑edge ML scoring; image→event links.
- **v1.0**: Multi‑edge fleet mgmt; blue/green updates; signed configs; site‑level HA (pair of Edges).

---

## Appendix A — Site Inventory Template (CSV)
```
siteId,machineId,make,model,controller,ip,proto,port,serial
BLR-Plant-01,FANUC-TC-01,FANUC,0i-TF,FANUC,10.10.20.11,FOCAS,8193,FN1234567
BLR-Plant-01,SIEMENS-TC-02,SIEMENS,840D,SINUMERIK,10.10.20.12,OPCUA,4840,SM1234567
BLR-Plant-01,HAAS-MILL-03,HAAS,VF-2SS,NGC,10.10.20.13,MTCONNECT,8082,HN1234567
BLR-Plant-01,MAZAK-5X-04,MAZAK,VARIAXIS i-700,Smooth,10.10.20.14,MTCONNECT,5000,MZ1234567
```

## Appendix B — Minimal Tag Map (per vendor)
- **FOCAS**: `statinfo`, `alarm`, `prgnum`, `absolute position`, `spindle`, `feed`, `tool` (macro vars as needed)
- **OPC UA (SINUMERIK)**: `Channel/State`, `Program/Name`, `Program/Block`, `Spindle/Speed`, `Feed/Rate`, `Tool/Number`
- **MTConnect (HAAS/MAZAK)**: `availability`, `controllerMode`, `execution`, `program`, `line`, `path_feedrate`, `spindle_speed`, `part_count`, `alarm` (category, code, nativeCode, severity)

---

**End of Spec**

