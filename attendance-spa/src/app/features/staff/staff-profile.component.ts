import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { StaffApiService } from '../../core/services/staff.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { StaffDto } from '../../core/models/staff.models';
import { environment } from '../../../environments/environment';

/**
 * Read-only staff detail view with a deactivate action
 * (Requirements 1.6, 5.4).
 */
@Component({
  standalone: false,
  selector: 'app-staff-profile',
  templateUrl: './staff-profile.component.html',
  styleUrls: ['./staff-profile.component.scss']
})
export class StaffProfileComponent implements OnInit {
  staff: StaffDto | null = null;
  qrCodeDataUrl: string | null = null;
  loading = true;

  constructor(
    private readonly staffApi: StaffApiService,
    private readonly errorHandler: ErrorHandlerService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  get photoUrl(): string | null {
    if (!this.staff?.photoUrl) return null;
    const apiOrigin = environment.apiUrl.replace(/\/api$/, '');
    return `${apiOrigin}/${this.staff.photoUrl}`;
  }

  ngOnInit(): void {
    const staffId = Number(this.route.snapshot.paramMap.get('id'));
    this.staffApi.getById(staffId).subscribe({
      next: staff => {
        this.staff = staff;
        this.loading = false;
        this.loadQrCode(staffId);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.errorHandler.show(err);
      }
    });
  }

  private loadQrCode(staffId: number): void {
    this.staffApi.getQrCode(staffId).subscribe({
      next: res => {
        if (res?.qrCodeBase64) {
          this.qrCodeDataUrl = `data:image/png;base64,${res.qrCodeBase64}`;
        }
      },
      error: (err: HttpErrorResponse) => {
        console.error('Failed to load QR code:', err);
      }
    });
  }

  deactivate(): void {
    if (!this.staff) return;
    this.staffApi.deactivate(this.staff.staffId).subscribe({
      next: () => {
        this.errorHandler.showSuccess('Staff member deactivated.');
        this.router.navigate(['/staff']);
      },
      error: (err: HttpErrorResponse) => this.errorHandler.show(err)
    });
  }
}
