import { Component, ChangeDetectorRef } from '@angular/core';
import { AuthService } from '../auth.service';
import { firstValueFrom } from 'rxjs';

@Component({
  standalone: false,
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html'
})
export class ForgotPasswordComponent {
  email = '';
  submitted = false;
  submitting = false;

  constructor(private authService: AuthService, private cdr: ChangeDetectorRef) {}

  async submit() {
    if (!this.email) return;

    this.submitting = true;
    try {
      await firstValueFrom(this.authService.forgotPassword(this.email));
    } catch {
      // Identity always returns success here to avoid leaking whether the email exists,
      // so a request failure is a transport/server error, not "email not found" — the
      // generic confirmation below still applies either way.
    } finally {
      this.submitting = false;
      this.submitted = true;
      this.cdr.detectChanges();
    }
  }
}
