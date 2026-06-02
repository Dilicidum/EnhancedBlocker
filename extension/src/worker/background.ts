/**
 * MV3 service worker (built by esbuild → ESM, "type":"module").
 *
 * Responsibilities:
 *  - observe navigations via chrome.webNavigation + chrome.tabs.onUpdated
 *  - log navigation events to POST /events
 *  - call POST /decision per navigation and relay the outcome to the content
 *    script so it can reveal / block / keep "checking…".
 *
 * The worker is the single place that talks to the backend on the hot path, so
 * the content script never needs host permissions or the token.
 */

// ---- Shared config (kept in sync with src/ui/app/core/config.ts) ----------

const DEFAULT_BASE_URL = 'http://127.0.0.1:5180';
const DEFAULT_TOKEN = 'dev-token';

const STORAGE_KEYS = {
  apiBaseUrl: 'eb.apiBaseUrl',
  apiToken: 'eb.apiToken',
  focusSessionId: 'eb.focusSessionId',
  intent: 'eb.focusIntent',
} as const;

type Outcome = 'Allow' | 'Block' | 'Pending';

interface TierResult {
  outcome: Outcome;
  tier: string;
  reason: string;
  score?: number | null;
}

interface DecisionRelay {
  outcome: Outcome;
  reason: string;
  url: string;
}

// ---- Config resolution -----------------------------------------------------

async function resolveConfig(): Promise<{ baseUrl: string; token: string }> {
  const res = await chrome.storage.local.get([
    STORAGE_KEYS.apiBaseUrl,
    STORAGE_KEYS.apiToken,
  ]);
  return {
    baseUrl: (res[STORAGE_KEYS.apiBaseUrl] as string) ?? DEFAULT_BASE_URL,
    token: (res[STORAGE_KEYS.apiToken] as string) ?? DEFAULT_TOKEN,
  };
}

async function apiFetch(path: string, body: unknown): Promise<Response> {
  const { baseUrl, token } = await resolveConfig();
  return fetch(`${baseUrl.replace(/\/+$/, '')}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-EB-Token': token,
    },
    body: JSON.stringify(body),
  });
}

function domainOf(url: string): string {
  try {
    return new URL(url).hostname;
  } catch {
    return '';
  }
}

function isTrackable(url: string | undefined): url is string {
  if (!url) {
    return false;
  }
  return url.startsWith('http://') || url.startsWith('https://');
}

async function currentFocus(): Promise<{
  focusSessionId: string | null;
  intent: string | null;
}> {
  const res = await chrome.storage.local.get([
    STORAGE_KEYS.focusSessionId,
    STORAGE_KEYS.intent,
  ]);
  return {
    focusSessionId: (res[STORAGE_KEYS.focusSessionId] as string) ?? null,
    intent: (res[STORAGE_KEYS.intent] as string) ?? null,
  };
}

// ---- Event logging ---------------------------------------------------------

async function logNavigation(
  url: string,
  tabId: number,
  title: string | null,
): Promise<void> {
  const { focusSessionId } = await currentFocus();
  const event = {
    ts: new Date().toISOString(),
    url,
    domain: domainOf(url),
    title,
    tabId,
    type: 'navigate' as const,
    focusSessionId,
    durationMs: null,
  };
  try {
    await apiFetch('/events', [event]);
  } catch {
    // Best-effort logging — never block navigation on a logging failure.
  }
}

// ---- Decision --------------------------------------------------------------

async function decide(
  url: string,
  title: string | null,
): Promise<DecisionRelay> {
  const { focusSessionId, intent } = await currentFocus();
  const ctx = {
    url,
    domain: domainOf(url),
    title,
    text: null,
    focusSessionId,
    intent,
    now: new Date().toISOString(),
  };
  try {
    const res = await apiFetch('/decision', ctx);
    if (!res.ok) {
      // Fail open: if the backend errors, do not trap the user.
      return { outcome: 'Allow', reason: 'decision endpoint error', url };
    }
    const result = (await res.json()) as TierResult;
    return { outcome: result.outcome, reason: result.reason, url };
  } catch {
    // Backend unreachable → fail open (allow) so the browser stays usable.
    return { outcome: 'Allow', reason: 'backend unreachable', url };
  }
}

/** Send the decision outcome to the content script in the given tab. */
function relayToTab(tabId: number, relay: DecisionRelay): void {
  chrome.tabs
    .sendMessage(tabId, { type: 'EB_DECISION', ...relay })
    .catch(() => {
      // Content script may not be present (e.g. tab closed / restricted page).
    });
}

// ---- Chrome event wiring ---------------------------------------------------

// Full-page navigations: the content script (re)loads on these and drives the
// decision itself (it has the page title and owns the "checking…" overlay), so
// here we only log the event to avoid a duplicate /decision call.
chrome.webNavigation.onCommitted.addListener((details) => {
  if (details.frameId !== 0 || !isTrackable(details.url)) {
    return; // top frame, http(s) only
  }
  void logNavigation(details.url, details.tabId, null);
});

// SPA / in-page history navigations do NOT reload the content script, so the
// worker logs *and* decides here, pushing the outcome to the content script.
chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
  if (details.frameId !== 0 || !isTrackable(details.url)) {
    return;
  }
  const { tabId, url } = details;
  void logNavigation(url, tabId, null);
  void decide(url, null).then((relay) => relayToTab(tabId, relay));
});

// The content script asks for a decision once it has the page title. This is the
// request/response path the "checking…" overlay awaits on full-page loads.
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg?.type === 'EB_REQUEST_DECISION' && sender.tab?.id != null) {
    const url: string = msg.url ?? sender.tab.url ?? '';
    const title: string | null = msg.title ?? sender.tab.title ?? null;
    if (!isTrackable(url)) {
      sendResponse({ outcome: 'Allow', reason: 'untracked scheme', url });
      return false;
    }
    // Logging for full-page loads is handled by webNavigation.onCommitted; here
    // we only (re)decide now that the content script has the page title.
    void decide(url, title).then((relay) => sendResponse(relay));
    return true; // keep the message channel open for the async response
  }
  return false;
});
