import { inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withMethods,
  withState,
} from '@ngrx/signals';

import { ApiClient } from '../core/api-client';
import { Category } from '../core/models';

interface CategoriesState {
  categories: Category[];
  loading: boolean;
  error: string | null;
}

const initialState: CategoriesState = {
  categories: [],
  loading: false,
  error: null,
};

/** Signal store backing the Options surface (category vocabulary CRUD). */
export const CategoriesStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, api = inject(ApiClient)) => ({
    async load(): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const categories = await api.listCategories();
        patchState(store, { categories, loading: false });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to load categories.',
        });
      }
    },

    async add(name: string): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const created = await api.addCategory(name);
        patchState(store, {
          categories: [...store.categories(), created],
          loading: false,
        });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to add category.',
        });
      }
    },

    async rename(id: string, name: string): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        const updated = await api.updateCategory(id, name);
        patchState(store, {
          categories: store.categories().map((c) => (c.id === id ? updated : c)),
          loading: false,
        });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to rename category.',
        });
      }
    },

    async remove(id: string): Promise<void> {
      patchState(store, { loading: true, error: null });
      try {
        await api.deleteCategory(id);
        patchState(store, {
          categories: store.categories().filter((c) => c.id !== id),
          loading: false,
        });
      } catch (e) {
        patchState(store, {
          loading: false,
          error: e instanceof Error ? e.message : 'Failed to delete category.',
        });
      }
    },
  })),
);
