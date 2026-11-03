import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { NotificationService } from '../../core/notifications/notification.service';

@Component({
  selector: 'app-toast-host',
  imports: [MatButtonModule],
  template: `
    <div class="toast-host" aria-live="polite">
      @for (toast of notifications.toasts(); track toast.id) {
        <div class="toast" [class]="toast.level">
          <span>{{ toast.message }}</span>
          <button mat-button type="button" (click)="notifications.dismiss(toast.id)">OK</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-host {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      z-index: 1000;
      display: grid;
      gap: 0.5rem;
      width: min(360px, calc(100vw - 2rem));
    }
    .toast {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.75rem 0.9rem;
      border-radius: 8px;
      color: #fff;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.18);
    }
    .toast.error { background: #c62828; }
    .toast.success { background: #2e7d32; }
    .toast.info { background: #1565c0; }
  `,
})
export class ToastHostComponent {
  readonly notifications = inject(NotificationService);
}
