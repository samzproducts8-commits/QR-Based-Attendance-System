import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuthGuard } from '../../core/guards/auth.guard';
import { RoleGuard } from '../../core/guards/role.guard';
import { DailyReportComponent } from './daily-report.component';
import { MonthlyReportComponent } from './monthly-report.component';
import { AbsenceReasonDialogComponent } from './absence-reason-dialog.component';
import { LiveDashboardComponent } from './live-dashboard.component';
import { PayrollReportComponent } from './payroll-report.component';

const routes: Routes = [
  {
    path: '',
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Admin', 'HR'] },
    children: [
      { path: '', redirectTo: 'live-dashboard', pathMatch: 'full' },
      { path: 'live-dashboard', component: LiveDashboardComponent },
      { path: 'payroll', component: PayrollReportComponent },
      { path: 'daily', component: DailyReportComponent },
      { path: 'monthly', component: MonthlyReportComponent }
    ]
  }
];

@NgModule({
  declarations: [
    DailyReportComponent,
    MonthlyReportComponent,
    AbsenceReasonDialogComponent,
    LiveDashboardComponent,
    PayrollReportComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ReportsModule {}
