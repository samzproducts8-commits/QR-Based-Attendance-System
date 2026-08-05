import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../core/services/auth.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';

@Component({
  standalone: false,
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  form: ReturnType<FormBuilder['group']>;
  submitting = false;
  hidePassword = true;
  currentYear = new Date().getFullYear();

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly errorHandler: ErrorHandlerService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      username: ['', Validators.required],
      password: ['', Validators.required],
      rememberMe: [false]
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    const { username, password } = this.form.getRawValue();

    this.authService.login({ username: username!, password: password! }).subscribe({
      next: () => {
        this.submitting = false;
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? this.defaultRouteFor();
        this.router.navigateByUrl(returnUrl);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        this.errorHandler.show(err);
      }
    });
  }

  onForgotPassword(event: Event): void {
    event.preventDefault();
    this.snackBar.open(
      'Please contact your DAFTech HR or IT Administrator to reset your access credentials.',
      'Close',
      { duration: 6000, horizontalPosition: 'center', verticalPosition: 'bottom' }
    );
  }

  private defaultRouteFor(): string {
    const user = this.authService.currentUser;
    if (user?.roles.includes('Admin') || user?.roles.includes('HR')) {
      return '/staff';
    }
    if (user?.roles.includes('Employee')) {
      return '/scan';
    }
    return '/';
  }
}
