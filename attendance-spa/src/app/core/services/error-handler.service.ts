import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProblemDetails } from '../models/attendance.models';

/**
 * Maps HTTP error responses (RFC 7807 ProblemDetails from
 * ExceptionHandlingMiddleware, or plain `{ error }` shapes from a few
 * controller actions) into user-facing toast messages.
 */
@Injectable({ providedIn: 'root' })
export class ErrorHandlerService {
  constructor(private readonly snackBar: MatSnackBar) {}

  show(error: HttpErrorResponse): void {
    this.showMessage(this.extractMessage(error));
  }

  showMessage(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 5000, panelClass: 'toast-error' });
  }

  showSuccess(message: string): void {
    this.snackBar.open(message, undefined, { duration: 3000, panelClass: 'toast-success' });
  }

  extractMessage(error: HttpErrorResponse): string {
    type ValidationBody = { errors?: Record<string, string[]>; title?: string };
    const body = error.error as ProblemDetails & ValidationBody & { error?: string } | undefined;

    if (body?.detail) {
      return body.detail;
    }
    if (body?.error) {
      return body.error;
    }
    // ASP.NET Core's automatic [ApiController] model-validation response
    // (e.g. an unparseable date) has no `detail` — the messages live in
    // `errors: { FieldName: ["message", ...] }` instead.
    if (body?.errors) {
      const messages = Object.values(body.errors).flat();
      if (messages.length > 0) return messages.join(' ');
      if (body.title) return body.title;
    }

    switch (error.status) {
      case 0: return 'Cannot reach the server. Please check your connection.';
      case 400: return 'The request contains invalid data. Please check the form and try again.';
      case 401: return 'Your session has expired. Please log in again.';
      case 403: return 'You do not have permission to perform this action.';
      case 404: return 'The requested resource was not found.';
      case 409: return 'This action conflicts with existing data.';
      case 410: return 'This QR code has expired.';
      case 422: return 'This request cannot be processed right now.';
      default: return 'An unexpected error occurred. Please try again.';
    }
  }
}
