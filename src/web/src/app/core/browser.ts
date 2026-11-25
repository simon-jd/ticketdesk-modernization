import { Injectable } from '@angular/core';

/** The browser's location, behind a service so tests can see where the app sends the user. */
@Injectable({ providedIn: 'root' })
export class BrowserLocation {
  /** Full page load: the server (registry) decides which app answers the URL. */
  assign(url: string): void {
    window.location.assign(url);
  }

  get pathAndQuery(): string {
    return window.location.pathname + window.location.search;
  }
}

/** The sign-in page with a ReturnUrl back to where the user was (legacy parameter name). */
export function signInUrl(returnTo: string): string {
  return '/user/sign-in?ReturnUrl=' + encodeURIComponent(returnTo);
}
