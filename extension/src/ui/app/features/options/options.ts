import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { storageGet, storageSet } from '../../core/chrome-storage';
import { DEFAULT_API_CONFIG, STORAGE_KEYS } from '../../core/config';
import { MatchKind, Rule, RuleKind } from '../../core/models';
import { RulesStore } from '../../state/rules.store';

@Component({
  selector: 'eb-options',
  imports: [FormsModule],
  templateUrl: './options.html',
  styleUrl: './options.css',
})
export class OptionsComponent implements OnInit {
  protected readonly store = inject(RulesStore);

  // New-rule form model.
  protected readonly pattern = signal('');
  protected readonly match = signal<MatchKind>('Domain');
  protected readonly kind = signal<RuleKind>('Block');
  protected readonly category = signal('');

  // API connection settings.
  protected readonly baseUrl = signal(DEFAULT_API_CONFIG.baseUrl);
  protected readonly token = signal(DEFAULT_API_CONFIG.token);
  protected readonly settingsSaved = signal(false);

  ngOnInit(): void {
    void this.hydrateSettings();
    void this.store.load();
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

  protected addRule(): void {
    const pattern = this.pattern().trim();
    if (!pattern) {
      return;
    }
    const rule: Rule = {
      pattern,
      match: this.match(),
      kind: this.kind(),
      source: 'manual',
      category: this.category().trim() || null,
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
}
