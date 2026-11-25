import { APP_BASE_HREF } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, TitleStrategy, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { signInWhenUnauthorized } from './core/http';
import { Session } from './core/session';
import { PageTitleStrategy } from './core/title';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // the assets live under /app (index.html's <base href>); the pages keep the legacy URLs from the site root
    { provide: APP_BASE_HREF, useValue: '/' },
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(
      withFetch(),
      // the page shell sets XSRF-TOKEN; state-changing /api calls send it back in the header the server validates
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'RequestVerificationToken' }),
      withInterceptors([signInWhenUnauthorized]),
    ),
    { provide: TitleStrategy, useClass: PageTitleStrategy },
    provideAppInitializer(() => inject(Session).load()),
  ],
};
