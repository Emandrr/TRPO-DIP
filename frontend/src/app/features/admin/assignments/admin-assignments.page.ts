import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { AdminApiService, AdminUser } from '../../../core/api/admin-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';

@Component({
  selector: 'app-admin-assignments-page',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatSelectModule, MatButtonModule],
  templateUrl: './admin-assignments.page.html',
  styleUrl: './admin-assignments.page.scss',
})
export class AdminAssignmentsPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(AdminApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);

  readonly doctors = signal<AdminUser[]>([]);
  readonly patients = signal<AdminUser[]>([]);
  readonly linked = signal<{ patientId: string; email: string; lastName: string | null; firstName: string | null }[]>(
    [],
  );

  readonly form = this.fb.nonNullable.group({
    doctorId: ['', Validators.required],
    patientId: ['', Validators.required],
  });

  ngOnInit(): void {
    this.api.listUsers({ role: 'Doctor' }).subscribe({
      next: (page) => this.doctors.set(page.items),
    });
    this.api.listUsers({ role: 'Patient' }).subscribe({
      next: (page) => this.patients.set(page.items),
    });

    this.form.controls.doctorId.valueChanges.subscribe((doctorId) => {
      if (!doctorId) {
        this.linked.set([]);
        return;
      }
      this.loadLinked(doctorId);
    });
  }

  assign(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { doctorId, patientId } = this.form.getRawValue();
    this.api.assign(doctorId, patientId).subscribe({
      next: () => {
        this.notifications.success(this.t.saved);
        this.loadLinked(doctorId);
      },
    });
  }

  unassign(patientId: string): void {
    const doctorId = this.form.controls.doctorId.value;
    if (!doctorId) {
      return;
    }
    this.api.unassign(doctorId, patientId).subscribe({
      next: () => {
        this.notifications.success(this.t.deleted);
        this.loadLinked(doctorId);
      },
    });
  }

  private loadLinked(doctorId: string): void {
    this.api.listDoctorPatients(doctorId).subscribe({
      next: (items) => this.linked.set(items),
    });
  }
}
