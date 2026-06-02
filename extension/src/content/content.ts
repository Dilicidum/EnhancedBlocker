/**
 * Content script (built by esbuild → IIFE), injected at document_start.
 *
 * Flow (implements the "checking…" seam):
 *  1. Immediately inject a framework-free full-page overlay ("checking…") so the
 *     underlying page never flashes before a decision is made.
 *  2. Ask the service worker for a decision (it calls POST /decision + logs).
 *  3. Allow   → remove the overlay (reveal the page).
 *     Block   → replace the overlay with an iframe to block.html.
 *     Pending → keep the "checking…" overlay until a later EB_DECISION arrives
 *               (this is the M2 seam; Tier-0 resolves instantly today).
 *
 * An allow-once flag (set by the block screen on "Bad call") short-circuits the
 * whole flow so the user is not immediately re-blocked.
 */

(() => {
  // Only run in the top frame; iframes (incl. our own block.html) are skipped.
  if (window.top !== window.self) {
    return;
  }

  const OVERLAY_ID = 'eb-checking-overlay';
  const BLOCK_FRAME_ID = 'eb-block-frame';
  const ALLOW_ONCE_PREFIX = 'eb.allowOnce:';

  type Outcome = 'Allow' | 'Block' | 'Pending';
  interface DecisionRelay {
    outcome: Outcome;
    reason: string;
    url: string;
  }

  // ---- Overlay -------------------------------------------------------------

  function injectOverlay(): void {
    if (document.getElementById(OVERLAY_ID)) {
      return;
    }
    const overlay = document.createElement('div');
    overlay.id = OVERLAY_ID;
    overlay.setAttribute(
      'style',
      [
        'position:fixed',
        'inset:0',
        'z-index:2147483647',
        'background:#1f1f23',
        'color:#f5f5f5',
        'display:flex',
        'align-items:center',
        'justify-content:center',
        'font-family:system-ui,sans-serif',
        'font-size:16px',
      ].join(';'),
    );
    overlay.textContent = 'checking…';
    // documentElement exists at document_start even before <body>.
    (document.body ?? document.documentElement).appendChild(overlay);
  }

  function removeOverlay(): void {
    document.getElementById(OVERLAY_ID)?.remove();
    document.getElementById(BLOCK_FRAME_ID)?.remove();
  }

  function showBlockScreen(url: string, reason: string): void {
    // Idempotent: drop any prior block frame so repeated blocks (e.g. via SPA
    // navigation) don't stack iframes.
    document.getElementById(BLOCK_FRAME_ID)?.remove();
    document.getElementById(OVERLAY_ID)?.remove();
    const iframe = document.createElement('iframe');
    iframe.id = BLOCK_FRAME_ID;
    const params = new URLSearchParams({
      url,
      reason,
      title: document.title || '',
    });
    iframe.src = chrome.runtime.getURL(`block.html?${params.toString()}`);
    iframe.setAttribute(
      'style',
      'position:fixed;inset:0;width:100vw;height:100vh;border:0;z-index:2147483647;',
    );
    (document.body ?? document.documentElement).appendChild(iframe);
  }

  // ---- Allow-once ----------------------------------------------------------

  async function consumeAllowOnce(url: string): Promise<boolean> {
    const key = ALLOW_ONCE_PREFIX + url;
    const res = await chrome.storage.local.get(key);
    if (res[key] != null) {
      // One-shot: clear it so a later genuine navigation is re-evaluated.
      await chrome.storage.local.remove(key);
      return true;
    }
    return false;
  }

  // ---- Decision handling ---------------------------------------------------

  function applyDecision(relay: DecisionRelay): void {
    switch (relay.outcome) {
      case 'Allow':
        removeOverlay();
        break;
      case 'Block':
        showBlockScreen(relay.url || location.href, relay.reason);
        break;
      case 'Pending':
        // Keep the "checking…" overlay; a follow-up EB_DECISION resolves it.
        injectOverlay();
        break;
    }
  }

  async function requestDecision(): Promise<void> {
    if (await consumeAllowOnce(location.href)) {
      removeOverlay();
      return;
    }
    try {
      const relay: DecisionRelay = await chrome.runtime.sendMessage({
        type: 'EB_REQUEST_DECISION',
        url: location.href,
        title: document.title || null,
      });
      applyDecision(relay ?? { outcome: 'Allow', reason: 'no response', url: location.href });
    } catch {
      // Worker unreachable → fail open so the page is usable.
      removeOverlay();
    }
  }

  // The worker may also push a decision (e.g. resolving a prior Pending, or a
  // navigation it observed first).
  chrome.runtime.onMessage.addListener((msg) => {
    if (msg?.type === 'EB_DECISION') {
      void (async () => {
        if (await consumeAllowOnce(msg.url || location.href)) {
          removeOverlay();
          return;
        }
        applyDecision(msg as DecisionRelay);
      })();
    }
  });

  // ---- Boot ----------------------------------------------------------------

  injectOverlay();
  void requestDecision();
})();
