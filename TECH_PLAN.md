# EnhancedBlocker — Technology Architecture Plan

> Status: technology plan for M1 (and seams for M2+). See [BUILD_PLAN.md](BUILD_PLAN.md) for milestones, [ARCHITECTURE.md](ARCHITECTURE.md) for the ML design.
> Last updated: 2026-06-01

## Verified local toolchain
| Tool | Version | Used for |
|---|---|---|
| .NET SDK | **10.0.300** | Backend (target `net10.0`) |
| Node | 24.14.0 | Extension build |
| npm | 11.9.0 | Extension deps |
| Angular CLI | **21.2.5** | Extension UI |
| Python | 3.12.9 | ML sidecar (M2) |
| git | 2.53.0 | VCS |

## Solution layout
```
EnhancedBlocker/
  IDEA.md  ARCHITECTURE.md  BUILD_PLAN.md  TECH_PLAN.md
  EnhancedBlocker.sln
  backend/
    EnhancedBlocker.Api/        # ASP.NET Core Minimal API (Kestrel @ 127.0.0.1)
    EnhancedBlocker.Core/       # domain entities + decision cascade (reusable)
    EnhancedBlocker.Data/       # EF Core DbContext + migrations (reusable)
  extension/                    # Angular 21 workspace + esbuild for SW/content
    src/
      ui/                       # Angular app: popup, options, block surfaces
      worker/background.ts      # MV3 service worker (esbuild → ESM)
      content/content.ts        # content script (esbuild → IIFE)
      manifest.json
    tools/build-extension.mjs   # orchestrates ng build + esbuild + asset copy
  ml/                           # Python FastAPI sidecar (M2)
```
`Core` and `Data` are class libraries (not folded into Api) so the future **statistics/Pomodoro apps can reference the same entities + DbContext**.

---

## Backend (.NET 10)

**Style:** ASP.NET Core **Minimal API**, Kestrel bound to `http://127.0.0.1:<port>` (loopback only). HTTP is fine for localhost; no cert hassle.

**NuGet packages**
- `Microsoft.EntityFrameworkCore.Sqlite` (10.x)
- `Microsoft.EntityFrameworkCore.Design` (migrations)
- `Microsoft.AspNetCore.Cors` (built-in)
- (M2) `System.Net.Http.Json` for calling the Python sidecar

**Hosting (M1):** console app (`dotnet run`). Later: package as a Windows **tray app** (or service) that auto-starts. SQLite file under `%LOCALAPPDATA%\EnhancedBlocker\app.db`.

**Security (single-user localhost):**
- CORS policy allowing the extension origin `chrome-extension://<stable-id>` (stable id via a `key` in `manifest.json`).
- Shared-secret header (`X-EB-Token`) checked by middleware; token stored in backend config + extension storage. (Good enough for localhost; note to harden later.)

### Domain entities (`EnhancedBlocker.Core`)
```csharp
record Event(Guid Id, DateTimeOffset Ts, string Url, string Domain, string? Title,
             int TabId, EventType Type, Guid? FocusSessionId, long? DurationMs);

record FocusSession(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? EndedAt,
                    string DeclaredIntent, byte[]? IntentEmbedding /*M2*/);

record Rule(Guid Id, string Pattern, MatchKind Match /*Exact|Domain*/,
            RuleKind Kind /*Block|Allow*/, RuleSource Source, string? Category);

record CategoryDomain(string Domain, string Category, double Confidence, DateTimeOffset AddedAt);

record Label(Guid Id, DateTimeOffset Ts, string Url, string? Title,
             Decision Decision, LabelSource Source, string? FeaturesJson /*M2*/);

record DecisionLog(Guid Id, DateTimeOffset Ts, string Url, string Tier,
                   string Outcome, double? Score);
```
`IntentEmbedding` and `FeaturesJson` are nullable now → no migration when M2 fills them.

### Decision cascade (the key seam)
```csharp
enum Outcome { Allow, Block, Pending }              // Pending exists for M2's slow path
record DecisionContext(string Url, string Domain, string? Title, string? Text,
                       Guid? FocusSessionId, string? Intent, DateTimeOffset Now);
record TierResult(Outcome Outcome, string Tier, string Reason, double? Score);

interface IDecisionTier {                            // M1 registers Tier0 only
    Task<TierResult?> EvaluateAsync(DecisionContext ctx, CancellationToken ct); // null = defer
}

class DecisionService(IEnumerable<IDecisionTier> tiers) {
    public async Task<TierResult> DecideAsync(DecisionContext ctx, CancellationToken ct) {
        foreach (var t in tiers) {
            var r = await t.EvaluateAsync(ctx, ct);
            if (r is not null) return r;             // first decisive tier wins
        }
        return new(Outcome.Allow, "default", "no rule matched", null);
    }
}
```
- **M1:** `Tier0RuleTier` — checks `Rule` (exact/domain) + `CategoryDomain` cache → Allow/Block, else `null`.
- **M2:** append `Tier1MlTier` — HttpClient to Python; may return `Pending` while the sidecar warms.
Registered in DI in order; adding M2 = one `AddScoped<IDecisionTier, Tier1MlTier>()` line.

