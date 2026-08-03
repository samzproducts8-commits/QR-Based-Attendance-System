import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '', loadChildren: () => import('./features/auth/auth.module').then(m => m.AuthModule) },
  { path: 'staff', loadChildren: () => import('./features/staff/staff.module').then(m => m.StaffModule) },
  { path: 'slots', loadChildren: () => import('./features/attendance-config/attendance-config.module').then(m => m.AttendanceConfigModule) },
  { path: 'kiosk', loadChildren: () => import('./features/kiosk/kiosk.module').then(m => m.KioskModule) },
  { path: 'scan', loadChildren: () => import('./features/scan/scan.module').then(m => m.ScanModule) },
  { path: 'employee-id', loadChildren: () => import('./features/employee-id/employee-id.module').then(m => m.EmployeeIdModule) },
  { path: 'reports', loadChildren: () => import('./features/reports/reports.module').then(m => m.ReportsModule) },
  { path: '**', redirectTo: 'login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
