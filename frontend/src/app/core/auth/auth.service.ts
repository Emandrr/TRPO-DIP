import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthSession, TokenResponse, UserRole } from './auth.models';

const STORAGE_KEY = 'saekg.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly sessionSignal = signal<AuthSession | null>(this.readStorage());

  readonly session = this.sessionSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.sessionSignal() !== null);
  readonly role = computed(() => this.sessionSignal()?.role ?? null);
  readonly mustChangePassword = computed(() => this.sessionSignal()?.mustChangePassword ?? false);

  login(email: string, password: string): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/login', { email, password })
      .pipe(tap((tokens) => this.persist(tokens)));
  }

  register(email: string, password: string): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/register', { email, password })
      .pipe(tap((tokens) => this.persist(tokens)));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http
      .post<void>('/api/v1/auth/change-password', { currentPassword, newPassword })
      .pipe(
        tap(() => {
          const current = this.sessionSignal();
          if (current) {
            this.sessionSignal.set({ ...current, mustChangePassword: false });
            this.writeStorage(this.sessionSignal());
          }
        }),
      );
  }

  refresh(): Observable<TokenResponse> {
    const refreshToken = this.sessionSignal()?.refreshToken;
    return this.http
      .post<TokenResponse>('/api/v1/auth/refresh', { refreshToken })
      .pipe(tap((tokens) => this.persist(tokens)));
  }

  logout(): void {
    const refreshToken = this.sessionSignal()?.refreshToken;
    this.clear();
    if (refreshToken) {
      this.http.post('/api/v1/auth/logout', { refreshToken }).subscribe({ error: () => undefined });
    }
    void this.router.navigateByUrl('/login');
  }

  accessToken(): string | null {
    return this.sessionSignal()?.accessToken ?? null;
  }

  homePathForRole(role: UserRole | null | undefined): string {
    switch (role) {
      case 'Doctor':
        return '/doctor';
      case 'Admin':
        return '/admin';
      case 'Patient':
      default:
        return '/patient';
    }
  }

  navigateHome(): void {
    const session = this.sessionSignal();
    if (!session) {
      void this.router.navigateByUrl('/login');
      return;
    }
    if (session.mustChangePassword) {
      void this.router.navigateByUrl('/change-password');
      return;
    }
    void this.router.navigateByUrl(this.homePathForRole(session.role));
  }

  clear(): void {
    this.sessionSignal.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private persist(tokens: TokenResponse): void {
    const session: AuthSession = {
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      accessTokenExpiresAt: tokens.accessTokenExpiresAt,
      mustChangePassword: tokens.mustChangePassword,
      role: tokens.role,
      userId: tokens.userId,
    };
    this.sessionSignal.set(session);
    this.writeStorage(session);
  }

  private readStorage(): AuthSession | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AuthSession) : null;
    } catch {
      return null;
    }
  }

  private writeStorage(session: AuthSession | null): void {
    if (!session) {
      localStorage.removeItem(STORAGE_KEY);
      return;
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }
}
