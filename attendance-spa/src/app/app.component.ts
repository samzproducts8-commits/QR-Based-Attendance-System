import { Component } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { filter, map, startWith } from 'rxjs/operators';
import { AuthService } from './core/services/auth.service';

/** Routes that render full-screen with no toolbar/sidenav chrome. */
const CHROMELESS_PREFIXES = ['/login', '/kiosk', '/scan', '/employee-id', '/access-denied'];

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'attendance-spa';
  showChrome$: Observable<boolean>;

  constructor(readonly authService: AuthService, private readonly router: Router) {
    this.showChrome$ = this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => !CHROMELESS_PREFIXES.some(prefix => event.urlAfterRedirects.startsWith(prefix))),
      startWith(true)
    );
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
