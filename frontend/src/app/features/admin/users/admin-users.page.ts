import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AdminApiService, AdminUser } from '../../../core/api/admin-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';

@Component({
  selector: 'app-admin-users-page',
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
  templateUrl: './admin-users.page.html',
  styleUrl: './admin-users.page.scss',
})
export class AdminUsersPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(AdminApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);

  readonly users = signal<AdminUser[]>([]);
  readonly lastTemp = signal<{ userId: string; password: string } | null>(null);
  readonly columns = ['email', 'role', 'blocked', 'createdAt', 'actions'];

  readonly createForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    role: ['Doctor' as 'Doctor' | 'Admin', Validators.required],
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.listUsers().subscribe({
      next: (page) => this.users.set(page.items),
    });
  }

  create(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const v = this.createForm.getRawValue();
    this.api.createStaff(v.email, v.role).subscribe({
      next: (created) => {
        this.lastTemp.set({ userId: created.user.id, password: created.temporaryPassword });
        this.notifications.success(this.t.saved);
        this.createForm.reset({ role: 'Doctor' });
        this.reload();
      },
    });
  }

  toggleBlock(user: AdminUser): void {
    const action = user.isBlocked ? this.api.unblock(user.id) : this.api.block(user.id);
    action.subscribe({
      next: () => {
        this.notifications.success(this.t.saved);
        this.reload();
      },
    });
  }

  resetPassword(user: AdminUser): void {
    this.confirm
      .ask({
        title: this.t.tempPasswordTitle,
        message: `${this.t.tempPasswordMessagePrefix} ${user.email}?`,
        confirmText: this.t.generate,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.api.resetTempPassword(user.id).subscribe({
          next: (res) => {
            this.lastTemp.set({ userId: user.id, password: res.temporaryPassword });
            this.notifications.success(this.t.saved);
            this.reload();
          },
        });
      });
  }

  downloadPdf(userId: string): void {
    this.api.downloadTempPasswordPdf(userId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `temp-password-${userId}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
        this.lastTemp.set(null);
      },
    });
  }

  setConsent(user: AdminUser): void {
    this.api.setConsent(user.id).subscribe({
      next: () => this.notifications.success(this.t.saved),
    });
  }
}
