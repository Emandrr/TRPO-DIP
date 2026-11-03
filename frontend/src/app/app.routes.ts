import { Routes } from '@angular/router';
import { authGuard, changePasswordGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register.page').then((m) => m.RegisterPage),
  },
  {
    path: 'change-password',
    canActivate: [changePasswordGuard],
    loadComponent: () =>
      import('./features/auth/change-password/change-password.page').then((m) => m.ChangePasswordPage),
  },
  {
    path: 'patient',
    canActivate: [authGuard, roleGuard(['Patient'])],
    loadChildren: () => import('./features/patient/patient.routes').then((m) => m.PATIENT_ROUTES),
  },
  {
    path: 'doctor',
    canActivate: [authGuard, roleGuard(['Doctor'])],
    loadChildren: () => import('./features/doctor/doctor.routes').then((m) => m.DOCTOR_ROUTES),
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard(['Admin'])],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  { path: '**', redirectTo: 'login' },
];
