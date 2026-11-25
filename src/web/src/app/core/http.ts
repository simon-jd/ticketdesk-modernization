import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { BrowserLocation, signInUrl } from './browser';

/** A 401 from the API means the sign-in cookie is gone (expired or signed out elsewhere): go to the sign-in page. */
export const signInWhenUnauthorized: HttpInterceptorFn = (req, next) => {
  const location = inject(BrowserLocation);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && req.url.startsWith('/api/')) {
        location.assign(signInUrl(location.pathAndQuery));
      }
      return throwError(() => error);
    }),
  );
};

/** Field errors of an ASP.NET Core validation problem (400 with `errors`), else null. */
export function validationErrors(error: unknown): Record<string, string[]> | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) return null;
  const errors = (error.error as { errors?: Record<string, string[]> } | null)?.errors;
  return errors && typeof errors === 'object' ? errors : null;
}

/** A short message for an API failure (problem details title when there is one). */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'The server could not be reached. Check your connection and try again.';
    if (error.status === 403) return 'You do not have permission to do this.';
    const title = (error.error as { title?: string } | null)?.title;
    return title ? title : `The server answered ${error.status}. Try again.`;
  }
  return 'Something went wrong. Try again.';
}
