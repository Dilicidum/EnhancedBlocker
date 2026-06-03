# EnhancedBlocker — Single-Tasking Guard (design exploration)

> Status: design exploration / options. Nothing built yet for this feature.
> See [IDEA.md](IDEA.md) (vision) · [ARCHITECTURE.md](ARCHITECTURE.md) (blocker design) · [ROADMAP.md](ROADMAP.md) (WPs).
> Last updated: 2026-06-03

## The problem (in my own words)

I want to stop myself from doing **multiple things at once** — most concretely:
listening to music or having a video playing **in the background** while I'm
doing focused work (e.g. web coding). I *think* I'm relaxing, but I'm actually
splitting my attention. Ideally I'm only ever doing **one thing at a time**.

## Why this is a *different* problem from the existing blocker

The existing blocker answers **"should I be on this *page*?"** — a per-page,
per-navigation **content** question (Tier 0 rules → Tier 1 ML). See
[ARCHITECTURE.md](ARCHITECTURE.md).

Single-tasking is a **concurrency** question: **"how many attention streams are
live *right now*?"** It is:
- **Cross-tab / cross-window**, not per-page. Each tab might individually be
  "allowed", yet *together* they're a violation (coding tab + background music tab).
- **Temporal / ambient**, not gated on a navigation. Music started 20 minutes ago
  doesn't trigger any `webNavigation` event today.
- About the **active vs. background** relationship — media is fine when it *is*
  the thing you're doing; the problem is media playing while you do something else.

So it doesn't fit the per-navigation `DecisionContext` cascade cleanly; it wants
its own ambient **activity monitor** path (detailed under Decision 4).

### What's actually detectable (scope reality)
- **In-browser audio/video is highly detectable.** Chrome exposes `tab.audible`
  and `tab.mutedInfo`; a content script sees real `<video>`/`<audio>` playback and
  `document.visibilityState`. This already covers the headline case: a YouTube /
  music tab playing while you work in another tab.
- **Desktop apps are invisible to the extension.** Spotify desktop, a native video
  player, a second monitor — the extension cannot see these. Catching them needs an
  **OS-level companion** (Decision 1, option A4). Worth naming up front so v1's
  coverage is honest: *web-first, like the rest of the project.*

---

## Decision 1 — Detection signal (how do we know two things are live?)

| Opt | Signal | Catches | Cost | Notes |
|----|--------|---------|------|-------|
| **A1** | `tab.audible` across tabs (service worker) | Any tab making sound that isn't your active tab | **Tiny** | `chrome.tabs.query({audible:true})` + `tabs.onUpdated`(`changeInfo.audible`). Zero new permissions (`tabs` already granted). The 80/20 signal. |
| **A2** | Content-script media instrumentation | `<video>`/`<audio>` `play`/`pause`/`ended`, muted-but-playing, page `visibilityState` | Low–med | More precise: knows whether the media tab is *foreground & visible* vs truly background. Distinguishes "watching" from "background audio". |
| **A3** | A2/A1 **+ content category** (reuse M2 ML) | "*Entertainment* media playing while intent = coding" | Med (needs M2) | Lets "focus music" pass while an off-topic podcast is flagged — uses the existing zero-shot category seam. |
| **A4** | OS companion: Core Audio sessions + foreground window | Spotify/VLC/desktop, any app making sound | **High** | A Windows helper (e.g. via the always-on .NET tray app) enumerating audio sessions + `GetForegroundWindow`. Biggest coverage, biggest build. |

**Lean:** start **A1**, enrich with **A2** (precision: foreground vs background).
A3 layers on once M2 ML exists. A4 is a later milestone if desktop leakage proves
to matter.

## Decision 2 — What counts as a violation (policy)

| Opt | Rule | Feel |
|----|------|------|
| **B1** | **Any** background audio during a focus session | Strictest single-tasking. Simple, unambiguous. |
| **B2** | Background audio only if **off-topic** vs declared intent | Lenient: coding lo-fi allowed, unrelated podcast blocked. Needs A3/M2. |
| **B3** | **Foreground** media + you're active in *another* tab/window | Catches "video playing while I type elsewhere", not just audio. Uses A2 + `tabs.onActivated`/`windows.onFocusChanged`. |
| **B4** | B1/B3 **+ an allowlist** (e.g. a chosen focus-music domain/playlist) | Practical escape hatch; reuses the `Rule`/allow concept. |

**Lean:** **B1 + B4** for v1 (strict, but with an allowlist so "focus music" is a
deliberate, declared exception rather than an accidental one). Graduate to **B2**
when ML lands.

## Decision 3 — Intervention (what to do about it)

| Opt | Action | Friction | Reuse |
|----|--------|----------|-------|
| **C1** | **Hard block** the active tab (the block screen) until the media is muted/closed | High | Reuses the existing block-screen overlay + Good/Bad-call feedback. |
| **C2** | **Auto-mute / pause** the offending tab (`tabs.update {muted:true}`; content script `.pause()`) | Low, automatic | Decisive; mute is reliable, true "pause" needs the content script. |
| **C3** | **Soft nag**: banner/toast "You're listening to X while working on Y — pick one" with *Stop* / *It's fine* | Medium | *It's fine* logs a `Label` exactly like Bad-call — friction = label source. |
| **C4** | **Log only** (passive) → feeds reports: "you multitasked N% of focus time" | None | Pure data; aligns with IDEA.md Part 2/3 ("where did my day go"). |

