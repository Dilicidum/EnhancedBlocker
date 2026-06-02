import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { storageGet } from './chrome-storage';
import { ApiConfig, DEFAULT_API_CONFIG, STORAGE_KEYS } from './config';
import {
  FeedbackPayload,
  NavEvent,
  Rule,
  StartFocusResponse,
} from './models';

/**
 * Thin typed HTTP client to the .NET backend. Reads the (configurable) base URL
 * and shared-secret token from chrome.storage.local, falling back to defaults,
 * and attaches the `X-EB-Token` header to every request.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  private async resolveConfig(): Promise<ApiConfig> {
    const baseUrl =
      (await storageGet<string>(STORAGE_KEYS.apiBaseUrl)) ??
      DEFAULT_API_CONFIG.baseUrl;
    const token =
      (await storageGet<string>(STORAGE_KEYS.apiToken)) ??
      DEFAULT_API_CONFIG.token;
    return { baseUrl, token };
  }

  private async headers(): Promise<HttpHeaders> {
    const cfg = await this.resolveConfig();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'X-EB-Token': cfg.token,
    });
  }

  private async url(path: string): Promise<string> {
    const cfg = await this.resolveConfig();
    return `${cfg.baseUrl.replace(/\/+$/, '')}${path}`;
  }

  // ---- Events ------------------------------------------------------------

  async logEvents(events: NavEvent[]): Promise<void> {
    await firstValueFrom(
      this.http.post(await this.url('/events'), events, {
        headers: await this.headers(),
      }),
    );
  }

  // ---- Feedback ----------------------------------------------------------

  async sendFeedback(payload: FeedbackPayload): Promise<void> {
    await firstValueFrom(
      this.http.post(await this.url('/feedback'), payload, {
        headers: await this.headers(),
      }),
    );
  }

  // ---- Rules -------------------------------------------------------------

  async listRules(): Promise<Rule[]> {
    return await firstValueFrom(
      this.http.get<Rule[]>(await this.url('/rules'), {
        headers: await this.headers(),
      }),
    );
  }

  async addRule(rule: Rule): Promise<Rule> {
    return await firstValueFrom(
      this.http.post<Rule>(await this.url('/rules'), rule, {
        headers: await this.headers(),
      }),
    );
  }

  async deleteRule(id: string): Promise<void> {
    await firstValueFrom(
      this.http.delete(await this.url(`/rules/${encodeURIComponent(id)}`), {
        headers: await this.headers(),
      }),
    );
  }

  // ---- Focus -------------------------------------------------------------

  async startFocus(intent: string): Promise<StartFocusResponse> {
    return await firstValueFrom(
      this.http.post<StartFocusResponse>(
        await this.url('/focus/start'),
        { intent },
        { headers: await this.headers() },
      ),
    );
  }

  async stopFocus(): Promise<void> {
    await firstValueFrom(
      this.http.post(
        await this.url('/focus/stop'),
        {},
        { headers: await this.headers() },
      ),
    );
  }
}
