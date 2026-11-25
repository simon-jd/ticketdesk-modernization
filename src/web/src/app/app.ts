import { HttpXsrfTokenExtractor } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { Session } from './core/session';

/**
 * The page frame: skip link, the TicketDesk navigation (links to server pages are plain links, the Angular pages use the router),
 * the main region and the legacy footer. After an in-app navigation the focus moves to the new page (its heading, or the main
 * region while the heading is still loading), so keyboard and screen-reader users start at the content.
 */
@Component({
  selector: 'td-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly session = inject(Session);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');
  protected readonly year = new Date().getFullYear();
  private readonly xsrf = inject(HttpXsrfTokenExtractor);

  constructor() {
    const injector = inject(Injector);
    // only when the page changes (the path): a sort or filter change keeps the focus where the user is
    let path: string | null = null;
    inject(Router).events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe((e) => {
      const next = e.urlAfterRedirects.split(/[?#]/)[0];
      const changed = path !== null && next !== path;
      path = next;
      if (changed) afterNextRender(() => this.focusPage(), { injector });
    });
  }

  private focusPage(): void {
    const main = this.main().nativeElement;
    (main.querySelector<HTMLElement>('h1') ?? main).focus();
  }

  /** The antiforgery request token the page shell set (XSRF-TOKEN cookie); the sign-out form posts it (S3). */
  protected xsrfToken(): string {
    return this.xsrf.getToken() ?? '';
  }

  protected skipToMain(event: Event): void {
    // a fragment link would resolve against <base href="/app/">: move the focus instead
    event.preventDefault();
    this.focusPage();
  }
}
