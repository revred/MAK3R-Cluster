# LANDING.md — Instant SSR Landing Page Workpackage

> Purpose: Add a Palantir‑style, manufacturing‑flavoured landing page that renders **instantly** with **SSR + server islands**, while deferring WASM to deeper routes. This package is productionizable (no throwaway).

---

## 1) Goal & Success Criteria
**Goal:** Make `/` feel “already ready” with near‑instant First Paint and immediate interactivity, without loading the heavy WASM bundle until the user navigates under `/app/*`.

**Success metrics (Dev laptop, fresh cache):**
- FCP ≤ **700ms**; LCP ≤ **1.2s**; TTI ≤ **100ms** (SSR interactive server islands)
- HTML + critical CSS ≤ **35KB** gz; No WASM requests on `/`
- Lighthouse Performance ≥ **95**; CLS ≤ **0.05**
- Visual parity with MAK3R dark theme (Palantir‑inspired)

---

## 2) What We’re Building
- **Route split**: `/` = ultra‑light SSR landing with a few server‑interactive islands (ticker, preview); `/app/*` = full product (onboard, twin, machines) hydrating with WASM as needed.
- **Palantir‑style UI**: dense, dark, crisp, data‑forward.
- **PWA‑aware**: SW pre‑caches fonts + CSS for instant revisits.

---

## 3) Deliverables (Checklist)
- [ ] `Components/Landing.razor` — SSR landing
- [ ] `Components/MachineTicker.razor` — server island (SignalR/timer)
- [ ] `Components/AnomalyPreview.razor` — server island
- [ ] `Components/StatTile.razor` — shared compact stat component
- [ ] `wwwroot/css/landing.css` — landing critical styles (Palantir‑like)
- [ ] `_Layout.cshtml` (or `App.razor` host) — preload fonts + inline critical CSS
- [ ] `Program.cs` — enable SSR + InteractiveServer; defer WASM
- [ ] `wwwroot/manifest.webmanifest` — ensure theme matches; icons exist
- [ ] `service-worker.published.js` — ensure CSS/fonts/hero assets pre‑cached
- [ ] Playwright E2E `Landing.spec.ts` — perf & smoke
- [ ] Feature flag `FeatureFlags:NewLanding` + fallback

---

## 4) Architecture Changes
### 4.1 Render Modes
- Enable **Interactive Server** (SignalR circuits) for islands on `/`.
- Keep **Interactive WASM** for `/app/*` only.

### 4.2 Program.cs patch (Server)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()        // for landing islands
    .AddInteractiveWebAssemblyComponents();  // for /app/* pages

