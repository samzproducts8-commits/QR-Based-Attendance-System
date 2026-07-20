import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { AuthGuard } from '../../core/guards/auth.guard';
import { RoleGuard } from '../../core/guards/role.guard';
import { ScanConfirmComponent } from './scan-confirm.component';

const routes: Routes = [
  { path: '', component: ScanConfirmComponent, canActivate: [AuthGuard, RoleGuard], data: { roles: ['Employee'] } }
];

@NgModule({
  declarations: [ScanConfirmComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ScanModule {}
