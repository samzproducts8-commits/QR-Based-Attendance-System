import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { LoginComponent } from './login.component';
import { AccessDeniedComponent } from './access-denied.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'access-denied', component: AccessDeniedComponent }
];

@NgModule({
  declarations: [LoginComponent, AccessDeniedComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class AuthModule {}
