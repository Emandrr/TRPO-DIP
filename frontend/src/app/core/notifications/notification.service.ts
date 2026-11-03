import { Injectable, signal } from '@angular/core';

export type ToastLevel = 'info' | 'error' | 'success';

export interface Toast {
  id: number;
  level: ToastLevel;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private seq = 0;
  readonly toasts = signal<Toast[]>([]);

  info(message: string): void {
    this.push('info', message);
  }

  success(message: string): void {
    this.push('success', message);
  }

  error(message: string): void {
    this.push('error', message);
  }

  dismiss(id: number): void {
    this.toasts.update((items) => items.filter((t) => t.id !== id));
  }

  private push(level: ToastLevel, message: string): void {
    const id = ++this.seq;
    this.toasts.update((items) => [...items, { id, level, message }]);
    window.setTimeout(() => this.dismiss(id), 5000);
  }
}
