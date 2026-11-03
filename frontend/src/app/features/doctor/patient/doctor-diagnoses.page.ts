import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Diagnosis, Prescription, Recommendation } from '../../../core/api/api.models';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import {
  optionalMultilineValidator,
  prescriptionDatesValidator,
  requiredLineValidator,
  ruDateValidator,
  todayRuDate,
} from '../../../shared/validators/med.validators';

interface Bundle {
  diagnosis: Diagnosis;
  recommendations: Recommendation[];
  prescriptions: Prescription[];
}

@Component({
  selector: 'app-doctor-diagnoses-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './doctor-diagnoses.page.html',
  styleUrl: './doctor-diagnoses.page.scss',
})
export class DoctorDiagnosesPage implements OnInit {
  readonly t = RU;
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(MedicalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private patientId = '';
  readonly items = signal<Bundle[]>([]);
  readonly loading = signal(true);

  readonly diagForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, requiredLineValidator()]],
    description: ['', [optionalMultilineValidator()]],
    diagnosedOn: [todayRuDate(), [Validators.required, ruDateValidator({ notFuture: true })]],
  });

  readonly recForm = this.fb.nonNullable.group({
    text: ['', [Validators.required, Validators.maxLength(5000)]],
    comment: ['', [optionalMultilineValidator()]],
  });

  readonly rxForm = this.fb.nonNullable.group(
    {
      drugName: ['', [Validators.required, requiredLineValidator()]],
      startDate: [todayRuDate(), [Validators.required, ruDateValidator()]],
      endDate: [todayRuDate(), [Validators.required, ruDateValidator()]],
      comment: ['', [optionalMultilineValidator()]],
    },
    { validators: prescriptionDatesValidator('startDate', 'endDate') },
  );

  ngOnInit(): void {
    this.patientId = this.route.parent?.snapshot.paramMap.get('patientId') ?? '';
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.listDiagnoses(this.patientId).subscribe({
      next: (diagnoses) => {
        if (!diagnoses.length) {
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

  createDiagnosis(): void {
    if (this.diagForm.invalid) {
      this.diagForm.markAllAsTouched();
      return;
    }
    const v = this.diagForm.getRawValue();
    this.api
      .createDiagnosis({
        patientId: this.patientId,
        title: v.title,
        description: v.description || null,
        diagnosedOn: v.diagnosedOn,
      })
      .subscribe({
        next: () => {
          this.diagForm.reset({ diagnosedOn: todayRuDate(), description: '' });
          this.notifications.success(this.t.saved);
          this.reload();
        },
      });
  }

  deleteDiagnosis(id: string): void {
    this.confirm
      .ask({
        title: this.t.deleteConfirmTitle,
        message: this.t.deleteConfirmMessage,
        confirmText: this.t.delete,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.api.deleteDiagnosis(id).subscribe({
          next: () => {
            this.notifications.success(this.t.deleted);
            this.reload();
          },
        });
      });
  }

  addRecommendation(diagnosisId: string): void {
    if (this.recForm.invalid) {
      this.recForm.markAllAsTouched();
      return;
    }
    const v = this.recForm.getRawValue();
    this.api.createRecommendation(diagnosisId, { text: v.text, comment: v.comment || null }).subscribe({
      next: () => {
        this.recForm.reset();
        this.notifications.success(this.t.saved);
        this.reload();
      },
    });
  }

  deleteRecommendation(id: string): void {
    this.confirm
      .ask({
        title: this.t.deleteConfirmTitle,
        message: this.t.deleteConfirmMessage,
        confirmText: this.t.delete,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.api.deleteRecommendation(id).subscribe({
          next: () => {
            this.notifications.success(this.t.deleted);
            this.reload();
          },
        });
      });
  }

  addPrescription(diagnosisId: string): void {
    if (this.rxForm.invalid) {
      this.rxForm.markAllAsTouched();
      return;
    }
    const v = this.rxForm.getRawValue();
    this.api
      .createPrescription(diagnosisId, {
        drugName: v.drugName,
        startDate: v.startDate,
        endDate: v.endDate,
        comment: v.comment || null,
      })
      .subscribe({
        next: () => {
          this.rxForm.reset({ startDate: todayRuDate(), endDate: todayRuDate(), comment: '' });
          this.notifications.success(this.t.saved);
          this.reload();
        },
      });
  }

  deletePrescription(id: string): void {
    this.confirm
      .ask({
        title: this.t.deleteConfirmTitle,
        message: this.t.deleteConfirmMessage,
        confirmText: this.t.delete,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.api.deletePrescription(id).subscribe({
          next: () => {
            this.notifications.success(this.t.deleted);
            this.reload();
          },
        });
      });
  }
}
