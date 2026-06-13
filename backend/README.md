# EnhancedBlocker — Backend (M1)

.NET 10 onion-architecture backend for the EnhancedBlocker context-aware website
blocker + time tracker. This is **Milestone 1**: event tracking + the Tier-0
(hard-rule) decision tier. No Python/ML yet (those are M2 seams, already wired in
nullable columns and the pluggable cascade).

## Projects (onion; dependencies point inward only)

| Project | Role |
|---|---|
| `EnhancedBlocker.Domain` | Entities (`Event`, `FocusSession`, `Rule`, `CategoryDomain`, `Label`, `DecisionLog`) as classes with **private setters** + static `Create`/`Update` factories returning `OneOf<T, ValidationError>`. Enums. No framework deps except `OneOf`. |
| `EnhancedBlocker.Application` | CQRS commands/queries + handlers, port interfaces, and the decision cascade (`DecideQueryHandler`). Handlers are injected and called directly (no mediator). Depends only on Domain. |
| `EnhancedBlocker.Infrastructure` | EF Core (`AppDbContext`) on **PostgreSQL/Npgsql**, entity configs, repository adapters, and `Tier0RuleTier`. The initial migration lives here. Depends on Application + Domain. |
| `EnhancedBlocker.Api` | ASP.NET Core **Minimal API** host (composition root). Loopback-only Kestrel, token middleware, CORS, thin endpoints. |
| `EnhancedBlocker.Tests` | xUnit: domain factories, the cascade handler, and Tier-0 matching. |

## CQRS: direct handler injection (no mediator)

We keep CQRS — one command/query record + one handler per use case — but there is
no mediator or dispatcher indirection. Each handler is registered as its concrete
type (`AddScoped<XHandler>()`) and endpoints inject the specific handler(s) they
need, calling `handler.Handle(message, ct)` directly. This keeps the call path
explicit and avoids any third-party mediator dependency. Endpoints stay thin: build
the message, invoke the handler, map the `OneOf` result.

## Configuration

`appsettings.json` / `appsettings.Development.json`:

- `ConnectionStrings:AppDb` — PostgreSQL connection. Default:
  `Host=localhost;Port=5432;Database=enhancedblocker;Username=postgres;Password=postgres`
- `EnhancedBlocker:Port` — loopback port Kestrel binds to (default **5180**).
- `EnhancedBlocker:ApiToken` — shared secret required in the `X-EB-Token` header
  on every endpoint except `GET /health`.
- `EnhancedBlocker:ExtensionId` — stable `chrome-extension://<id>` allowed by CORS
  in production.
- `EnhancedBlocker:AllowDevOrigins` — when true (dev), CORS allows any origin.
- `EnhancedBlocker:AutoMigrate` — when true (Development default), apply EF
  migrations on startup and seed Tier-0 rules from `SeedRulesFile` if the Rules
  table is empty.
- `EnhancedBlocker:SeedRulesFile` — starter block list (default `seed-rules.json`
  in the Api project). **Edit it to your personal known-bad list**; it is only read
  when the Rules table is empty.

The design-time migration factory also honors the `EB_CONNECTION_STRING`
environment variable.

## Run

```bash
# 1. Ensure the database exists (any client). Example:
#    psql -h localhost -U postgres -c "CREATE DATABASE enhancedblocker;"

# 2. Build
dotnet build backend/EnhancedBlocker.sln

# 3. Run the API. Development (appsettings.Development.json) uses token "dev-token"
#    (matching the extension's default) and AutoMigrate=true, which applies the EF
#    migration and seeds seed-rules.json on first start — no manual `dotnet ef` step.
dotnet run --project backend/EnhancedBlocker.Api

# (Manual migration alternative; install tools once: dotnet tool install --global dotnet-ef)
dotnet ef database update \
  --project backend/EnhancedBlocker.Infrastructure \
  --startup-project backend/EnhancedBlocker.Api
```

The API listens on `http://127.0.0.1:5180` (loopback only).

## Endpoints

| Method | Route | Notes |
|---|---|---|
| GET | `/health` | `{status}`. Bypasses the token guard. |
| POST | `/events` | Batch of events → 202. |
| POST | `/decision` | `DecisionContext` → `{outcome, tier, reason, score?}`. |
| POST | `/feedback` | Writes a `Label` → 201. |
| GET | `/rules` | List rules. |
| POST | `/rules` | Add a rule → 201. |
| DELETE | `/rules/{id}` | 204 / 404. |
| POST | `/focus/start` | `{intent}` → `{focusSessionId}`. |
| POST | `/focus/stop` | optional `{focusSessionId}` (defaults to active session) → 200. |

All non-`/health` endpoints require header `X-EB-Token: <ApiToken>`.

**Wire contract:** JSON is camelCase and **enums travel as strings**
(`JsonStringEnumConverter`): `"type":"navigate"`, `"match":"Domain"`,
`"decision":"block"`. Binding is case-insensitive, but multi-word values must be
the exact member name — `"source":"GoodCall"`, not `"good-call"`. The endpoint
contract tests in `EnhancedBlocker.Tests/Api/ApiContractTests.cs` pin this with
the extension's exact payloads.

### Example

```bash
TOKEN=dev-token
curl http://127.0.0.1:5180/health
curl -X POST http://127.0.0.1:5180/rules -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"pattern":"youtube.com","match":"Domain","kind":"Block","source":"Manual"}'
curl http://127.0.0.1:5180/rules -H "X-EB-Token: $TOKEN"
curl -X POST http://127.0.0.1:5180/decision -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"url":"https://youtube.com/watch?v=1"}'    # → outcome Block
curl -X POST http://127.0.0.1:5180/decision -H "X-EB-Token: $TOKEN" -H "Content-Type: application/json" \
  -d '{"url":"https://github.com"}'               # → outcome Allow (default)
```

## Tests

```bash
dotnet test backend/EnhancedBlocker.sln
```

Covers domain `Create`/`Update` factories (valid + invalid), `DecideQueryHandler`
(block match, allow default, defer fall-through), and `Tier0RuleTier` (exact,
domain, subdomain, lookalike-suffix, allow precedence, category cache).

## M2 seams (already in place)

- `FocusSession.IntentEmbedding` — nullable PostgreSQL `bytea` (stays null in M1).
- `Label.FeaturesJson` — nullable PostgreSQL `jsonb` (stays null in M1).
- `IDecisionTier` cascade — M1 registers only `Tier0RuleTier`; adding the ML tier
  is one `AddScoped<IDecisionTier, Tier1MlTier>()` line.
- `Outcome.Pending` exists for M2's slow ("checking…") path.
