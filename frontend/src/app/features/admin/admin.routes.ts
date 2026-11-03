import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin.layout').then((m) => m.AdminLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      {
        path: 'users',
        loadComponent: () => import('./users/admin-users.page').then((m) => m.AdminUsersPage),
      },
      {
        path: 'assignments',
        loadComponent: () =>
          import('./assignments/admin-assignments.page').then((m) => m.AdminAssignmentsPage),
      },
      {
        path: 'devices',
        loadComponent: () => import('./devices/admin-devices.page').then((m) => m.AdminDevicesPage),
      },
      {
        path: 'logs',
        loadComponent: () => import('./logs/admin-logs.page').then((m) => m.AdminLogsPage),
      },
      {
        path: 'medical',
        loadComponent: () => import('./medical/admin-medical.page').then((m) => m.AdminMedicalPage),
      },
    ],
  },
];
