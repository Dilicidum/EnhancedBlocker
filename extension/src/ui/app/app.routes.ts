import { Routes } from '@angular/router';

import { BlockComponent } from './features/block/block';
import { OptionsComponent } from './features/options/options';
import { PopupComponent } from './features/popup/popup';

/**
 * One Angular app, three surfaces. Each entry HTML (popup.html / options.html /
 * block.html) loads the same bundle; the router matches the surface from the
 * `.html` path so we don't depend on history rewriting inside the extension.
 */
export const routes: Routes = [
  { path: 'popup', component: PopupComponent },
  { path: 'popup.html', component: PopupComponent },
  { path: 'options', component: OptionsComponent },
  { path: 'options.html', component: OptionsComponent },
  { path: 'block', component: BlockComponent },
  { path: 'block.html', component: BlockComponent },
  { path: '', pathMatch: 'full', redirectTo: 'popup' },
  { path: '**', redirectTo: 'popup' },
];
