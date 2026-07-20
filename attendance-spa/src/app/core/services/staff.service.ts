import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateStaffRequest,
  DepartmentDto,
  PagedResult,
  StaffDto,
  StaffFilterRequest,
  UpdateStaffRequest
} from '../models/staff.models';

@Injectable({ providedIn: 'root' })
export class StaffApiService {
  private readonly baseUrl = `${environment.apiUrl}/staff`;

  constructor(private readonly http: HttpClient) {}

  getAll(filter: StaffFilterRequest): Observable<PagedResult<StaffDto>> {
    let params = new HttpParams()
      .set('pageNumber', filter.pageNumber)
      .set('pageSize', filter.pageSize);

    if (filter.department) params = params.set('department', filter.department);
    if (filter.status !== null && filter.status !== undefined) params = params.set('status', filter.status);
    if (filter.searchText) params = params.set('searchText', filter.searchText);

    return this.http.get<PagedResult<StaffDto>>(this.baseUrl, { params });
  }

  getById(staffId: number): Observable<StaffDto> {
    return this.http.get<StaffDto>(`${this.baseUrl}/${staffId}`);
  }

  create(request: CreateStaffRequest, photo: File): Observable<StaffDto> {
    const form = this.toFormData(request);
    form.set('photo', photo, photo.name);
    return this.http.post<StaffDto>(this.baseUrl, form);
  }

  update(staffId: number, request: UpdateStaffRequest): Observable<StaffDto> {
    return this.http.put<StaffDto>(`${this.baseUrl}/${staffId}`, request);
  }

  deactivate(staffId: number): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${staffId}/deactivate`, {});
  }

  /** Permanently deletes a staff member and all their dependent data. */
  delete(staffId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${staffId}`);
  }

  private toFormData(request: CreateStaffRequest): FormData {
    const form = new FormData();
    form.set('FullName', request.fullName);
    form.set('Gender', request.gender);
    form.set('DateOfBirth', request.dateOfBirth);
    form.set('PhoneNumber', request.phoneNumber);
    form.set('Email', request.email);
    form.set('DepartmentId', String(request.departmentId));
    form.set('JobTitle', request.jobTitle);
    form.set('EmploymentDate', request.employmentDate);
    if (request.address) form.set('Address', request.address);
    if (request.emergencyContact) form.set('EmergencyContact', request.emergencyContact);
    return form;
  }
}

@Injectable({ providedIn: 'root' })
export class DepartmentApiService {
  private readonly baseUrl = `${environment.apiUrl}/departments`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<DepartmentDto[]> {
    return this.http.get<DepartmentDto[]>(this.baseUrl);
  }
}
