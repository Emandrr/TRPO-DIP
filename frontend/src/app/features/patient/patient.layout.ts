import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { RU } from '../../core/i18n/ru';

@Component({
  selector: 'app-patient-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule],
  templateUrl: './patient.layout.html',
  styleUrl: './patient.layout.scss',
})
export class PatientLayout {
  readonly t = RU;
  readonly auth = inject(AuthService);
}
