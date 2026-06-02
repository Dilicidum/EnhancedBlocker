import { inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withMethods,
  withState,
} from '@ngrx/signals';

import { ApiClient } from '../core/api-client';
import { Rule } from '../core/models';

interface RulesState {
  rules: Rule[];
  loading: boolean;
  error: string | null;
}

const initialState: RulesState = {
  rules: [],
  loading: false,
  error: null,
};

/** Signal store backing the Options surface (Tier-0 rules CRUD). */
export const RulesStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, api = inject(ApiClient)) => ({
    async load(): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const rules = await api.listRules();
        patchState(store, { rules, loading: false });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to load rules.',
        });
      }
    },

    async add(rule: Rule): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const created = await api.addRule(rule);
        patchState(store, {
          rules: [...store.rules(), created],
          loading: false,
        });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to add rule.',
        });
      }
    },

    async remove(id: string): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        await api.deleteRule(id);
        patchState(store, {
          rules: store.rules().filter((r) => r.id !== id),
          loading: false,
        });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to delete rule.',
        });
      }
    },
  })),
);
