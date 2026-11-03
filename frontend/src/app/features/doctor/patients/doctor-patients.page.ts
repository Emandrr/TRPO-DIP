import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { AssignedPatient } from '../../../core/api/api.models';
import { DoctorApiService } from '../../../core/api/doctor-api.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-doctor-patients-page',
  imports: [MatCardModule, MatTableModule, MatButtonModule, RouterLink, DatePipe],
  templateUrl: './doctor-patients.page.html',
  styleUrl: './doctor-patients.page.scss',
})
export class DoctorPatientsPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(DoctorApiService);
  readonly patients = signal<AssignedPatient[]>([]);
  readonly loading = signal(true);
  readonly columns = ['fio', 'email', 'consent', 'assignedAt', 'actions'];

  ngOnInit(): void {
    this.api.listPatients().subscribe({
      next: (items) => {
        this.patients.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  fio(p: AssignedPatient): string {
    const parts = [p.lastName, p.firstName, p.middleName].filter(Boolean);
    return parts.length ? parts.join(' ') : '—';
  }
}
