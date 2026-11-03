import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { EcgRecord } from '../../../core/api/api.models';
import { EcgApiService } from '../../../core/api/ecg-api.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-patient-ecg-page',
  imports: [MatCardModule, MatTableModule, MatButtonModule, RouterLink, DatePipe],
  templateUrl: './patient-ecg.page.html',
  styleUrl: './patient-ecg.page.scss',
})
export class PatientEcgPage implements OnInit, OnDestroy {
  readonly t = RU;
  private readonly api = inject(EcgApiService);
  readonly records = signal<EcgRecord[]>([]);
  readonly loading = signal(true);
  readonly columns = ['filename', 'uploadedAt', 'status', 'actions'];
  private timer: number | null = null;

  ngOnInit(): void {
    this.reload();
    this.timer = window.setInterval(() => this.reload(true), 3000);
  }

  ngOnDestroy(): void {
    if (this.timer !== null) {
      window.clearInterval(this.timer);
    }
  }

  private reload(silent = false): void {
    if (!silent) {
      this.loading.set(true);
    }
    this.api.listRecords().subscribe({
      next: (items) => {
        this.records.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
