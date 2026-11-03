import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Device, DownsampledPoint, EcgRecord, EcgReport } from './api.models';

@Injectable({ providedIn: 'root' })
export class EcgApiService {
  private readonly http = inject(HttpClient);

  listRecords(): Observable<EcgRecord[]> {
    return this.http.get<EcgRecord[]>('/api/v1/ecg-records');
  }

  getRecord(id: string): Observable<EcgRecord> {
    return this.http.get<EcgRecord>(`/api/v1/ecg-records/${id}`);
  }

  getReport(id: string): Observable<EcgReport> {
    return this.http.get<EcgReport>(`/api/v1/ecg-records/${id}/report`);
  }

  getSignal(id: string, fromMs: number, toMs: number, maxPoints = 2000): Observable<DownsampledPoint[]> {
    return this.http.get<DownsampledPoint[]>(`/api/v1/ecg-records/${id}/signal`, {
      params: {
        fromMs: String(fromMs),
        toMs: String(toMs),
        maxPoints: String(maxPoints),
      },
    });
  }

  listDevices(): Observable<Device[]> {
    return this.http.get<Device[]>('/api/v1/devices');
  }

  upload(deviceId: string, file: File): Observable<{ recordId: string }> {
    const form = new FormData();
    form.append('deviceId', deviceId);
    form.append('file', file, file.name);
    return this.http.post<{ recordId: string }>('/api/v1/ecg-records', form);
  }

  acceptReport(recordId: string, comment?: string | null): Observable<void> {
    return this.http.post<void>(`/api/v1/ecg-records/${recordId}/report/accept`, { comment: comment ?? null });
  }

  rejectReport(recordId: string, comment?: string | null): Observable<void> {
    return this.http.post<void>(`/api/v1/ecg-records/${recordId}/report/reject`, { comment: comment ?? null });
  }

  reanalyze(recordId: string): Observable<void> {
    return this.http.post<void>(`/api/v1/ecg-records/${recordId}/reanalyze`, {});
  }
}
