import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ReportApiService, downloadBlob } from '../../core/services/report.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { DailyAttendanceSheet } from '../../core/models/attendance.models';

/**
 * Daily attendance sheet with slot-status badges and Excel/PDF export
 * buttons (Requirements 4.1, 4.3, 5.4, 8.6).
 */
@Component({
  standalone: false,
  selector: 'app-daily-report',
  templateUrl: './daily-report.component.html',
  styleUrls: ['./daily-report.component.scss']
})
export class DailyReportComponent implements OnInit {
  date = new Date().toISOString().slice(0, 10);
  sheets: DailyAttendanceSheet[] = [];
  loading = false;
  exporting = false;

  constructor(
    private readonly reportApi: ReportApiService,
    private readonly errorHandler: ErrorHandlerService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.reportApi.getDaily(this.date).subscribe({
      next: sheets => {
        this.sheets = sheets;
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.errorHandler.show(err);
      }
    });
  }

  badgeClass(statusLabel: string): string {
    switch (statusLabel) {
      case 'On Time': return 'chip chip--ok';
      case 'Late': return 'chip chip--warn';
      case 'Absent': return 'chip chip--bad';
      default: return 'chip chip--muted';
    }
  }

  export(format: 'xlsx' | 'pdf'): void {
    this.exporting = true;
    this.reportApi.exportDaily(this.date, format).subscribe({
      next: blob => {
        this.exporting = false;
        downloadBlob(blob, `daily-attendance-${this.date}.${format}`);
      },
      error: (err: HttpErrorResponse) => {
        this.exporting = false;
        this.errorHandler.show(err);
      }
    });
  }
}
