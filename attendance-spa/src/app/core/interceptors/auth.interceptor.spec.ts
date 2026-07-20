import { TestBed } from '@angular/core/testing';
import { HttpClient, HTTP_INTERCEPTORS } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('AuthInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthService,
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);

    localStorage.removeItem('attendance_auth');
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.removeItem('attendance_auth');
  });

  it('attaches Authorization header to requests sent after login', () => {
    localStorage.setItem('attendance_auth', JSON.stringify({
      accessToken: 'fake-jwt-token',
      expiresAt: new Date(Date.now() + 60000).toISOString(),
      refreshToken: 'fake-refresh',
      username: 'admin',
      roles: ['Admin'],
      staffId: null
    }));

    httpClient.get('/api/staff').subscribe();

    const req = httpMock.expectOne('/api/staff');
    expect(req.request.headers.get('Authorization')).toBe('Bearer fake-jwt-token');
    req.flush({});
  });

  it('does NOT attach Authorization header to requests sent before login', () => {
    expect(authService.getToken()).toBeNull();

    httpClient.get('/api/staff').subscribe();

    const req = httpMock.expectOne('/api/staff');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
