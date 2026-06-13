import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { storageGet, storageSet } from '../../core/chrome-storage';
import { DEFAULT_API_CONFIG, STORAGE_KEYS } from '../../core/config';
import { Category, MatchKind, Rule, RuleKind } from '../../core/models';
import { CategoriesStore } from '../../state/categories.store';
import { RulesStore } from '../../state/rules.store';

@Component({
  selector: 'eb-options',
  imports: [FormsModule],
  templateUrl: './options.html',
  styleUrl: './options.css',
})
export class OptionsComponent implements OnInit {
  protected readonly store = inject(RulesStore);
  protected readonly categories = inject(CategoriesStore);

  // New-rule form model.
  protected readonly pattern = signal('');
  protected readonly match = signal<MatchKind>('Domain');
  protected readonly kind = signal<RuleKind>('Block');
  protected readonly category = signal('');

  // New-category form model.
  protected readonly newCategory = signal('');

  // API connection settings.
  protected readonly baseUrl = signal(DEFAULT_API_CONFIG.baseUrl);
  protected readonly token = signal(DEFAULT_API_CONFIG.token);
  protected readonly settingsSaved = signal(false);

  ngOnInit(): void {
    void this.hydrateSettings();
    void this.store.load();
    void this.categories.load();
  }

  private async hydrateSettings(): Promise<void> {
    this.baseUrl.set(
      (await storageGet<string>(STORAGE_KEYS.apiBaseUrl)) ??
        DEFAULT_API_CONFIG.baseUrl,
    );
    this.token.set(
      (await storageGet<string>(STORAGE_KEYS.apiToken)) ??
        DEFAULT_API_CONFIG.token,
    );
  }

  protected async saveSettings(): Promise<void> {
    await storageSet(STORAGE_KEYS.apiBaseUrl, this.baseUrl().trim());
    await storageSet(STORAGE_KEYS.apiToken, this.token().trim());
    this.settingsSaved.set(true);
    setTimeout(() => this.settingsSaved.set(false), 2000);
  }

  // ---- Rules ----

  protected addRule(): void {
    const pattern = this.pattern().trim();
    if (!pattern) {
      return;
    }
    const category = this.category().trim();
    const rule: Rule = {
      pattern,
      match: this.match(),
      kind: this.kind(),
      source: 'manual',
      category: category || null,
    };
    void this.store.add(rule).then(() => {
      this.pattern.set('');
      this.category.set('');
    });
  }

  protected remove(rule: Rule): void {
    if (rule.id) {
      void this.store.remove(rule.id);
    }
  }

  // ---- Categories ----

  protected addCategory(): void {
    const name = this.newCategory().trim();
    if (!name) {
      return;
    }
    void this.categories.add(name).then(() => this.newCategory.set(''));
  }

  protected renameCategory(cat: Category, name: string): void {
    const trimmed = name.trim();
    if (cat.id && trimmed && trimmed !== cat.name) {
      void this.categories.rename(cat.id, trimmed);
    }
  }

  protected removeCategory(cat: Category): void {
    if (cat.id) {
      void this.categories.remove(cat.id);
    }
  }
}
