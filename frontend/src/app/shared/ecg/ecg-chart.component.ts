import {
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import { DownsampledPoint, EcgAnomaly, EcgGap } from '../../core/api/api.models';
import { EcgApiService } from '../../core/api/ecg-api.service';
import { RU } from '../../core/i18n/ru';

@Component({
  selector: 'app-ecg-chart',
  templateUrl: './ecg-chart.component.html',
  styleUrl: './ecg-chart.component.scss',
})
export class EcgChartComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) recordId!: string;
  @Input() durationMs = 0;
  @Input() anomalies: EcgAnomaly[] = [];
  @Input() gaps: EcgGap[] = [];

  @ViewChild('overviewEl', { static: true }) overviewEl!: ElementRef<HTMLDivElement>;
  @ViewChild('detailEl', { static: true }) detailEl!: ElementRef<HTMLDivElement>;

  readonly t = RU;
  private readonly api = inject(EcgApiService);
  private overview: uPlot | null = null;
  private detail: uPlot | null = null;
  private selStartMs = 0;
  private selEndMs = 10_000;
  private windowStartMs = 0;
  readonly rangeLabel = signal('');
  readonly loading = signal(false);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['recordId'] || changes['durationMs'] || changes['anomalies']) {
      this.bootstrap();
    }
  }

  ngOnDestroy(): void {
    this.overview?.destroy();
    this.detail?.destroy();
  }

  onSlider(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    const span = Math.max(0, this.selEndMs - this.selStartMs - 10_000);
    this.windowStartMs = this.selStartMs + (span * value) / 100;
    this.loadDetail();
  }

  fmtPublic(ms: number): string {
    return this.fmt(ms / 1000);
  }

  jumpToAnomaly(a: EcgAnomaly): void {
    this.selStartMs = Math.max(0, a.startMs - 5_000);
    this.selEndMs = Math.min(this.durationMs || a.endMs + 5_000, a.endMs + 5_000);
    if (this.selEndMs - this.selStartMs < 10_000) {
      this.selEndMs = this.selStartMs + 10_000;
    }
    this.windowStartMs = a.startMs;
    this.updateRangeLabel();
    this.loadDetail();
  }

  private bootstrap(): void {
    if (!this.recordId || this.durationMs <= 0) {
      return;
    }
    this.selStartMs = 0;
    this.selEndMs = Math.min(this.durationMs, 3_600_000);
    this.windowStartMs = 0;
    this.updateRangeLabel();
    this.loadOverview();
    this.loadDetail();
  }

  private loadOverview(): void {
    this.loading.set(true);
    this.api.getSignal(this.recordId, 0, this.durationMs, 3000).subscribe({
      next: (points) => {
        this.renderOverview(points);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private loadDetail(): void {
    const from = this.windowStartMs;
    const to = from + 10_000;
    this.api.getSignal(this.recordId, from, to, 2500).subscribe({
      next: (points) => this.renderDetail(points, from, to),
    });
  }

  private renderOverview(points: DownsampledPoint[]): void {
    const xs = points.map((p) => p.offsetMs / 1000);
    const ys = points.map((p) => (p.minUv + p.maxUv) / 2);
    const opts: uPlot.Options = {
      width: this.overviewEl.nativeElement.clientWidth || 800,
      height: 110,
      series: [{}, { stroke: '#0a7a3d', width: 1 }],
      scales: { x: { time: false }, y: {} },
      axes: [
        { values: (_u, splits) => splits.map((v) => this.fmt(v)) },
        { show: false },
      ],
      cursor: { drag: { x: true, y: false } },
      hooks: {
        setSelect: [
          (u) => {
            const left = u.select.left;
            const width = u.select.width;
            if (width < 2) {
              const x = u.posToVal(left, 'x');
              const hour = Math.floor(x / 3600) * 3600;
              this.selStartMs = hour * 1000;
              this.selEndMs = Math.min(this.durationMs, (hour + 3600) * 1000);
            } else {
              this.selStartMs = u.posToVal(left, 'x') * 1000;
              this.selEndMs = u.posToVal(left + width, 'x') * 1000;
            }
            this.windowStartMs = this.selStartMs;
            this.updateRangeLabel();
            this.loadDetail();
          },
        ],
      },
    };

    this.overview?.destroy();
    this.overview = new uPlot(opts, [xs, ys], this.overviewEl.nativeElement);
  }

  private renderDetail(points: DownsampledPoint[], fromMs: number, toMs: number): void {
    const xs = points.map((p) => p.offsetMs / 1000);
    const ys = points.map((p) => (p.minUv + p.maxUv) / 2);
    const opts: uPlot.Options = {
      width: this.detailEl.nativeElement.clientWidth || 800,
      height: 200,
      series: [{}, { stroke: '#0a7a3d', width: 1.5 }],
      scales: {
        x: { time: false, min: fromMs / 1000, max: toMs / 1000 },
        y: {},
      },
      axes: [
        { values: (_u, splits) => splits.map((v) => this.fmt(v)) },
        {},
      ],
    };
    this.detail?.destroy();
    this.detail = new uPlot(opts, [xs, ys], this.detailEl.nativeElement);
  }

  private updateRangeLabel(): void {
    this.rangeLabel.set(`${this.fmt(this.selStartMs / 1000)} – ${this.fmt(this.selEndMs / 1000)}`);
  }

  private fmt(sec: number): string {
    const s = Math.max(0, Math.floor(sec));
    const h = String(Math.floor(s / 3600)).padStart(2, '0');
    const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
    const ss = String(s % 60).padStart(2, '0');
    return `${h}:${m}:${ss}`;
  }
}
