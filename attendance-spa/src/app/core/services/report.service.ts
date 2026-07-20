import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DailyAttendanceSheet, MonthlySummary } from '../models/attendance.models';

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  private readonly baseUrl = `${environment.apiUrl}/reports`;

  constructor(private readonly http: HttpClient) {}

  getDaily(date: string, staffId?: number): Observable<DailyAttendanceSheet[]> {
    let params = new HttpParams().set('date', date);
    if (staffId) params = params.set('staffId', staffId);
    return this.http.get<DailyAttendanceSheet[]>(`${this.baseUrl}/daily`, { params });
  }

  getMonthly(year: number, month: number, staffId?: number, departmentId?: number): Observable<MonthlySummary> {
    let params = new HttpParams().set('year', year).set('month', month);
    if (staffId) params = params.set('staffId', staffId);
    if (departmentId) params = params.set('departmentId', departmentId);
    return this.http.get<MonthlySummary>(`${this.baseUrl}/monthly`, { params });
  }

  exportDaily(date: string, format: 'xlsx' | 'pdf'): Observable<Blob> {
    const params = new HttpParams().set('date', date).set('format', format);
    return this.http.get(`${this.baseUrl}/daily/export`, { params, responseType: 'blob' });
  }

  exportMonthly(year: number, month: number, format: 'xlsx' | 'pdf'): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('month', month).set('format', format);
    return this.http.get(`${this.baseUrl}/monthly/export`, { params, responseType: 'blob' });
  }
}

/** Triggers a browser download for a Blob response. */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  window.URL.revokeObjectURL(url);
}
