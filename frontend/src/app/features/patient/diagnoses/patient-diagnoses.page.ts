import { Component, OnInit, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Diagnosis, Prescription, Recommendation } from '../../../core/api/api.models';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { RU } from '../../../core/i18n/ru';

interface DiagnosisBundle {
  diagnosis: Diagnosis;
  recommendations: Recommendation[];
  prescriptions: Prescription[];
}

@Component({
  selector: 'app-patient-diagnoses-page',
  imports: [MatCardModule, MatExpansionModule],
  templateUrl: './patient-diagnoses.page.html',
  styleUrl: './patient-diagnoses.page.scss',
})
export class PatientDiagnosesPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(MedicalApiService);
  private readonly auth = inject(AuthService);
  readonly items = signal<DiagnosisBundle[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    const patientId = this.auth.session()?.userId;
    if (!patientId) {
      this.loading.set(false);
      return;
    }

    this.api.listDiagnoses(patientId).subscribe({
      next: (diagnoses) => {
        if (diagnoses.length === 0) {
          this.items.set([]);
          this.loading.set(false);
          return;
        }

        forkJoin(
          diagnoses.map((diagnosis) =>
            forkJoin({
              diagnosis: of(diagnosis),
              recommendations: this.api.listRecommendations(diagnosis.id).pipe(catchError(() => of([]))),
              prescriptions: this.api.listPrescriptions(diagnosis.id).pipe(catchError(() => of([]))),
            }),
          ),
        ).subscribe({
          next: (bundles) => {
            this.items.set(bundles);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }
}
