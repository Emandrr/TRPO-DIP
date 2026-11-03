import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { RU } from '../../core/i18n/ru';

@Component({
  selector: 'app-doctor-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule],
  templateUrl: './doctor.layout.html',
  styleUrl: './doctor.layout.scss',
})
export class DoctorLayout {
  readonly t = RU;
  readonly auth = inject(AuthService);
}
