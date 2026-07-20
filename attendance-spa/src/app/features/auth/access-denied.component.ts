import { Component } from '@angular/core';

@Component({
  standalone: false,
  selector: 'app-access-denied',
  template: `
    <div class="access-denied">
      <h2>Access Denied</h2>
      <p>You do not have permission to view this page.</p>
      <a routerLink="/">Return home</a>
    </div>
  `,
  styles: [`
    .access-denied { text-align: center; padding: 64px 24px; }
  `]
})
export class AccessDeniedComponent {}
