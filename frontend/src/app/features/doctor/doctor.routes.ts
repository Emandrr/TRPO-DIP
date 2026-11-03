import { Routes } from '@angular/router';

export const DOCTOR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./doctor.layout').then((m) => m.DoctorLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'patients' },
      {
        path: 'patients',
        loadComponent: () => import('./patients/doctor-patients.page').then((m) => m.DoctorPatientsPage),
      },
      {
        path: 'patients/:patientId',
        loadComponent: () => import('./patient/doctor-patient.layout').then((m) => m.DoctorPatientLayout),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'card' },
          {
            path: 'card',
            loadComponent: () => import('./patient/doctor-card.page').then((m) => m.DoctorCardPage),
          },
          {
            path: 'diagnoses',
            loadComponent: () => import('./patient/doctor-diagnoses.page').then((m) => m.DoctorDiagnosesPage),
          },
          {
            path: 'ecg',
            loadComponent: () => import('./patient/doctor-ecg.page').then((m) => m.DoctorEcgPage),
          },
        ],
      },
      {
        path: 'patients/:patientId/ecg/:recordId',
        loadComponent: () =>
          import('./patient/doctor-ecg-detail.page').then((m) => m.DoctorEcgDetailPage),
      },
    ],
  },
];
