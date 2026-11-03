import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AssignedPatient } from '../../../core/api/api.models';
import { DoctorApiService } from '../../../core/api/doctor-api.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-doctor-patient-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatTabsModule],
  templateUrl: './doctor-patient.layout.html',
  styleUrl: './doctor-patient.layout.scss',
})
export class DoctorPatientLayout implements OnInit {
  readonly t = RU;
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(DoctorApiService);
  readonly patientId = signal('');
  readonly patient = signal<AssignedPatient | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('patientId') ?? '';
    this.patientId.set(id);
    this.api.listPatients().subscribe({
      next: (items) => this.patient.set(items.find((p) => p.patientId === id) ?? null),
    });
  }

  title(): string {
    const p = this.patient();
    if (!p) {
      return this.t.openPatient;
    }
    const fio = [p.lastName, p.firstName, p.middleName].filter(Boolean).join(' ');
    return fio || p.email;
  }
}
