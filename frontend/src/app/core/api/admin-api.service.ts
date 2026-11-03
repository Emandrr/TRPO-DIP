import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Device } from './api.models';

export interface AdminUser {
  id: string;
  email: string;
  role: string;
  isBlocked: boolean;
  mustChangePassword: boolean;
  createdAt: string;
}

export interface PagedUsers {
  items: AdminUser[];
  page: number;
  pageSize: number;
  total: number;
}

export interface CreatedStaff {
  user: AdminUser;
  temporaryPassword: string;
}

export interface SystemLog {
  id: number;
  ts: string;
  level: string;
  module: string;
  code: string | null;
  message: string;
  requestId: string | null;
}

export interface PagedLogs {
  items: SystemLog[];
  page: number;
  pageSize: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);

  listUsers(opts?: { role?: string; email?: string; page?: number }): Observable<PagedUsers> {
    let params = new HttpParams();
    if (opts?.role) {
      params = params.set('role', opts.role);
    }
    if (opts?.email) {
      params = params.set('email', opts.email);
    }
    params = params.set('page', String(opts?.page ?? 1)).set('pageSize', '100');
    return this.http.get<PagedUsers>('/api/v1/admin/users', { params });
  }

  createStaff(email: string, role: 'Doctor' | 'Admin'): Observable<CreatedStaff> {
    return this.http.post<CreatedStaff>('/api/v1/admin/users', { email, role });
  }

  block(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/admin/users/${id}/block`, {});
  }

  unblock(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/admin/users/${id}/unblock`, {});
  }

  resetTempPassword(id: string): Observable<{ temporaryPassword: string }> {
    return this.http.post<{ temporaryPassword: string }>(`/api/v1/admin/users/${id}/temp-password`, {});
  }

  downloadTempPasswordPdf(id: string): Observable<Blob> {
    return this.http.get(`/api/v1/admin/users/${id}/temp-password.pdf`, { responseType: 'blob' });
  }

  setConsent(patientId: string): Observable<void> {
    return this.http.post<void>(`/api/v1/admin/patients/${patientId}/consent`, {});
  }

  assign(doctorId: string, patientId: string): Observable<void> {
    return this.http.put<void>('/api/v1/admin/assignments', { doctorId, patientId });
  }

  unassign(doctorId: string, patientId: string): Observable<void> {
    return this.http.request<void>('DELETE', '/api/v1/admin/assignments', {
      body: { doctorId, patientId },
    });
  }

  listDoctorPatients(doctorId: string): Observable<
    { patientId: string; email: string; lastName: string | null; firstName: string | null }[]
  > {
    return this.http.get<
      { patientId: string; email: string; lastName: string | null; firstName: string | null }[]
    >(`/api/v1/admin/doctors/${doctorId}/patients`);
  }

  listDevices(): Observable<Device[]> {
    return this.http.get<Device[]>('/api/v1/admin/devices');
  }

  createDevice(body: {
    name: string;
    serialNumber: string;
    model?: string | null;
    description?: string | null;
  }): Observable<Device> {
    return this.http.post<Device>('/api/v1/admin/devices', body);
  }

  updateDevice(
    id: string,
    body: { name: string; model?: string | null; description?: string | null },
  ): Observable<Device> {
    return this.http.put<Device>(`/api/v1/admin/devices/${id}`, body);
  }

  deleteDevice(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/admin/devices/${id}`);
  }

  listLogs(opts?: {
    level?: string;
    module?: string;
    code?: string;
    page?: number;
  }): Observable<PagedLogs> {
    let params = new HttpParams().set('page', String(opts?.page ?? 1)).set('pageSize', '50');
    if (opts?.level) {
      params = params.set('level', opts.level);
    }
    if (opts?.module) {
      params = params.set('module', opts.module);
    }
    if (opts?.code) {
      params = params.set('code', opts.code);
    }
    return this.http.get<PagedLogs>('/api/v1/admin/logs', { params });
  }
}
