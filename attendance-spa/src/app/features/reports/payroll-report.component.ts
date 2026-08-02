import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { MonthlyPayrollSummary, StaffPayrollSummary } from '../../core/models/attendance.models';
import { downloadBlob, ReportApiService } from '../../core/services/report.service';

@Component({
  standalone: false,
  selector: 'app-payroll-report',
  templateUrl: './payroll-report.component.html',
  styleUrls: ['./payroll-report.component.scss']
})
export class PayrollReportComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();

  selectedYear: number = new Date().getFullYear();
  selectedMonth: number = new Date().getMonth() + 1;

  months = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];

  years: number[] = [];

  payrollSummary: MonthlyPayrollSummary | null = null;
  filteredStaff: StaffPayrollSummary[] = [];
  searchTerm: string = '';

  loading = false;
  exportingCsv = false;
  exportingXlsx = false;
  errorMessage: string | null = null;

  constructor(private readonly reportService: ReportApiService) {
    const currentYear = new Date().getFullYear();
    for (let y = currentYear; y >= currentYear - 3; y--) {
      this.years.push(y);
    }
  }

  ngOnInit(): void {
    this.loadPayroll();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPayroll(): void {
    this.loading = true;
    this.errorMessage = null;

    this.reportService.getMonthlyPayroll(this.selectedYear, this.selectedMonth)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (summary) => {
          this.payrollSummary = summary;
          this.filterStaff();
          this.loading = false;
        },
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.detail || 'Failed to load monthly payroll summary.';
        }
      });
  }

  filterStaff(): void {
    if (!this.payrollSummary) {
      this.filteredStaff = [];
      return;
    }

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      this.filteredStaff = [...this.payrollSummary.staffSummaries];
      return;
    }

    this.filteredStaff = this.payrollSummary.staffSummaries.filter(s =>
      s.fullName.toLowerCase().includes(term) ||
      s.uniqueCode.toLowerCase().includes(term) ||
      s.department.toLowerCase().includes(term)
    );
  }

  export(format: 'csv' | 'xlsx'): void {
    if (format === 'csv') this.exportingCsv = true;
    if (format === 'xlsx') this.exportingXlsx = true;

    this.reportService.exportPayroll(this.selectedYear, this.selectedMonth, format)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const monthStr = this.selectedMonth.toString().padStart(2, '0');
          const ext = format === 'csv' ? 'csv' : 'xlsx';
          const fileName = `payroll-summary-${this.selectedYear}-${monthStr}.${ext}`;
          downloadBlob(blob, fileName);

          if (format === 'csv') this.exportingCsv = false;
          if (format === 'xlsx') this.exportingXlsx = false;
        },
        error: (err) => {
          if (format === 'csv') this.exportingCsv = false;
          if (format === 'xlsx') this.exportingXlsx = false;
          this.errorMessage = 'Failed to export payroll report.';
        }
      });
  }
}
