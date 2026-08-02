import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuthGuard } from '../../core/guards/auth.guard';
import { RoleGuard } from '../../core/guards/role.guard';
import { DailyReportComponent } from './daily-report.component';
import { MonthlyReportComponent } from './monthly-report.component';
import { AbsenceReasonDialogComponent } from './absence-reason-dialog.component';

const routes: Routes = [
  {
    path: '',
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Admin', 'HR'] },
    children: [
      { path: '', redirectTo: 'daily', pathMatch: 'full' },
      { path: 'daily', component: DailyReportComponent },
      { path: 'monthly', component: MonthlyReportComponent }
    ]
  }
];

@NgModule({
  declarations: [DailyReportComponent, MonthlyReportComponent, AbsenceReasonDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ReportsModule {}
