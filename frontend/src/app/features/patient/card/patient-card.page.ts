import { Component, OnInit, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MedicalCard } from '../../../core/api/api.models';
import { MedicalApiService } from '../../../core/api/medical-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-patient-card-page',
  imports: [MatCardModule],
  templateUrl: './patient-card.page.html',
  styleUrl: './patient-card.page.scss',
})
export class PatientCardPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(MedicalApiService);
  private readonly auth = inject(AuthService);
  readonly card = signal<MedicalCard | null>(null);
  readonly missing = signal(false);
  readonly loading = signal(true);

  ngOnInit(): void {
    const patientId = this.auth.session()?.userId;
    if (!patientId) {
      this.loading.set(false);
      this.missing.set(true);
      return;
    }
    this.api.getMedicalCard(patientId).subscribe({
      next: (card) => {
        this.card.set(card);
        this.loading.set(false);
      },
      error: () => {
        this.missing.set(true);
        this.loading.set(false);
      },
    });
  }
}
