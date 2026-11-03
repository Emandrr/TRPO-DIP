import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import {
  requiredLineValidator,
  ruDateValidator,
  sexRuValidator,
} from '../../../shared/validators/med.validators';

@Component({
  selector: 'app-doctor-card-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './doctor-card.page.html',
  styleUrl: './doctor-card.page.scss',
})
export class DoctorCardPage implements OnInit {
  readonly t = RU;
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(MedicalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);
  private patientId = '';
  readonly exists = signal(false);
  readonly loading = signal(true);
  readonly busy = signal(false);

  readonly form = this.fb.nonNullable.group({
    lastName: ['', [Validators.required, requiredLineValidator()]],
    firstName: ['', [Validators.required, requiredLineValidator()]],
    middleName: [''],
    birthDate: ['', [Validators.required, ruDateValidator({ notFuture: true })]],
    sex: ['мужской', [Validators.required, sexRuValidator()]],
  });

  ngOnInit(): void {
    this.patientId = this.route.parent?.snapshot.paramMap.get('patientId') ?? '';
    this.api.getMedicalCard(this.patientId).subscribe({
      next: (card) => {
        this.exists.set(true);
        this.form.patchValue({
          lastName: card.lastName ?? '',
          firstName: card.firstName ?? '',
          middleName: card.middleName ?? '',
          birthDate: card.birthDate,
          sex: card.sex,
        });
        this.loading.set(false);
      },
      error: () => {
        this.exists.set(false);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const v = this.form.getRawValue();
    this.api
      .upsertMedicalCard(this.patientId, {
        lastName: v.lastName,
        firstName: v.firstName,
        middleName: v.middleName || null,
        birthDate: v.birthDate,
        sex: v.sex,
      })
      .subscribe({
        next: () => {
          this.exists.set(true);
          this.busy.set(false);
          this.notifications.success(this.t.saved);
        },
        error: () => this.busy.set(false),
      });
  }

  remove(): void {
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
        this.api.deleteMedicalCard(this.patientId).subscribe({
          next: () => {
            this.exists.set(false);
            this.form.reset({ sex: 'мужской' });
            this.notifications.success(this.t.deleted);
          },
        });
      });
  }
}
