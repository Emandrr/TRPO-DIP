import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { ApiError, Device } from '../../../core/api/api.models';
import { EcgApiService } from '../../../core/api/ecg-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';

@Component({
  selector: 'app-patient-upload-page',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  templateUrl: './patient-upload.page.html',
  styleUrl: './patient-upload.page.scss',
})
export class PatientUploadPage implements OnInit {
  readonly t = RU;
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(EcgApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  readonly devices = signal<Device[]>([]);
  readonly file = signal<File | null>(null);
  readonly dragOver = signal(false);
  readonly busy = signal(false);
  readonly csvHint = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    deviceId: ['', Validators.required],
  });

  ngOnInit(): void {
    this.api.listDevices().subscribe({
      next: (items) => this.devices.set(items),
    });
  }

  onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const selected = input.files?.[0] ?? null;
    this.setFile(selected);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    const selected = event.dataTransfer?.files?.[0] ?? null;
    this.setFile(selected);
  }

  submit(): void {
    if (this.form.invalid || !this.file() || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.csvHint.set(null);
    this.api.upload(this.form.controls.deviceId.value, this.file()!).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.notifications.success(this.t.uploadAccepted);
        void this.router.navigate(['/patient/ecg', res.recordId]);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        if (err instanceof HttpErrorResponse) {
          const body = err.error as ApiError | null;
          if (body?.code === 'CSV_CHECK_FAILED') {
            const check = body.details?.check;
            const line = body.details?.line;
            this.csvHint.set(
              `${this.t.csvCheckFailed}: check=${check ?? '?'}` +
                (line != null ? `, line=${line}` : '') +
                (body.message ? ` — ${body.message}` : ''),
            );
          }
        }
      },
    });
  }

  private setFile(file: File | null): void {
    this.file.set(file);
    this.csvHint.set(null);
  }
}
