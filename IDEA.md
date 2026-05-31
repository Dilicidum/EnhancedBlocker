# EnhancedBlocker — Idea & Vision

> Status: brainstorming / design exploration. Nothing built yet.
> Last updated: 2026-06-01

## The problem (in my own words)

I want to block myself, while I'm working, from sites I don't want to visit.

- **Some sites I already know** I don't want to see → I can block those statically with a Chrome extension.
- **But the harder problem**: I want to *dynamically* understand which sites / URLs / specific YouTube videos I shouldn't visit — including ones I haven't thought of in advance. This is what needs intelligence (an agentic workflow), not a hardcoded list.

The nuance that makes hardcoding impossible:

- I **don't** want to block *all* of YouTube. I want to block YouTube when the **content** is off-topic for what I'm doing (e.g. non-work topics while working).
- Some categories I never want during the day, **even when I'm not working** — e.g. **news**.
- Whether something should be blocked depends on **context** (am I working? on what?) and on **content** (what is this video/article actually about?), not just the domain. That's genuinely hard to express as static rules.

## The three parts

1. **The blocker itself** (build this first).
   Context-aware blocking. Not just a blocklist — a system that decides whether a given page/video/URL should be allowed right now, given what I'm doing. (See ARCHITECTURE.md for the classifier-based approach.)

2. **Time tracking — where did my day actually go?**
   I suspect I don't spend much time actually working, but I don't have the data. I want to see where my time goes, what I did.

3. **Reports.**
   Summaries of my time/behavior — daily/weekly, ideally with insight, not just raw numbers.

## Key decisions made so far

- **"Working" signal = manual toggle.** I press a "focus session" button to tell the system I'm working, and give a short declared intent. (Simplest, reliable, day-one.) Auto-detection can come later.
- **Classifier-first, not "agentic" for the blocker.** The per-page decision is a principled local classifier; an LLM is an edge component, not the core. ("Agentic" only makes sense for the Part 3 coach.)
- **Local-first.** Page content stays on-device.
- **Hard rules** in front for the ~20 already-known-bad URLs; embeddings generalize to unknown-but-similar ones.
- **Few-shot / zero-shot from day one** — no expectation of a large training set.
- **First deliverable = tracking + the blocker.**

→ Full design in [ARCHITECTURE.md](ARCHITECTURE.md).

## Open questions / to refine

- Scope of distractions (web-only vs. web + desktop apps) — TBD; leaning web-first since most distractions are browser-based.
- Block style when uncertain (hard-block + override vs. soft-friction vs. ask) — undecided.
- Where the classifier runs (in-extension WASM vs. small local companion service) — see ARCHITECTURE.md open questions.
