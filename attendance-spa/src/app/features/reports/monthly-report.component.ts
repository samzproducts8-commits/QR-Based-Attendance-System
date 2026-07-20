import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ReportApiService, downloadBlob } from '../../core/services/report.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { MonthlySummary } from '../../core/models/attendance.models';

/**
 * Monthly summary table with OnTime/Late/Absent counts per employee and
 * export buttons (Requirements 4.2, 4.3, 4.4, 5.4, 8.6).
 */
@Component({
  standalone: false,
  selector: 'app-monthly-report',
  templateUrl: './monthly-report.component.html',
  styleUrls: ['./monthly-report.component.scss']
})
export class MonthlyReportComponent implements OnInit {
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  summary: MonthlySummary | null = null;
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
    this.reportApi.getMonthly(this.year, this.month).subscribe({
      next: summary => {
        this.summary = summary;
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.errorHandler.show(err);
      }
    });
  }

  export(format: 'xlsx' | 'pdf'): void {
    this.exporting = true;
    this.reportApi.exportMonthly(this.year, this.month, format).subscribe({
      next: blob => {
        this.exporting = false;
        downloadBlob(blob, `monthly-attendance-${this.year}-${this.month}.${format}`);
      },
      error: (err: HttpErrorResponse) => {
        this.exporting = false;
        this.errorHandler.show(err);
      }
    });
  }
}
