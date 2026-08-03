import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { StaffApiService } from '../../core/services/staff.service';
import { StaffIdentityCardDto } from '../../core/models/staff.models';
import { environment } from '../../../environments/environment';

@Component({
  standalone: false,
  selector: 'app-employee-id',
  templateUrl: './employee-id.component.html',
  styleUrls: ['./employee-id.component.scss']
})
export class EmployeeIdComponent implements OnInit {
  cardData: StaffIdentityCardDto | null = null;
  loading = true;
  error: string | null = null;
  scannedAt: Date = new Date();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly staffApi: StaffApiService
  ) {}

  get photoUrl(): string | null {
    if (!this.cardData?.photoUrl) return null;
    const apiOrigin = environment.apiUrl.replace(/\/api$/, '');
    return `${apiOrigin}/${this.cardData.photoUrl}`;
  }

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (!code) {
      this.error = 'No employee code provided in the scan URL.';
      this.loading = false;
      return;
    }

    this.staffApi.getIdentityCard(code).subscribe({
      next: data => {
        this.cardData = data;
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        if (err.status === 404) {
          this.error = `No active employee record found for code "${code}".`;
        } else {
          this.error = 'Unable to load employee identification data. Please check connection.';
        }
      }
    });
  }
}
