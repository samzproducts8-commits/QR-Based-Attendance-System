import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateSlotRequest, SlotConfigDto, UpdateSlotRequest } from '../models/slot-config.models';

@Injectable({ providedIn: 'root' })
export class SlotConfigApiService {
  private readonly baseUrl = `${environment.apiUrl}/slots`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<SlotConfigDto[]> {
    return this.http.get<SlotConfigDto[]>(this.baseUrl);
  }

  create(request: CreateSlotRequest): Observable<SlotConfigDto> {
    return this.http.post<SlotConfigDto>(this.baseUrl, request);
  }

  update(slotId: number, request: UpdateSlotRequest): Observable<SlotConfigDto> {
    return this.http.put<SlotConfigDto>(`${this.baseUrl}/${slotId}`, request);
  }

  deactivate(slotId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${slotId}`);
  }

  /** Permanently removes a slot. Fails with 409 when attendance logs reference it. */
  delete(slotId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${slotId}/permanent`);
  }
}