**Lean:** ship **C4 first** (measure before you police — and it's risk-free), then
**C3** (the nag is the primary label source, mirroring the block screen's design),
with **C2 auto-mute** as the opt-in "strict mode" enforcement. C1 hard-block is the
nuclear option, reserved for repeat offenders.

> Note: matches the project's existing philosophy — *friction doubles as the label
> source* (ARCHITECTURE.md "label collection via friction"). The "It's fine" / "Stop"
> choice is the concurrency analogue of "Bad call / Good call".

## Decision 4 — Where it lives (architecture)

| Opt | Placement | Fit with conventions |
|----|-----------|----------------------|
| **D1** | **Extension-only** heuristic — SW watches audible tabs and acts locally | Fastest to ship; but bypasses the backend, so nothing is logged/reported and it violates "decisions go through the backend". |
| **D2** | **Backend-driven** — SW pushes *activity snapshots*; backend decides + returns a directive | Matches onion + CQRS; logged for reports; ML-ready. **Recommended.** |
| **D3** | D2 **+ a new decision tier** shoehorned into the existing cascade | Tempting reuse, but concurrency is ambient/cross-tab, not per-navigation — forcing it into `DecisionContext` muddies the cascade. Keep it a **sibling path**. |

**Lean:** **D2**, as a *sibling* to the page cascade (not inside it):

```
 service worker (ambient)                       .NET backend (onion + CQRS)
 ┌───────────────────────────┐  POST /activity  ┌───────────────────────────────┐
 │ track active tab + audible │ ───────────────► │ EvaluateActivityQuery          │
 │ tabs (A1) + media events   │   ActivitySnapshot│  → ConcurrencyPolicy (B1+B4)   │
 │ (A2). Debounced snapshots. │ ◄─────────────── │  → directive {Allow|Nag|Mute|  │
 └───────────────────────────┘   Directive       │     Block} + reason            │
        │ apply C2/C3/C1                          │  → log ConcurrencyEvent (C4)   │
        ▼                                         └───────────────────────────────┘
   mute / nag / block screen
```

### Concrete changes (recommended slice)
**Domain** (`Create`/`Update` factories, `OneOf`, per CLAUDE.md):
- `ConcurrencyEvent` — `Id, Ts, FocusSessionId?, ActiveUrl, BackgroundUrl, Kind(BackgroundAudio|ForegroundMediaWhileActiveElsewhere), ResolvedMs?`. The
  loggable record (C4) and report input.
- `ConcurrencyPolicy` (or extend `Rule`) — strictness (`Off|Measure|Nag|Strict`),
  allowlist domains (B4). Encapsulated invariants in the factory.
- New `EventType` value(s) if we want media start/stop in the existing `Event`
  stream too (e.g. `MediaStart`/`MediaStop`) — additive, nullable-friendly.

**Application** (CQRS):
- `LogActivityCommand` (writes `ConcurrencyEvent`s) and
  `EvaluateActivityQuery` → `OneOf<ActivityDirective, ValidationError>`, where
  `ActivityDirective = {Action(Allow|Nag|Mute|Block), Reason}`.
- `ConcurrencyPolicy` read/update commands for the options page.

**Api:** one new thin endpoint `POST /activity` → `EvaluateActivityQuery`
(maps directive to JSON). Sits alongside `/decision`, `/focus/*`.

**Extension (SW):** new `activity-monitor.ts` (or fold into `background.ts`):
subscribe to `tabs.onUpdated`/`onActivated` + `windows.onFocusChanged`; maintain
the current activity set; debounce; `POST /activity`; apply the directive
(mute via `tabs.update`, nag/block via a message to the content script). Reuse the
existing block-screen iframe for C1, add a lightweight banner for C3.

**Reports (M4 seam):** `ConcurrencyEvent` rollups → "single-tasking score",
"% of focus time with background media", which media domains break focus most.

---

## Recommended v1 slice (smallest thing worth shipping)

> **A1 + A2 detection → B1+B4 policy → C4 then C3 intervention → D2 backend path.**

1. **Measure (C4).** SW tracks active tab + audible tabs; posts `ConcurrencyEvent`s
   to `POST /activity`; backend logs them. No intervention yet — get the data first.
2. **Nag (C3).** When background media overlaps a focus session, show the
   pick-one banner; *Stop* mutes, *It's fine* logs a label + allowlists for the
   session. This is the label-collecting friction step.
3. **Strict mode (C2, opt-in).** A toggle in Options that auto-mutes background
   media during focus, with the B4 allowlist as the escape hatch.

Everything after that (B2 off-topic-only via ML, A4 desktop companion, C1
hard-block) is layered on without reworking the slice — same additive philosophy
the rest of the project uses.

## Roadmap placement
A new, mostly-independent work package — call it **WP-ST** — that depends only on
**WP1/WP2** (domain + CQRS + endpoint plumbing) and **WP4** (SW), and runs parallel
to the ML milestones. It *enriches* M4 reports rather than blocking them.

## Open questions
- **Strictness default:** ship as *Measure-only* first, or *Nag* out of the box?
- **"Background" definition:** audible-but-not-active-tab (A1) enough, or require
  A2's `visibilityState` to avoid false positives (e.g. picture-in-picture you're
  deliberately watching)?
- **Allowlist granularity (B4):** per-domain, per-URL, or a declared "focus
  playlist"? Per-session or persistent?
- **Mute vs. pause:** auto-mute is reliable; true pause needs per-site player
  hooks — worth it, or is mute enough to break the habit?
- **Desktop coverage (A4):** is web-only acceptable for v1, or is Spotify-desktop
  the main offender (which would push A4 up the priority list)?
- **Idle interaction:** if you walk away (`chrome.idle`), is background music still
  a "violation", or only when you're actively working elsewhere?
```
