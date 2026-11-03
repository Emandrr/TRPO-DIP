import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from '../../core/auth/auth.service';
import { RU } from '../../core/i18n/ru';

@Component({
  selector: 'app-role-home-page',
  imports: [MatToolbarModule, MatButtonModule],
  template: `
    <mat-toolbar color="primary">
      <span>{{ title() }}</span>
      <span class="spacer"></span>
      <button mat-button type="button" (click)="auth.logout()">{{ t.logout }}</button>
    </mat-toolbar>
    <main class="content">
      <h1>{{ title() }}</h1>
      <p>{{ t.appTitle }} · {{ auth.role() }}</p>
    </main>
  `,
  styles: `
    .spacer { flex: 1; }
    .content { padding: 1.25rem; max-width: 960px; margin: 0 auto; }
  `,
})
export class RoleHomePage {
  readonly t = RU;
  readonly auth = inject(AuthService);
  readonly title = computed(() => {
    switch (this.auth.role()) {
      case 'Doctor':
        return RU.homeDoctor;
      case 'Admin':
        return RU.homeAdmin;
      default:
        return RU.homePatient;
    }
  });
}
