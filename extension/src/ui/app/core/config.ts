import { InjectionToken } from '@angular/core';

/** Runtime configuration for talking to the .NET backend. */
export interface ApiConfig {
  /** Base URL of the .NET backend (loopback). */
  baseUrl: string;
  /** Shared-secret token sent as the `X-EB-Token` header on every request. */
  token: string;
}

/** Default config. The base is configurable; values may be overridden from chrome.storage.local. */
export const DEFAULT_API_CONFIG: ApiConfig = {
  baseUrl: 'http://127.0.0.1:5180',
  token: 'dev-token',
};

/**
 * Storage keys shared across the extension surfaces and the worker/content scripts.
 * Keep these in one place so the worker (plain TS) and the Angular UI agree.
 */
export const STORAGE_KEYS = {
  apiBaseUrl: 'eb.apiBaseUrl',
  apiToken: 'eb.apiToken',
  focusSessionId: 'eb.focusSessionId',
  focusIntent: 'eb.focusIntent',
  /** Prefix for allow-once flags, suffixed by the URL. */
  allowOncePrefix: 'eb.allowOnce:',
} as const;

export const API_CONFIG = new InjectionToken<ApiConfig>('EB_API_CONFIG');
