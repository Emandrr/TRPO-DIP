import { Routes } from '@angular/router';

export const PATIENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./patient.layout').then((m) => m.PatientLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'card' },
      {
        path: 'card',
        loadComponent: () => import('./card/patient-card.page').then((m) => m.PatientCardPage),
      },
      {
        path: 'diagnoses',
        loadComponent: () =>
          import('./diagnoses/patient-diagnoses.page').then((m) => m.PatientDiagnosesPage),
      },
      {
        path: 'ecg',
        loadComponent: () => import('./ecg/patient-ecg.page').then((m) => m.PatientEcgPage),
      },
      {
        path: 'ecg/:id',
        loadComponent: () =>
          import('./ecg/patient-ecg-detail.page').then((m) => m.PatientEcgDetailPage),
      },
      {
        path: 'upload',
        loadComponent: () => import('./upload/patient-upload.page').then((m) => m.PatientUploadPage),
      },
    ],
  },
];
