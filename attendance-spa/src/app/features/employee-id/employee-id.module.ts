import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { EmployeeIdComponent } from './employee-id.component';

const routes: Routes = [
  {
    path: ':code',
    component: EmployeeIdComponent
  }
];

@NgModule({
  declarations: [EmployeeIdComponent],
  imports: [
    SharedModule,
    RouterModule.forChild(routes)
  ]
})
export class EmployeeIdModule {}
