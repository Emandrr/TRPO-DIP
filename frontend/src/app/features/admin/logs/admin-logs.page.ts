import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AdminApiService, SystemLog } from '../../../core/api/admin-api.service';
import { RU } from '../../../core/i18n/ru';

@Component({
  selector: 'app-admin-logs-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    DatePipe,
  ],
  templateUrl: './admin-logs.page.html',
  styleUrl: './admin-logs.page.scss',
})
export class AdminLogsPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(AdminApiService);
  private readonly fb = inject(FormBuilder);

  readonly items = signal<SystemLog[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly columns = ['ts', 'level', 'module', 'code', 'message'];

  readonly filterForm = this.fb.nonNullable.group({
    level: [''],
    module: [''],
    code: [''],
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(page = 1): void {
    const f = this.filterForm.getRawValue();
    this.api
      .listLogs({
        level: f.level || undefined,
        module: f.module || undefined,
        code: f.code || undefined,
        page,
      })
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.total);
          this.page.set(res.page);
        },
      });
  }

  apply(): void {
    this.reload(1);
  }

  prev(): void {
    if (this.page() > 1) {
      this.reload(this.page() - 1);
    }
  }

  next(): void {
    if (this.page() * 50 < this.total()) {
      this.reload(this.page() + 1);
    }
  }
}
