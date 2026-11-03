export interface MedicalCard {
  id: string;
  patientId: string;
  lastName: string | null;
  firstName: string | null;
  middleName: string | null;
  birthDate: string;
  sex: string;
}

export interface UpsertMedicalCardRequest {
  lastName: string;
  firstName: string;
  middleName?: string | null;
  birthDate: string;
  sex: string;
}

export interface Diagnosis {
  id: string;
  patientId: string;
  doctorId: string;
  recordId: string | null;
  reportId: string | null;
  title: string;
  description: string | null;
  diagnosedOn: string;
  anomalyIds: string[];
}

export interface CreateDiagnosisRequest {
  patientId: string;
  title: string;
  description?: string | null;
  diagnosedOn: string;
  recordId?: string | null;
  reportId?: string | null;
  anomalyIds?: string[] | null;
}

export interface UpdateDiagnosisRequest {
  title: string;
  description?: string | null;
  diagnosedOn: string;
  anomalyIds?: string[] | null;
}

export interface Recommendation {
  id: string;
  diagnosisId: string;
  text: string;
  comment: string | null;
}

export interface UpsertRecommendationRequest {
  text: string;
  comment?: string | null;
}

export interface Prescription {
  id: string;
  diagnosisId: string;
  drugName: string;
  startDate: string;
  endDate: string;
  comment: string | null;
}

export interface UpsertPrescriptionRequest {
  drugName: string;
  startDate: string;
  endDate: string;
  comment?: string | null;
}

export interface EcgRecord {
  id: string;
  patientId: string;
  deviceId: string;
  uploadedAt: string;
  originalFilename: string;
  sampleCount: number;
  startedAt: string;
  endedAt: string;
  status: string;
  failureCode: string | null;
  unusableRatio: number;
  rawPurged: boolean;
}

export interface Device {
  id: string;
  name: string;
  serialNumber: string;
  model: string | null;
  description: string | null;
}

export interface AssignedPatient {
  patientId: string;
  email: string;
  consentSigned: boolean;
  assignedAt: string;
  lastName: string | null;
  firstName: string | null;
  middleName: string | null;
}

export interface ApiError {
  code: string;
  message: string;
  details?: { check?: number; line?: number | null; errors?: string[] };
  requestId?: string;
}

export interface DownsampledPoint {
  offsetMs: number;
  minUv: number;
  maxUv: number;
}

export interface EcgAnomaly {
  id: string;
  code: string;
  title: string;
  startMs: number;
  endMs: number;
  valueMin: number | null;
  valueMax: number | null;
  unit: string | null;
  reference: string | null;
}

export interface EcgGap {
  startMs: number;
  endMs: number;
  reason: string;
}

export interface EcgReport {
  id: string;
  recordId: string;
  lead: string;
  status: string;
  startedAt: string;
  endedAt: string;
  durationSec: number;
  unusablePercent: number;
  summary: {
    hrMean: number | null;
    hrMin: number | null;
    hrMax: number | null;
    rrMean: number | null;
    qtcMean: number | null;
    rAmpMean: number | null;
    cyclesTotal: number;
    cyclesUndetermined: number;
  };
  gaps: EcgGap[];
  anomalies: EcgAnomaly[];
}
