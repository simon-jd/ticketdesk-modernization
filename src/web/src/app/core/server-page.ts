import { Component, inject } from '@angular/core';
import { BrowserLocation } from './browser';

/** Shown only when the first URL has no Angular page here (the registry changed between the page load and the session). */
@Component({
  selector: 'td-server-page',
  template: `
    <h1 tabindex="-1">This page opens from the server</h1>
    <p>This page is not part of the new front end right now.</p>
    <p><a [href]="url">Open {{ url }}</a> &middot; <a href="/tickets">Go to the ticket center</a></p>
  `,
})
export class ServerPage {
  protected readonly url = inject(BrowserLocation).pathAndQuery;
}
