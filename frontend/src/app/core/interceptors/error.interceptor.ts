import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RU } from '../i18n/ru';
import { NotificationService } from '../notifications/notification.service';

interface ApiErrorBody {
  code?: string;
  message?: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) {
        notifications.error(RU.unexpectedError);
        return throwError(() => err);
      }

      const body = err.error as ApiErrorBody | null;
      if (body?.code === 'PASSWORD_CHANGE_REQUIRED') {
        void router.navigateByUrl('/change-password');
        return throwError(() => err);
      }

      if (err.status === 401 && !req.url.includes('/api/v1/auth/login')) {
        auth.clear();
        void router.navigateByUrl('/login');
      }

      const message = body?.message?.trim() || RU.unexpectedError;
      const silent404 = err.status === 404;
      const authLoginFail = err.status === 401 && req.url.includes('/api/v1/auth/login');
      if (!silent404 && (authLoginFail || err.status !== 401)) {
        notifications.error(message);
      }

      return throwError(() => err);
    }),
  );
};
