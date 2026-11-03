import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { UserRole } from './auth.models';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  if (auth.mustChangePassword()) {
    return router.createUrlTree(['/change-password']);
  }
  return true;
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return true;
  }
  if (auth.mustChangePassword()) {
    return router.createUrlTree(['/change-password']);
  }
  return router.createUrlTree([auth.homePathForRole(auth.role())]);
};

export const changePasswordGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  return true;
};

export const roleGuard = (roles: UserRole[]): CanActivateFn => {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }
    if (auth.mustChangePassword()) {
      return router.createUrlTree(['/change-password']);
    }
    const role = auth.role();
    if (!role || !roles.includes(role)) {
      return router.createUrlTree([auth.homePathForRole(role)]);
    }
    return true;
  };
};
