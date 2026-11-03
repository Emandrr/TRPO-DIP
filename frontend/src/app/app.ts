import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastHostComponent } from './shared/ui/toast-host.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastHostComponent],
  template: `<router-outlet /><app-toast-host />`,
  styles: `:host { display: block; min-height: 100dvh; }`,
})
export class App {}
