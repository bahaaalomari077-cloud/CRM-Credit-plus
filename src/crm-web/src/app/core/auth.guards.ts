import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './auth.models';

/**
 * Both guards inject synchronously first — `inject()` is only valid before the
 * first await — then resolve the session and decide. A UrlTree redirects rather
 * than simply refusing.
 */
function guardFor(roles?: Role[]): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    const decide = (): true | UrlTree => {
      if (!auth.isAuthenticated()) {
        // Remember where they were heading so sign-in can return them there.
        return router.createUrlTree(['/login'], { queryParams: { redirect: state.url } });
      }

      if (!roles) {
        return true;
      }

      const role = auth.role();
      return role !== null && roles.includes(role) ? true : router.createUrlTree(['/dashboard']);
    };

    return auth.ready() ? decide() : auth.restore().then(decide);
  };
}

/** Blocks a route until a session exists, sending the visitor to the sign-in page. */
export const authGuard: CanActivateFn = guardFor();

/** Blocks a route unless the signed-in user holds one of the given roles. */
export const roleGuard = (roles: Role[]): CanActivateFn => guardFor(roles);
