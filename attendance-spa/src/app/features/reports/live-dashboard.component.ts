import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { LiveDashboardMetrics } from '../../core/models/attendance.models';
import { AttendanceHubService } from '../../core/services/attendance-hub.service';
import { ReportApiService } from '../../core/services/report.service';

@Component({
  standalone: false,
  selector: 'app-live-dashboard',
  templateUrl: './live-dashboard.component.html',
  styleUrls: ['./live-dashboard.component.scss']
})
export class LiveDashboardComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();

  metrics: LiveDashboardMetrics | null = null;
  loading = true;
  errorMessage: string | null = null;
  lastUpdated: Date = new Date();

  constructor(
    private readonly reportService: ReportApiService,
    private readonly hubService: AttendanceHubService
  ) {}

  ngOnInit(): void {
    this.loadMetrics();
    this.initRealtimeSubscription();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadMetrics(): void {
    this.loading = true;
    this.errorMessage = null;

    this.reportService.getLiveDashboard()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.metrics = data;
          this.loading = false;
          this.lastUpdated = new Date();
        },
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.detail || 'Failed to load live dashboard metrics.';
        }
      });
  }

  private initRealtimeSubscription(): void {
    this.hubService.connect();

    this.hubService.liveDashboardUpdate$
      .pipe(takeUntil(this.destroy$))
      .subscribe((updatedMetrics) => {
        if (updatedMetrics) {
          this.metrics = updatedMetrics;
          this.lastUpdated = new Date();
        }
      });
  }

  getCheckInPercentage(): number {
    if (!this.metrics || this.metrics.totalActiveStaff === 0) return 0;
    return Math.round((this.metrics.totalActiveCheckIns / this.metrics.totalActiveStaff) * 100);
  }

  getStatusClass(label: string): string {
    switch (label) {
      case 'On Time':
        return 'status-badge--ontime';
      case 'Late':
        return 'status-badge--late';
      case 'Manual Entry':
        return 'status-badge--manual';
      case 'Absent':
        return 'status-badge--absent';
      default:
        return '';
    }
  }
}
