import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AttendanceHistoryEntry, AttendanceRecordDto, ScanRequestDto } from '../models/attendance.models';

@Injectable({ providedIn: 'root' })
export class AttendanceApiService {
  private readonly baseUrl = `${environment.apiUrl}/attendance`;

  constructor(private readonly http: HttpClient) {}

  scan(request: ScanRequestDto): Observable<AttendanceRecordDto> {
    return this.http.post<AttendanceRecordDto>(`${this.baseUrl}/scan`, request);
  }

  myHistory(from?: string, to?: string): Observable<AttendanceHistoryEntry[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<AttendanceHistoryEntry[]>(`${this.baseUrl}/my-history`, { params });
  }
}