var app = builder.Build();

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions {
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append(
        "Cache-Control", "public,max-age=31536000,immutable")
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
```

### 4.3 Routes
- `/` → `Landing.razor` (SSR + server islands)
- `/app/onboard`, `/app/twin`, `/app/machines`, `/app/connectors` → WASM/Auto hydrate

---

## 5) Visual System (Palantir‑inspired)
### 5.1 Tokens (reuse across app)
```css
:root{
  --bg:#0B0F14;--panel:#121821;--text:#E6EDF3;--muted:#9FB0C1;--grid:#1E2631;
  --accent:#3DA8FF;--accentHover:#5CC1FF;--success:#2BD99F;--warn:#FFC857;--danger:#FF6B6B;
  --radius:16px;--shadow:0 10px 30px rgba(0,0,0,.35);
}
```

### 5.2 Layout
- Content max‑width: **1280–1440px**
- Grid: **12‑col**, tight gutters, card density
- Typography: Inter (400/600/700), IBM Plex Mono for codes/ids

---

## 6) Implementation Steps

### 6.1 Create Landing Page
`Pages/Marketing.razor`
```razor
@page "/"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@layout EmptyLayout

<section class="hero">
  <div class="hero__content">
    <h1>Transform your manufacturing operations with unified digital intelligence</h1>
    <p class="sub">MAK3R connects your ERP, MES, SCADA, and IoT systems into a single source of truth. Gain real-time visibility, predict failures before they happen, and optimize operations automatically.</p>
    <div class="cta">
      <a class="btn btn-primary" href="/dashboard">Book a Demo</a>
      <a class="btn btn-ghost" href="/demo">See Live Demo</a>
    </div>
    <ul class="hero__badges">
      <li>Enterprise Ready</li><li>ISO 27001</li><li>SOC2 Type II</li>
    </ul>
  </div>
  <div class="hero__viz">
    <div class="stats-grid">
      <StatTile Title="Uptime" Value="99.7%" Hint="Achieved" />
      <StatTile Title="Savings" Value="$4.2M" Hint="Annual Avg" />
      <StatTile Title="Connectors" Value="150+" Hint="Ready" />
      <StatTile Title="Processing" Value="Real-time" Hint="Data" />
    </div>
  </div>
</section>

<section class="grid">
  <div class="feature-card">
    <h3>Universal Integration</h3>
    <p>Connect any system, any protocol. From legacy SCADA to modern cloud APIs.</p>
    <div class="feature-metric">150+ connectors ready</div>
  </div>
  <div class="feature-card featured">
    <h3>Digital Twin Intelligence</h3>
    <p>Living models of your machines, processes, and products that learn and predict.</p>
    <div class="feature-metric">99.7% prediction accuracy</div>
  </div>
  <div class="feature-card">
    <h3>Autonomous Operations</h3>
    <p>Beyond monitoring. MAK3R takes action to optimize performance automatically.</p>
    <div class="feature-metric">24/7 optimization</div>
  </div>
</section>

<section class="panel">
  <h2>Proven results across manufacturing</h2>
  <div class="results-grid">
    <StatTile Title="OEE Improvement" Value="47%" Hint="Avg across 200+ lines" />
    <StatTile Title="Cost Savings" Value="$12M" Hint="Per facility annually" />
    <StatTile Title="Data Silos" Value="89%" Hint="Eliminated" />
    <StatTile Title="Decisions" Value="6.2x" Hint="Faster" />
  </div>
</section>

<section class="final-cta">
  <h2>Ready to transform your manufacturing operations?</h2>
  <p class="sub">Join 500+ manufacturing leaders who chose MAK3R to eliminate data silos and achieve operational excellence.</p>
  <div class="cta">
    <a class="btn btn-primary" href="/dashboard">Book Your Demo</a>
    <a class="btn btn-ghost" href="/demo">Try Interactive Demo</a>
  </div>
  <p class="assurance">No contracts • No credit card • Full access in minutes</p>
</section>
```

### 6.2 Islands
`Components/MachineTicker.razor`
```razor
@inject IMachineTicker Ticker
<div class="ticker">
  @if (_rows is null)
  { <div class="skeleton">Connecting to shop floor…</div> }
  else
  {
    <ul>
      @foreach (var r in _rows)
      { <li><strong>@r.Machine</strong> • @r.State • @r.RPM rpm • @r.Temp°C</li> }
    </ul>
  }
</div>

@code {
  private List<Row>? _rows;
  protected override async Task OnInitializedAsync()
  {
    await foreach (var s in Ticker.StreamAsync())
      _rows = s.ToList();
    StateHasChanged();
  }
  public record Row(string Machine, string State, int RPM, int Temp);
}
```

`Components/AnomalyPreview.razor`
```razor
@inject IAnomalyService Anoms
@if (_items is null) { <div class="skeleton">Scanning for anomalies…</div> }
else
{
  <ul>
    @foreach (var a in _items.Take(5)) { <li><span>@a.Severity</span> @a.Message</li> }
  </ul>
}
@code {
  private IEnumerable<AnomalyDto>? _items;
  protected override async Task OnInitializedAsync() => _items = await Anoms.GetTopAsync();
}
```

`Components/StatTile.razor`
```razor
<div class="stat">
  <div class="k">@Value</div>
  <div class="t">@Title</div>
  @if (!string.IsNullOrWhiteSpace(Hint)) { <div class="hint">@Hint</div> }
</div>
@code {
  [Parameter] public string Title { get; set; } = "";
  [Parameter] public string Value { get; set; } = "—";
  [Parameter] public string? Hint { get; set; }
}
```

### 6.3 CSS
`wwwroot/css/landing.css`
```css
:root{ --bg:#0B0F14;--panel:#121821;--text:#E6EDF3;--muted:#9FB0C1;--grid:#1E2631;--accent:#3DA8FF;--accentHover:#5CC1FF;--radius:16px;--shadow:0 10px 30px rgba(0,0,0,.35);}
body{ background:#0B0F14;color:var(--text);font-family:Inter,system-ui,-apple-system,"Segoe UI",Roboto,Arial }
.hero{ display:grid;grid-template-columns:1.2fr .8fr;gap:32px;padding:64px 24px 32px;max-width:1280px;margin:0 auto }
.hero__content h1{ font-weight:600;font-size:32px;margin:0 0 8px;letter-spacing:.2px }
.sub{ color:var(--muted);margin:0 0 20px }
.cta{ display:flex;gap:12px;margin:12px 0 16px }
.btn{ padding:10px 14px;border-radius:12px;border:1px solid var(--grid);text-decoration:none;color:var(--text) }
.btn-primary{ background:linear-gradient(135deg,#1677ff,#3DA8FF);border-color:transparent;box-shadow:var(--shadow) }
.btn-primary:hover{ filter:brightness(1.05) }
.btn-ghost:hover{ border-color:var(--accent);color:var(--accent) }
.hero__badges{ display:flex;gap:10px;list-style:none;padding:0;margin:10px 0 0;flex-wrap:wrap }
.hero__badges li{ background:#0f1720;border:1px solid var(--grid);border-radius:999px;padding:6px 10px;font-size:12px;color:#b8c7d8 }
.hero__viz{ background:#0f1720;border:1px solid var(--grid);border-radius:var(--radius);min-height:240px;box-shadow:var(--shadow) }
.grid{ max-width:1280px;margin:24px auto;padding:0 24px;display:grid;grid-template-columns:repeat(4,1fr);gap:14px }
.panel{ max-width:1280px;margin:24px auto;padding:24px;background:#0f1720;border:1px solid var(--grid);border-radius:var(--radius) }
.stat{ background:#0f1720;border:1px solid var(--grid);border-radius:var(--radius);padding:16px }
.stat .k{ font-size:24px;font-weight:600 }
.stat .hint{ color:var(--muted);font-size:12px }
.skeleton{ height:100%;min-height:40px;background:linear-gradient(90deg,#11161f,#151c27,#11161f);background-size:200% 100%;animation:s 1.4s infinite ease-in-out }
@keyframes s{0%{background-position:200% 0}100%{background-position:-200% 0}}
@media (max-width:1024px){ .hero{ grid-template-columns:1fr } .grid{ grid-template-columns:repeat(2,1fr) } }
```

### 6.4 Layout Host: preload fonts + critical CSS
`_Layout.cshtml` (or equivalent host):
```html
<link rel="preload" href="/fonts/Inter-Variable.woff2" as="font" type="font/woff2" crossorigin>
<link rel="stylesheet" href="/css/landing.css">
<style>
/* Optionally inline ~2KB of critical hero styles for true instant paint */
</style>
```

### 6.5 Service Worker pre‑cache (first revisit is instant)
`service-worker.published.js` — ensure font/CSS pre‑cache entries exist and versioned.

### 6.6 SEO & A11y
- Proper `<h1>`/`<h2>` hierarchy, skip‑to‑content link, focus styles
- Meta tags: title/description; OpenGraph image (lightweight SVG/PNG)
- Optional JSON‑LD `SoftwareApplication` with brand fields

### 6.7 Feature Flag & Fallback
- `appsettings.Development.json` → `"FeatureFlags": { "NewLanding": true }`
- If disabled, route `/` to prior home.

---

## 7) Testing Plan
### 7.1 Unit/Snapshot
- Verify landing component HTML (Verify.Xunit) — ensures no accidental bloat

### 7.2 Playwright E2E (`tests/MAK3R.PlaywrightTests/Landing.spec.ts`)
- Load `/` → assert **no** WASM fetch
- Assert FCP < 1s via `performance.getEntriesByType('navigation')`
- Click **Live Machines** → deep route loads WASM and hydrates

### 7.3 Performance Budgets
- HTML+critical CSS gz ≤ 35KB
- Fonts (WOFF2) ≤ 80KB total on first load; immutable cache

---

## 8) CI/CD
- Add Lighthouse CI (optional) or Playwright perf assertions
- Artifact: Lighthouse HTML + trace; fail build if budgets breached

---

## 9) Security & Headers
- `Cache-Control: immutable` for static assets
- CSP (report‑only for PoC): `default-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:`

---

## 10) Timeline
- **Day 1**: Components + CSS + Program.cs + fonts + SW entries
- **Day 2**: Playwright tests + budgets + polish + demo pass

---

## 11) Demo Script (2 minutes)
1. Load `/` — note instantaneous paint + live ticker moving.
2. Show badges + stat tiles; open anomaly panel.
3. Click **Start Onboarding** → `/app/onboard` (WASM loads now) — highlight deferred hydration.

---

## 12) Risks & Mitigations
- **SignalR disconnects on landing** → show skeleton; auto‑reconnect
- **Font FOUT/CLS** → self‑host + preload; set consistent `font-display: swap`
- **WASM leak onto landing** → assert via E2E (no `.wasm` requests on `/`)

---

## 13) Acceptance Criteria
- Instant paint, interactive server islands functioning
- No WASM network requests on `/`
- Meets design tokens and layout spec
- All checkboxes in §3 green; tests passing

