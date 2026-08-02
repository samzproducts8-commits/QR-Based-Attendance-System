import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ReportApiService, downloadBlob } from '../../core/services/report.service';
import { ErrorHandlerService } from '../../core/services/error-handler.service';
import { DailyAttendanceSheet, DailySlotEntry } from '../../core/models/attendance.models';
import { AbsenceReasonDialogComponent } from './absence-reason-dialog.component';

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
    private readonly errorHandler: ErrorHandlerService,
    private readonly dialog: MatDialog,
    private readonly snackBar: MatSnackBar
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

  openAbsenceReasonDialog(sheet: DailyAttendanceSheet, entry: DailySlotEntry): void {
    if (entry.statusLabel !== 'Absent') return;

    const dialogRef = this.dialog.open(AbsenceReasonDialogComponent, {
      data: {
        staffName: sheet.staffName,
        slotName: entry.slotName,
        date: this.date,
        currentReason: entry.absenceReason
      }
    });

    dialogRef.afterClosed().subscribe(reason => {
      if (reason) {
        this.reportApi.setAbsenceReason(sheet.staffId, entry.slotId, this.date, reason).subscribe({
          next: () => {
            this.snackBar.open('Absence reason saved successfully', 'Close', { duration: 3000 });
            this.load();
          },
          error: (err: HttpErrorResponse) => {
            this.errorHandler.show(err);
          }
        });
      }
    });
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
