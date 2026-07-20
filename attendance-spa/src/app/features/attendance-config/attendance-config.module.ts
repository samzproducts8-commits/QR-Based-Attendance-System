import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuthGuard } from '../../core/guards/auth.guard';
import { RoleGuard } from '../../core/guards/role.guard';
import { SlotConfigComponent } from './slot-config.component';

const routes: Routes = [
  { path: '', component: SlotConfigComponent, canActivate: [AuthGuard, RoleGuard], data: { roles: ['Admin'] } }
];

@NgModule({
  declarations: [SlotConfigComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class AttendanceConfigModule {}
