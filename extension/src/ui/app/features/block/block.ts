import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { ApiClient } from '../../core/api-client';
import { storageSet } from '../../core/chrome-storage';
import { STORAGE_KEYS } from '../../core/config';
import { hostOf, youTubeThumbnail } from '../../core/url-utils';

@Component({
  selector: 'eb-block',
  imports: [],
  templateUrl: './block.html',
  styleUrl: './block.css',
})
export class BlockComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiClient);

  protected readonly url = signal('');
  protected readonly title = signal('');
  protected readonly reason = signal('');
  /** YouTube thumbnail (via the video id in the URL), when applicable. */
  protected readonly thumbnail = signal<string | null>(null);
  /** The link becomes clickable only after "Bad call" is pressed. */
  protected readonly unlocked = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const url = params.get('url') ?? '';
    this.url.set(url);
    this.title.set(params.get('title') || hostOf(url) || url);
    this.reason.set(params.get('reason') || 'This page is on your block list.');
    this.thumbnail.set(youTubeThumbnail(url));
  }

  /** Confirm the block: records a `block` label and closes the iframe content. */
  protected async goodCall(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await this.api.sendFeedback({
        url: this.url(),
        title: this.title(),
        decision: 'block',
        source: 'good-call',
      });
    } catch {
      // Best-effort: even if the label POST fails we keep the block in place.
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * False positive: records an `allow` label, sets an allow-once flag so the
   * content script does not immediately re-block, then navigates the top frame
   * to the original URL for immediate access.
   */
  protected async badCall(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await this.api.sendFeedback({
        url: this.url(),
        title: this.title(),
        decision: 'allow',
        source: 'bad-call',
      });
    } catch {
      // Proceed even if the label POST fails — the user has overridden.
    }
    // Allow-once flag keyed by URL; the content script clears it after use.
    await storageSet(STORAGE_KEYS.allowOncePrefix + this.url(), Date.now());
    this.unlocked.set(true);
    this.busy.set(false);
    // Immediate access: drive the top frame to the original URL.
    if (typeof top !== 'undefined' && top) {
      top.location.href = this.url();
    } else {
      window.location.href = this.url();
    }
  }
}
