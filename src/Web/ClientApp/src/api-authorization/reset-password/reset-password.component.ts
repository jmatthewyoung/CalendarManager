import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { firstValueFrom } from 'rxjs';

const MIN_PASSWORD_LENGTH = 6;

@Component({
  standalone: false,
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html'
})
export class ResetPasswordComponent implements OnInit {
  email = '';
  private code = '';
  newPassword = '';
  passwordTouched = false;
  submitting = false;
  succeeded = false;
  error = '';
  linkValid = true;

  readonly minPasswordLength = MIN_PASSWORD_LENGTH;

  get passwordValid() { return this.newPassword.length >= MIN_PASSWORD_LENGTH; }

  constructor(private route: ActivatedRoute, private authService: AuthService, private router: Router, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.email = params.get('email') ?? '';
    this.code = params.get('code') ?? '';
    this.linkValid = !!this.email && !!this.code;
  }

  async submit() {
    this.error = '';
    this.passwordTouched = true;
    if (!this.passwordValid) return;

    this.submitting = true;
    try {
      await firstValueFrom(this.authService.resetPassword(this.email, this.code, this.newPassword));
      this.succeeded = true;
    } catch {
      this.error = 'Could not reset your password. The link may have expired — request a new one.';
    } finally {
      this.submitting = false;
      this.cdr.detectChanges();
    }
  }

  goToLogin() {
    this.router.navigate(['/login']);
  }
}
