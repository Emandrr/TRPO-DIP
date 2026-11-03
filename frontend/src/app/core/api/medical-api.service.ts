import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateDiagnosisRequest,
  Diagnosis,
  MedicalCard,
  Prescription,
  Recommendation,
  UpdateDiagnosisRequest,
  UpsertMedicalCardRequest,
  UpsertPrescriptionRequest,
  UpsertRecommendationRequest,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class MedicalApiService {
  private readonly http = inject(HttpClient);

  getMedicalCard(patientId: string): Observable<MedicalCard> {
    return this.http.get<MedicalCard>(`/api/v1/medical-cards/${patientId}`);
  }

  upsertMedicalCard(patientId: string, body: UpsertMedicalCardRequest): Observable<MedicalCard> {
    return this.http.put<MedicalCard>(`/api/v1/medical-cards/${patientId}`, body);
  }

  deleteMedicalCard(patientId: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/medical-cards/${patientId}`);
  }

  listDiagnoses(patientId: string): Observable<Diagnosis[]> {
    return this.http.get<Diagnosis[]>(`/api/v1/patients/${patientId}/diagnoses`);
  }

  createDiagnosis(body: CreateDiagnosisRequest): Observable<Diagnosis> {
    return this.http.post<Diagnosis>('/api/v1/diagnoses', body);
  }

  updateDiagnosis(id: string, body: UpdateDiagnosisRequest): Observable<Diagnosis> {
    return this.http.put<Diagnosis>(`/api/v1/diagnoses/${id}`, body);
  }

  deleteDiagnosis(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/diagnoses/${id}`);
  }

  listRecommendations(diagnosisId: string): Observable<Recommendation[]> {
    return this.http.get<Recommendation[]>(`/api/v1/diagnoses/${diagnosisId}/recommendations`);
  }

  createRecommendation(diagnosisId: string, body: UpsertRecommendationRequest): Observable<Recommendation> {
    return this.http.post<Recommendation>(`/api/v1/diagnoses/${diagnosisId}/recommendations`, body);
  }

  updateRecommendation(id: string, body: UpsertRecommendationRequest): Observable<Recommendation> {
    return this.http.put<Recommendation>(`/api/v1/recommendations/${id}`, body);
  }

  deleteRecommendation(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/recommendations/${id}`);
  }

  listPrescriptions(diagnosisId: string): Observable<Prescription[]> {
    return this.http.get<Prescription[]>(`/api/v1/diagnoses/${diagnosisId}/prescriptions`);
  }

  createPrescription(diagnosisId: string, body: UpsertPrescriptionRequest): Observable<Prescription> {
    return this.http.post<Prescription>(`/api/v1/diagnoses/${diagnosisId}/prescriptions`, body);
  }

  updatePrescription(id: string, body: UpsertPrescriptionRequest): Observable<Prescription> {
    return this.http.put<Prescription>(`/api/v1/prescriptions/${id}`, body);
  }

  deletePrescription(id: string): Observable<void> {
    return this.http.delete<void>(`/api/v1/prescriptions/${id}`);
  }
}