### API endpoints (Minimal API)
| Method | Route | Body / Query | Returns |
|---|---|---|---|
| POST | `/events` | `Event[]` (batch) | 202 |
| POST | `/decision` | `DecisionContext` | `TierResult` |
| POST | `/feedback` | `{url,title,decision,source}` | 201 (writes Label) |
| GET/POST/DELETE | `/rules` | `Rule` | rule list / 201 / 204 |
| POST | `/focus/start` | `{intent}` | `{focusSessionId}` |
| POST | `/focus/stop` | — | 200 |
| GET | `/health` | — | `{status}` |
| GET | `/reports/...` | — | M4 |

`/focus/start` is the shared seam for the **Pomodoro app** to drive sessions; reads of `Event`/`FocusSession` are the seam for the **statistics app**.

---

## Extension (Angular 21 + MV3)

**Manifest V3**, three code kinds with different constraints, so **two build tools**:

| Part | Tech | Why |
|---|---|---|
| Popup / Options / Block UI | **Angular 21** (one app, route per surface) | Rich UI, your stack |
| `background.ts` (service worker) | **esbuild** → ESM (`"type":"module"`) | SW is one file, ephemeral; no Angular |
| `content.ts` (content script) | **esbuild** → IIFE | Content scripts can't use runtime ESM imports |

**Build:** `tools/build-extension.mjs` runs `ng build` (UI, `outputHashing: none` for stable names), then esbuild for `background.ts` + `content.ts`, then copies `manifest.json` + icons into `dist/`. One `npm run build:ext`.

**UI surfaces:** single Angular app, standalone components + `provideRouter`; `popup.html`, `options.html`, `block.html` each load the bundle and route to `/popup`, `/options`, `/block`. Keeps one bundle, three entry HTMLs.

### Block + "checking…" flow (implements seam #2)
1. **Content script @ `document_start`** injects a minimal, framework-free **full-page overlay** ("checking…") so nothing flashes.
2. Sends `{url, title}` to the service worker → `POST /decision` (+ logs an Event).
3. **Allow** → content script removes overlay (reveal page).
4. **Block** → overlay is replaced by an **iframe** to `chrome.runtime.getURL('block.html')?url=...&reason=...` — full Angular block screen, same extension origin.
5. **Block screen (Angular):** shows title + reason (+ YouTube thumbnail via oembed); **Good call** → `POST /feedback {decision:block}`; **Bad call (false positive)** → `POST /feedback {decision:allow}`, set an **allow-once** flag, then `top.location = originalUrl` → **immediate access** (allow-once stops a re-block loop).
6. **Pending** (M2) is already handled: overlay just stays in "checking…" until resolved — **no extension rework needed for ML.**

### Chrome APIs (M1)
- `chrome.webNavigation` / `chrome.tabs.onUpdated` — observe navigations (logging + trigger decision).
- `chrome.storage.local` — focus state, API token, allow-once flags.
- `host_permissions`: `http://127.0.0.1/*`; `permissions`: `webNavigation`, `storage`, `tabs`, `scripting`.
- `manifest.key` → stable extension id (needed for backend CORS allowlist).
- *(declarativeNetRequest deliberately not used in M1 — we route through `/decision` to keep one async-capable cascade path for M2.)*

---

## Python ML sidecar (M2 — committed tech, built later)
- **FastAPI** + **uvicorn**, loopback port, spawned/killed by .NET (lifecycle in BUILD_PLAN.md).
- `sentence-transformers` (`all-MiniLM-L6-v2`, 384-dim) — embeddings.
- `transformers` zero-shot (`facebook/bart-large-mnli`, or smaller `valhalla/distilbart-mnli-12-3` for CPU) — categories.
- `scikit-learn` — nearest-centroid (few-shot) → logistic regression (M3).
- Endpoints: `/embed`, `/zeroshot`, `/score`, `/health`. Managed via a `venv`; `.NET` launches `python -m uvicorn ml.app:app --port <p>` and polls `/health`.
- (Verify `torch` wheels for Python 3.12 at setup — fine on 3.12.)

---

## M1 implementation order
1. `dotnet new sln` + `Core` / `Data` / `Api` projects; entities + `AppDbContext` + first migration (SQLite).
2. `DecisionService` + `Tier0RuleTier`; wire DI.
3. Minimal API endpoints + CORS + token middleware; `dotnet run` smoke test.
4. Angular workspace + esbuild build script; manifest; load unpacked in Chrome.
5. Service worker (nav logging + decision call) ↔ content script (overlay/checking/block iframe).
6. Angular surfaces: popup (focus toggle + intent), options (rules CRUD), block screen (Good/Bad call + immediate access).
7. End-to-end: log a session, block the ~20 URLs, confirm Good/Bad-call writes Labels.
