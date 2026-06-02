import { computed, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withState,
} from '@ngrx/signals';

import { ApiClient } from '../core/api-client';
import { storageGet, storageRemove, storageSet } from '../core/chrome-storage';
import { STORAGE_KEYS } from '../core/config';

interface FocusState {
  focusSessionId: string | null;
  intent: string;
  loading: boolean;
  error: string | null;
}

const initialState: FocusState = {
  focusSessionId: null,
  intent: '',
  loading: false,
  error: null,
};

/** Signal store for the focus-session toggle + declared intent (popup surface). */
export const FocusStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    active: computed(() => store.focusSessionId() !== null),
  })),
  withMethods((store, api = inject(ApiClient)) => ({
    /** Hydrate from chrome.storage.local so the toggle reflects the live session. */
    async hydrate(): Promise<void> {
      const id = await storageGet<string>(STORAGE_KEYS.focusSessionId);
      const intent =
        (await storageGet<string>(STORAGE_KEYS.focusIntent)) ?? '';
      patchState(store, { focusSessionId: id ?? null, intent });
    },

    setIntent(intent: string): void {
      patchState(store, { intent });
    },

    async start(): Promise<void> {
      const intent = store.intent().trim();
      if (!intent) {
        patchState(store, { error: 'Declare an intent first.' });
        return;
      }
      patchState(store, { loading: true, error: null });
      try {
        const { focusSessionId } = await api.startFocus(intent);
        await storageSet(STORAGE_KEYS.focusSessionId, focusSessionId);
        await storageSet(STORAGE_KEYS.focusIntent, intent);
        patchState(store, { focusSessionId, loading: false });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to start focus.',
        });
      }
    },

    async stop(): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        await api.stopFocus();
        await storageRemove(STORAGE_KEYS.focusSessionId);
        patchState(store, { focusSessionId: null, loading: false });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to stop focus.',
        });
      }
    },
  })),
);
