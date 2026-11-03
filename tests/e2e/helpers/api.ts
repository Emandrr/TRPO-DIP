import { APIRequestContext, expect } from '@playwright/test';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

const API = process.env.API_BASE ?? 'http://localhost:8080';

export type Tokens = {
  accessToken: string;
  refreshToken: string;
  userId: string;
  mustChangePassword: boolean;
  role: string;
};

export async function apiJson(
  request: APIRequestContext,
  method: string,
  urlPath: string,
  options: { token?: string; data?: unknown; retries?: number } = {},
) {
  const headers: Record<string, string> = {};
  if (options.token) headers.Authorization = `Bearer ${options.token}`;
  const retries = options.retries ?? 6;
  let lastStatus = 0;
  let text = '';
  let json: any = null;
  for (let attempt = 0; attempt <= retries; attempt++) {
    const res = await request.fetch(`${API}${urlPath}`, {
      method,
      headers,
      data: options.data,
      failOnStatusCode: false,
    });
    lastStatus = res.status();
    text = await res.text();
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = null;
    }
    if (lastStatus !== 429) {
      break;
    }
    await new Promise((r) => setTimeout(r, 1200 * (attempt + 1)));
  }
  return { status: lastStatus, json, text };
}

export async function login(request: APIRequestContext, email: string, password: string): Promise<Tokens> {
  const res = await apiJson(request, 'POST', '/api/v1/auth/login', { data: { email, password } });
  expect(res.status, `login ${email}`).toBe(200);
  return {
    accessToken: res.json.accessToken,
    refreshToken: res.json.refreshToken,
    userId: res.json.userId,
    mustChangePassword: !!res.json.mustChangePassword,
    role: res.json.role,
  };
}

export async function registerPatient(request: APIRequestContext, email: string, password = 'Password1') {
  const res = await apiJson(request, 'POST', '/api/v1/auth/register', { data: { email, password } });
  expect(res.status).toBe(200);
  return res.json as Tokens & { accessToken: string; userId: string };
}

function stampAt(offsetMs: number): string {
  const t = new Date(2026, 0, 1, 8, 0, 0, 0);
  t.setMilliseconds(t.getMilliseconds() + offsetMs);
  const p = (n: number, w = 2) => String(n).padStart(w, '0');
  return `${t.getFullYear()}-${p(t.getMonth() + 1)}-${p(t.getDate())} ${p(t.getHours())}:${p(t.getMinutes())}:${p(t.getSeconds())}.${p(t.getMilliseconds(), 3)}`;
}

export function buildCsv(sampleCount = 2500, badValueAt?: number): string {
  const lines = ['timestamp,value,errors'];
  for (let i = 0; i < sampleCount; i++) {
    const value = badValueAt === i ? 5001 : 100;
    lines.push(`${stampAt(i * 4)},${value},NoError`);
  }
  return lines.join('\n');
}

export async function uploadCsv(
  request: APIRequestContext,
  token: string,
  deviceId: string,
  csv: string,
  fileName = 'ecg.csv',
) {
  const tmp = path.join(os.tmpdir(), `saekg-${Date.now()}-${fileName}`);
  fs.writeFileSync(tmp, csv, 'utf8');
  const res = await request.post(`${API}/api/v1/ecg-records`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      deviceId,
      file: {
        name: fileName,
        mimeType: 'text/csv',
        buffer: fs.readFileSync(tmp),
      },
    },
    failOnStatusCode: false,
  });
  fs.unlinkSync(tmp);
  const text = await res.text();
  let json: any = null;
  try {
    json = JSON.parse(text);
  } catch {
    json = null;
  }
  return { status: res.status(), json };
}

export async function waitRecordStatus(
  request: APIRequestContext,
  token: string,
  recordId: string,
  statuses: string[],
  timeoutMs = 180_000,
) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await apiJson(request, 'GET', `/api/v1/ecg-records/${recordId}`, { token });
    if (res.status === 200 && statuses.includes(res.json.status)) {
      return res.json;
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`timeout waiting ${recordId} in ${statuses.join(',')}`);
}
