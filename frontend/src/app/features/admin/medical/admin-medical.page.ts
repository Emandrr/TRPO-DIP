import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { AdminApiService, AdminUser } from '../../../core/api/admin-api.service';
import { Diagnosis, MedicalCard } from '../../../core/api/api.models';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';

@Component({
  selector: 'app-admin-medical-page',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  templateUrl: './admin-medical.page.html',
  styleUrl: './admin-medical.page.scss',
})
export class AdminMedicalPage implements OnInit {
  readonly t = RU;
  private readonly adminApi = inject(AdminApiService);
  private readonly medicalApi = inject(MedicalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);

  readonly patients = signal<AdminUser[]>([]);
  readonly card = signal<MedicalCard | null>(null);
  readonly diagnoses = signal<Diagnosis[]>([]);

  readonly form = this.fb.nonNullable.group({
    patientId: ['', Validators.required],
  });

  ngOnInit(): void {
    this.adminApi.listUsers({ role: 'Patient' }).subscribe({
      next: (page) => this.patients.set(page.items),
    });
    this.form.controls.patientId.valueChanges.subscribe((id) => {
      if (id) {
        this.load(id);
      }
    });
  }

  load(patientId: string): void {
    this.medicalApi.getMedicalCard(patientId).subscribe({
      next: (card) => this.card.set(card),
      error: () => this.card.set(null),
    });
    this.medicalApi.listDiagnoses(patientId).subscribe({
      next: (items) => this.diagnoses.set(items),
      error: () => this.diagnoses.set([]),
    });
  }

  deleteCard(): void {
    const patientId = this.form.controls.patientId.value;
    if (!patientId) {
      return;
    }
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
        this.medicalApi.deleteMedicalCard(patientId).subscribe({
          next: () => {
            this.card.set(null);
            this.notifications.success(this.t.deleted);
          },
        });
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
        this.medicalApi.deleteDiagnosis(id).subscribe({
          next: () => {
            this.notifications.success(this.t.deleted);
            this.load(this.form.controls.patientId.value);
          },
        });
      });
  }
}
