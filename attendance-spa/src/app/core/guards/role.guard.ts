import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Blocks navigation when the current user lacks any of the roles listed in
 * the route's `data.roles` array. Redirects to an access-denied page rather
 * than login, since the user IS authenticated — just not authorized.
 */
@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {
  constructor(private readonly authService: AuthService, private readonly router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): boolean | UrlTree {
    const requiredRoles = (route.data['roles'] as string[] | undefined) ?? [];

    if (requiredRoles.length === 0 || this.authService.hasAnyRole(requiredRoles)) {
      return true;
    }

    return this.router.createUrlTree(['/access-denied']);
  }
}
