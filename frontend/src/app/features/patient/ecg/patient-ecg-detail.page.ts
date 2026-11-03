import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EcgRecord, EcgReport } from '../../../core/api/api.models';
import { EcgApiService } from '../../../core/api/ecg-api.service';
import { RU } from '../../../core/i18n/ru';
import { EcgChartComponent } from '../../../shared/ecg/ecg-chart.component';

@Component({
  selector: 'app-patient-ecg-detail-page',
  imports: [MatCardModule, MatButtonModule, RouterLink, DatePipe, EcgChartComponent],
  templateUrl: './patient-ecg-detail.page.html',
  styleUrl: './patient-ecg-detail.page.scss',
})
export class PatientEcgDetailPage implements OnInit, OnDestroy {
  readonly t = RU;
  private readonly api = inject(EcgApiService);
  private readonly route = inject(ActivatedRoute);
  readonly record = signal<EcgRecord | null>(null);
  readonly report = signal<EcgReport | null>(null);
  private timer: number | null = null;
  id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.id) {
      return;
    }
    this.load();
    this.timer = window.setInterval(() => this.load(true), 3000);
  }

  ngOnDestroy(): void {
    if (this.timer !== null) {
      window.clearInterval(this.timer);
    }
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

  private load(silent = false): void {
    this.api.getRecord(this.id).subscribe({
      next: (item) => {
        this.record.set(item);
        this.loadReport();
      },
      error: () => {
        if (!silent) {
          this.record.set(null);
        }
      },
    });
  }

  private loadReport(): void {
    this.api.getReport(this.id).subscribe({
      next: (report) => this.report.set(report),
      error: () => undefined,
    });
  }
}
