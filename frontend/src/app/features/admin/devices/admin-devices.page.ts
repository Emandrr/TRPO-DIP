import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { Device } from '../../../core/api/api.models';
import { AdminApiService } from '../../../core/api/admin-api.service';
import { RU } from '../../../core/i18n/ru';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmService } from '../../../shared/ui/confirm.service';

@Component({
  selector: 'app-admin-devices-page',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './admin-devices.page.html',
  styleUrl: './admin-devices.page.scss',
})
export class AdminDevicesPage implements OnInit {
  readonly t = RU;
  private readonly api = inject(AdminApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly confirm = inject(ConfirmService);

  readonly devices = signal<Device[]>([]);
  readonly columns = ['name', 'serialNumber', 'model', 'actions'];

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    serialNumber: ['', Validators.required],
    model: [''],
    description: [''],
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.listDevices().subscribe({
      next: (items) => this.devices.set(items),
    });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.api
      .createDevice({
        name: v.name,
        serialNumber: v.serialNumber,
        model: v.model || null,
        description: v.description || null,
      })
      .subscribe({
        next: () => {
          this.form.reset();
          this.notifications.success(this.t.saved);
          this.reload();
        },
      });
  }

  remove(device: Device): void {
    this.confirm
      .ask({
        title: this.t.deleteConfirmTitle,
        message: `${this.t.deleteConfirmMessage} (${device.name})`,
        confirmText: this.t.delete,
      })
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.api.deleteDevice(device.id).subscribe({
          next: () => {
            this.notifications.success(this.t.deleted);
            this.reload();
          },
        });
      });
  }
}
