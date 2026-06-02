import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    // Default (path) location: each entry HTML is its own file
    // (popup.html / options.html / block.html). The router matches the surface
    // from that `.html` path, and block.html's `?url=&reason=` query params are
    // read straight from location.search. We never navigate between surfaces,
    // so no history rewriting is involved.
    provideRouter(routes),
  ],
};
