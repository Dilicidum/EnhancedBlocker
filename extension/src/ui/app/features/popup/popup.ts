import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { FocusStore } from '../../state/focus.store';

@Component({
  selector: 'eb-popup',
  imports: [FormsModule],
  templateUrl: './popup.html',
  styleUrl: './popup.css',
})
export class PopupComponent implements OnInit {
  protected readonly store = inject(FocusStore);

  ngOnInit(): void {
    void this.store.hydrate();
  }

  protected onIntentInput(value: string): void {
    this.store.setIntent(value);
  }

  protected toggle(): void {
    if (this.store.active()) {
      void this.store.stop();
    } else {
      void this.store.start();
    }
  }

  /** Opens the full settings/options page (manifest options_page). */
  protected openSettings(): void {
    chrome.runtime.openOptionsPage();
  }
}
