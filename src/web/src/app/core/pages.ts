import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { BrowserLocation } from './browser';
import { Session } from './session';

/**
 * Route guard: the Angular page answers only while the migration registry has it on Angular. Otherwise the route does not
 * match and the catch-all below sends the browser to the server, which answers with the server page or the legacy app.
 */
export function angularPage(pageId: string): CanMatchFn {
  return () => inject(Session).servesPage(pageId);
}

/**
 * Catch-all: any URL without an Angular page (or whose page is switched back) is loaded from the server. On the very first
 * navigation the server already chose this app for the URL, so reloading would loop; the page then explains and links instead.
 */
export const leaveForServer: CanMatchFn = () => {
  const router = inject(Router);
  const target = router.currentNavigation()?.finalUrl ?? router.currentNavigation()?.extractedUrl;
  if (!router.navigated || !target) return true;
  inject(BrowserLocation).assign(router.serializeUrl(target));
  // keep the navigation pending while the browser loads the server page (no flash of this app, no "no route" error)
  return new Promise<boolean>(() => undefined);
};
