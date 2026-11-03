import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EcgRecord, EcgReport } from '../../../core/api/api.models';
import { EcgApiService } from '../../../core/api/ecg-api.service';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { EcgChartComponent } from '../../../shared/ecg/ecg-chart.component';
import { ConfirmService } from '../../../shared/ui/confirm.service';
import {
  optionalMultilineValidator,
  requiredLineValidator,
  ruDateValidator,
  todayRuDate,
} from '../../../shared/validators/med.validators';

@Component({
  selector: 'app-doctor-ecg-detail-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    RouterLink,
    DatePipe,
    EcgChartComponent,
  ],
  templateUrl: './doctor-ecg-detail.page.html',
  styleUrl: './doctor-ecg-detail.page.scss',
})
export class DoctorEcgDetailPage implements OnInit {
  readonly t = RU;
  private readonly route = inject(ActivatedRoute);
  private readonly ecgApi = inject(EcgApiService);
  private readonly medicalApi = inject(MedicalApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);

  patientId = '';
  recordId = '';
  readonly record = signal<EcgRecord | null>(null);
  readonly report = signal<EcgReport | null>(null);
  readonly selectedAnomalies = signal<Set<string>>(new Set());
  readonly busy = signal(false);

  readonly decisionForm = this.fb.nonNullable.group({
    comment: ['', [optionalMultilineValidator()]],
  });

  readonly diagForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, requiredLineValidator()]],
    description: ['', [optionalMultilineValidator()]],
    diagnosedOn: [todayRuDate(), [Validators.required, ruDateValidator({ notFuture: true })]],
  });

  ngOnInit(): void {
    this.patientId = this.route.snapshot.paramMap.get('patientId') ?? '';
    this.recordId = this.route.snapshot.paramMap.get('recordId') ?? '';
    this.reload();
  }

  durationMs(): number {
    const report = this.report();
    if (report) {
      return Math.max(0, report.durationSec * 1000);
    }
    const record = this.record();
    if (!record) {
      return 0;
    }
    const start = Date.parse(record.startedAt);
    const end = Date.parse(record.endedAt);
    return Number.isFinite(start) && Number.isFinite(end) ? Math.max(0, end - start) : 0;
  }

  canDecide(): boolean {
    return this.report()?.status === 'Pending';
  }

  toggleAnomaly(id: string, checked: boolean): void {
    const next = new Set(this.selectedAnomalies());
    if (checked) {
      next.add(id);
    } else {
      next.delete(id);
    }
    this.selectedAnomalies.set(next);
  }

  accept(): void {
    this.confirm
      .ask({
        title: this.t.acceptConfirmTitle,
        message: this.t.acceptConfirmMessage,
        confirmText: this.t.accept,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.busy.set(true);
        this.ecgApi.acceptReport(this.recordId, this.decisionForm.controls.comment.value || null).subscribe({
          next: () => {
            this.busy.set(false);
            this.notifications.success(this.t.accepted);
            this.reload();
          },
          error: () => this.busy.set(false),
        });
      });
  }

  reject(): void {
    this.confirm
      .ask({
        title: this.t.rejectConfirmTitle,
        message: this.t.rejectConfirmMessage,
        confirmText: this.t.reject,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.busy.set(true);
        this.ecgApi.rejectReport(this.recordId, this.decisionForm.controls.comment.value || null).subscribe({
          next: () => {
            this.busy.set(false);
            this.notifications.success(this.t.rejected);
            this.reload();
          },
          error: () => this.busy.set(false),
        });
      });
  }

  createDiagnosis(): void {
    if (this.diagForm.invalid) {
      this.diagForm.markAllAsTouched();
      return;
    }
    const report = this.report();
    if (!report) {
      return;
    }
    const v = this.diagForm.getRawValue();
    const anomalyIds = [...this.selectedAnomalies()];
    this.medicalApi
      .createDiagnosis({
        patientId: this.patientId,
        title: v.title,
        description: v.description || null,
        diagnosedOn: v.diagnosedOn,
        recordId: this.recordId,
        reportId: report.id,
        anomalyIds: anomalyIds.length ? anomalyIds : null,
      })
      .subscribe({
        next: () => {
          this.diagForm.reset({ diagnosedOn: todayRuDate(), description: '' });
          this.selectedAnomalies.set(new Set());
          this.notifications.success(this.t.saved);
        },
      });
  }

  private reload(): void {
    this.ecgApi.getRecord(this.recordId).subscribe({
      next: (record) => this.record.set(record),
    });
    this.ecgApi.getReport(this.recordId).subscribe({
      next: (report) => this.report.set(report),
      error: () => this.report.set(null),
    });
  }
}
