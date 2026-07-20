import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuthGuard } from '../../core/guards/auth.guard';
import { RoleGuard } from '../../core/guards/role.guard';
import { StaffListComponent } from './staff-list.component';
import { StaffFormComponent } from './staff-form.component';
import { StaffProfileComponent } from './staff-profile.component';

const routes: Routes = [
  {
    path: '',
    canActivate: [AuthGuard, RoleGuard],
    data: { roles: ['Admin', 'HR'] },
    children: [
      { path: '', component: StaffListComponent },
      { path: 'new', component: StaffFormComponent, canActivate: [RoleGuard], data: { roles: ['Admin'] } },
      { path: ':id', component: StaffProfileComponent },
      { path: ':id/edit', component: StaffFormComponent, canActivate: [RoleGuard], data: { roles: ['Admin', 'HR'] } }
    ]
  }
];

@NgModule({
  declarations: [StaffListComponent, StaffFormComponent, StaffProfileComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class StaffModule {}
