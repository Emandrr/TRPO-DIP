import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EcgRecord } from '../../../core/api/api.models';
import { EcgApiService } from '../../../core/api/ecg-api.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-doctor-ecg-page',
  imports: [MatCardModule, MatTableModule, MatButtonModule, RouterLink, DatePipe],
  templateUrl: './doctor-ecg.page.html',
  styleUrl: './doctor-ecg.page.scss',
})
export class DoctorEcgPage implements OnInit, OnDestroy {
  readonly t = RU;
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(EcgApiService);
  private patientId = '';
  readonly records = signal<EcgRecord[]>([]);
  readonly loading = signal(true);
  readonly columns = ['filename', 'uploadedAt', 'status', 'actions'];
  private timer: number | null = null;

  ngOnInit(): void {
    this.patientId = this.route.parent?.snapshot.paramMap.get('patientId') ?? '';
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
        this.records.set(items.filter((r) => r.patientId === this.patientId));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
