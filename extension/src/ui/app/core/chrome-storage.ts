/**
 * Thin promise wrapper over chrome.storage.local with a safe fallback to
 * window.localStorage (useful when a surface is opened outside the extension
 * context, e.g. plain `ng serve` during development).
 */

function hasChromeStorage(): boolean {
  return (
    typeof chrome !== 'undefined' &&
    !!chrome.storage &&
    !!chrome.storage.local
  );
}

export async function storageGet<T>(key: string): Promise<T | undefined> {
  if (hasChromeStorage()) {
    const res = await chrome.storage.local.get(key);
    return res[key] as T | undefined;
  }
  const raw = globalThis.localStorage?.getItem(key);
  return raw == null ? undefined : (JSON.parse(raw) as T);
}

export async function storageSet(key: string, value: unknown): Promise<void> {
  if (hasChromeStorage()) {
    await chrome.storage.local.set({ [key]: value });
    return;
  }
  globalThis.localStorage?.setItem(key, JSON.stringify(value));
}

export async function storageRemove(key: string): Promise<void> {
  if (hasChromeStorage()) {
    await chrome.storage.local.remove(key);
    return;
  }
  globalThis.localStorage?.removeItem(key);
}
