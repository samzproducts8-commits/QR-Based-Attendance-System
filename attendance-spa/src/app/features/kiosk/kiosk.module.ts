import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { KioskComponent } from './kiosk.component';

// Deliberately no AuthGuard: the kiosk is a public display screen meant to
// run unattended all day. It broadcasts a QR payload with no security value
// (single-use, ~15s-lived, identity-free) — see AttendanceHub and
// docs/QR-Security.md. Gating it behind login only breaks the display every
// ~15 minutes when the login's access token expires, with no benefit.
const routes: Routes = [
  { path: '', component: KioskComponent }
];

@NgModule({
  declarations: [KioskComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class KioskModule {}
