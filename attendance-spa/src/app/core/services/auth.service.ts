import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RefreshRequest } from '../models/auth.models';

const STORAGE_KEY = 'attendance_auth';

export interface CurrentUser {
  username: string;
  roles: string[];
  staffId: number | null;
}

/**
 * Owns the JWT pair in localStorage and exposes the authenticated user as an
 * observable so the shell / guards can react to login and logout.
 * Satisfies Requirements 5.1, 5.6.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSubject = new BehaviorSubject<CurrentUser | null>(this.readStoredUser());
  readonly currentUser$ = this.currentUserSubject.asObservable();

  constructor(private readonly http: HttpClient) {}

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap(response => this.storeSession(response))
    );
  }

  refresh(): Observable<AuthResponse> {
    const stored = this.readStoredSession();
    const request: RefreshRequest = { refreshToken: stored?.refreshToken ?? '' };
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, request).pipe(
      tap(response => this.storeSession(response))
    );
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.currentUserSubject.next(null);
  }

  getToken(): string | null {
    return this.readStoredSession()?.accessToken ?? null;
  }

  getRefreshToken(): string | null {
    return this.readStoredSession()?.refreshToken ?? null;
  }

  get currentUser(): CurrentUser | null {
    return this.currentUserSubject.value;
  }

  hasRole(role: string): boolean {
    return this.currentUser?.roles.includes(role) ?? false;
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(r => this.hasRole(r));
  }

  private storeSession(response: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this.currentUserSubject.next({
      username: response.username,
      roles: response.roles,
      staffId: response.staffId
    });
  }

  private readStoredSession(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthResponse;
    } catch {
      return null;
    }
  }

  private readStoredUser(): CurrentUser | null {
    const stored = this.readStoredSession();
    if (!stored) return null;
    return { username: stored.username, roles: stored.roles, staffId: stored.staffId };
  }
}
