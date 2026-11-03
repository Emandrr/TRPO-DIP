import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AssignedPatient } from './api.models';

@Injectable({ providedIn: 'root' })
export class DoctorApiService {
  private readonly http = inject(HttpClient);

  listPatients(): Observable<AssignedPatient[]> {
    return this.http.get<AssignedPatient[]>('/api/v1/doctor/patients');
  }
}
