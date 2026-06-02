# EnhancedBlocker — Chrome Extension (M1)

Angular 21 + Chrome MV3 extension: the tracking + Tier-0 blocker front end. One
Angular app serves three surfaces (popup, options, block screen); a service
worker and a content script (both bundled with esbuild) handle the per-navigation
decision flow against the local .NET backend.

## Layout

```
src/
  ui/                  Angular app (standalone components, NgRx Signals)
    app/core/          typed HTTP client, models, config, chrome.storage helper
    app/state/         signal stores (focus, rules)
    app/features/      popup / options / block surfaces
    pages/             popup.html · options.html · block.html (entry HTMLs)
  worker/background.ts MV3 service worker (esbuild → ESM)
  content/content.ts   content script @ document_start (esbuild → IIFE)
  manifest.json        MV3 manifest (stable id via `key`)
tools/build-extension.mjs   ng build + esbuild + asset copy → dist/
icons/                 extension icons (16/48/128)
```

## Build

```bash
npm install
npm run build:ext
```

This runs `ng build` (production, `outputHashing: none` for stable filenames),
bundles `background.ts` and `content.ts` with esbuild, then assembles everything
into `dist/`:

```
dist/
  manifest.json
  background.js        service worker
  content.js           content script
  main.js  styles.css  Angular UI bundle
  popup.html  options.html  block.html
  icons/
```

Other scripts:

- `npm test` — unit tests (vitest/jsdom, headless; no browser needed).
- `npm run typecheck:scripts` — type-checks the worker + content scripts.

## Load unpacked in Chrome

1. `npm run build:ext`
2. Open `chrome://extensions`, enable **Developer mode**.
3. **Load unpacked** → select the `dist/` folder.

The `key` in `manifest.json` pins a stable extension id
(`efhomhlkheioedgdgpjpjieldmgdfhcj`) so the backend CORS allowlist stays valid
across reloads.

## Backend API base + token config

The extension talks to the .NET backend over loopback and sends a shared-secret
`X-EB-Token` header on every request.

- **Defaults:** base URL `http://127.0.0.1:5180`, token `dev-token`
  (see `src/ui/app/core/config.ts`).
- **Override at runtime:** open the **Options** page and edit *API base URL* and
  *X-EB-Token*, then **Save settings**. Values are stored in
  `chrome.storage.local` (`eb.apiBaseUrl`, `eb.apiToken`) and read by both the
  Angular UI and the service worker.

The backend must allow the extension origin
`chrome-extension://efhomhlkheioedgdgpjpjieldmgdfhcj` in CORS and accept the
configured token.

## Decision / block flow

1. Content script injects a framework-free **"checking…"** overlay at
   `document_start` (no page flash).
2. It asks the service worker for a decision; the worker logs an event
   (`POST /events`) and calls `POST /decision`.
3. **Allow** → overlay removed. **Block** → overlay replaced by an iframe to
   `block.html?url=…&reason=…`. **Pending** → overlay stays until a follow-up
   decision arrives (the M2 seam — works today with instant Tier-0).
4. On the block screen, **Good call** posts a `block` label; **Bad call (false
   positive)** posts an `allow` label, sets an allow-once flag in
   `chrome.storage.local`, and navigates the top frame to the original URL. The
   content link is non-clickable until **Bad call** is pressed; the allow-once
   flag stops an immediate re-block loop.
