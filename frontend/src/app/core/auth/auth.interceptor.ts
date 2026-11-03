import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

let refreshInFlight: ReturnType<AuthService['refresh']> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const withAuth =
    token && !req.url.includes('/api/v1/auth/login') && !req.url.includes('/api/v1/auth/register')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(withAuth).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401 || req.url.includes('/api/v1/auth/')) {
        return throwError(() => err);
      }

      if (!auth.session()?.refreshToken) {
        auth.clear();
        return throwError(() => err);
      }

      refreshInFlight ??= auth.refresh().pipe(
        catchError((refreshErr) => {
          auth.clear();
          return throwError(() => refreshErr);
        }),
      );

      return refreshInFlight.pipe(
        switchMap((tokens) => {
          refreshInFlight = null;
          return next(
            req.clone({ setHeaders: { Authorization: `Bearer ${tokens.accessToken}` } }),
          );
        }),
        catchError((refreshErr) => {
          refreshInFlight = null;
          return throwError(() => refreshErr);
        }),
      );
    }),
  );
};
