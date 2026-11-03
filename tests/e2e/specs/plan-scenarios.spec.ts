import { test, expect, APIRequestContext } from '@playwright/test';
import {
  apiJson,
  buildCsv,
  login,
  registerPatient,
  uploadCsv,
  waitRecordStatus,
} from '../helpers/api';

const ADMIN_EMAIL = process.env.ADMIN_EMAIL ?? 'admin@saekg.local';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD ?? 'AdminPass1';

async function ensureAdmin(request: APIRequestContext) {
  return login(request, ADMIN_EMAIL, ADMIN_PASSWORD);
}

async function createDoctor(request: APIRequestContext, adminToken: string, email: string) {
  const created = await apiJson(request, 'POST', '/api/v1/admin/users', {
    token: adminToken,
    data: { email, role: 'Doctor' },
  });
  expect(created.status).toBe(201);
  const temp = created.json.temporaryPassword as string;
  const id = created.json.user.id as string;
  const pdf = await request.get(`http://localhost:8080/api/v1/admin/users/${id}/temp-password.pdf`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    failOnStatusCode: false,
  });
  expect(pdf.status()).toBe(200);
  const pdf2 = await request.get(`http://localhost:8080/api/v1/admin/users/${id}/temp-password.pdf`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    failOnStatusCode: false,
  });
  expect(pdf2.status()).toBe(410);

  let doc = await login(request, email, temp);
  expect(doc.mustChangePassword).toBeTruthy();
  const blocked = await apiJson(request, 'GET', '/api/v1/doctor/patients', { token: doc.accessToken });
  expect(blocked.status).toBe(403);
  const changed = await apiJson(request, 'POST', '/api/v1/auth/change-password', {
    token: doc.accessToken,
    data: { currentPassword: temp, newPassword: 'DoctorPass1' },
  });
  expect(changed.status).toBe(204);
  doc = await login(request, email, 'DoctorPass1');
  expect(doc.mustChangePassword).toBeFalsy();
  return { id, token: doc.accessToken };
}

test.describe('Plan §8 API e2e', () => {
  test.describe.configure({ mode: 'serial' });
  test.beforeEach(async ({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'API e2e once on chromium');
  });

  test('1-3 register consent upload Done accept diagnosis', async ({ request }) => {
    const suffix = Date.now().toString(36);
    const admin = await ensureAdmin(request);
    const device = await apiJson(request, 'POST', '/api/v1/admin/devices', {
      token: admin.accessToken,
      data: { name: `Holter-${suffix}`, serialNumber: `SN-${suffix}` },
    });
    expect(device.status).toBe(201);
    const deviceId = device.json.id as string;

    const patientEmail = `p_${suffix}@example.com`;
    const patient = await registerPatient(request, patientEmail);
    const noConsent = await uploadCsv(request, patient.accessToken, deviceId, buildCsv());
    expect(noConsent.status).toBe(403);

    expect(
      (
        await apiJson(request, 'POST', `/api/v1/admin/patients/${patient.userId}/consent`, {
          token: admin.accessToken,
        })
      ).status,
    ).toBe(204);

    const doctor = await createDoctor(request, admin.accessToken, `doc_${suffix}@example.com`);
    expect(
      (
        await apiJson(request, 'PUT', '/api/v1/admin/assignments', {
          token: admin.accessToken,
          data: { doctorId: doctor.id, patientId: patient.userId },
        })
      ).status,
    ).toBe(204);

    expect(
      (
        await apiJson(request, 'PUT', `/api/v1/medical-cards/${patient.userId}`, {
          token: doctor.token,
          data: {
            lastName: 'Иванов',
            firstName: 'Иван',
            middleName: null,
            birthDate: '15.03.1990',
            sex: 'мужской',
          },
        })
      ).status,
    ).toBe(200);

    const uploaded = await uploadCsv(request, patient.accessToken, deviceId, buildCsv());
    expect(uploaded.status).toBe(202);
    const recordId = uploaded.json.recordId as string;
    const done = await waitRecordStatus(request, patient.accessToken, recordId, ['Done', 'Failed']);
    expect(done.status).toBe('Done');

    const before = await apiJson(request, 'GET', `/api/v1/ecg-records/${recordId}/report`, {
      token: patient.accessToken,
    });
    expect(before.status).toBe(404);

    const doctorReport = await apiJson(request, 'GET', `/api/v1/ecg-records/${recordId}/report`, {
      token: doctor.token,
    });
    expect(doctorReport.status).toBe(200);

    expect(
      (
        await apiJson(request, 'POST', `/api/v1/ecg-records/${recordId}/report/accept`, {
          token: doctor.token,
          data: { comment: 'ok' },
        })
      ).status,
    ).toBe(204);

    const after = await apiJson(request, 'GET', `/api/v1/ecg-records/${recordId}/report`, {
      token: patient.accessToken,
    });
    expect(after.status).toBe(200);

    const diag = await apiJson(request, 'POST', '/api/v1/diagnoses', {
      token: doctor.token,
      data: {
        patientId: patient.userId,
        title: 'Аритмия',
        description: 'e2e',
        diagnosedOn: '11.10.2026',
        recordId,
        reportId: doctorReport.json.id,
        anomalyIds: [],
      },
    });
    expect(diag.status).toBe(201);
    const diagnosisId = diag.json.id as string;
    expect(
      (
        await apiJson(request, 'POST', `/api/v1/diagnoses/${diagnosisId}/recommendations`, {
          token: doctor.token,
          data: { text: 'Отдых', comment: null },
        })
      ).status,
    ).toBe(201);
    expect(
      (
        await apiJson(request, 'POST', `/api/v1/diagnoses/${diagnosisId}/prescriptions`, {
          token: doctor.token,
          data: {
            drugName: 'Препарат',
            startDate: '11.10.2026',
            endDate: '18.10.2026',
            comment: null,
          },
        })
      ).status,
    ).toBe(201);
    const patientDiags = await apiJson(request, 'GET', `/api/v1/patients/${patient.userId}/diagnoses`, {
      token: patient.accessToken,
    });
    expect(patientDiags.status).toBe(200);
    expect(patientDiags.json.length).toBeGreaterThan(0);
  });

  test('4 foreign doctor 403; patient A vs B 404', async ({ request }) => {
    const suffix = Date.now().toString(36);
    const admin = await ensureAdmin(request);
    const a = await registerPatient(request, `a_${suffix}@example.com`);
    const b = await registerPatient(request, `b_${suffix}@example.com`);
    const doc = await createDoctor(request, admin.accessToken, `d4_${suffix}@example.com`);
    await apiJson(request, 'PUT', '/api/v1/admin/assignments', {
      token: admin.accessToken,
      data: { doctorId: doc.id, patientId: a.userId },
    });
    await apiJson(request, 'PUT', `/api/v1/medical-cards/${a.userId}`, {
      token: doc.token,
      data: {
        lastName: 'A',
        firstName: 'A',
        middleName: null,
        birthDate: '15.03.1990',
        sex: 'мужской',
      },
    });
    const foreign = await createDoctor(request, admin.accessToken, `d4f_${suffix}@example.com`);
    const forbidden = await apiJson(request, 'GET', `/api/v1/medical-cards/${a.userId}`, {
      token: foreign.token,
    });
    expect(forbidden.status).toBe(403);
    const notFound = await apiJson(request, 'GET', `/api/v1/medical-cards/${a.userId}`, {
      token: b.accessToken,
    });
    expect(notFound.status).toBe(404);
  });

  test('5 CSV check 5 + admin log; 6 unusable ratio; 8 block', async ({ request }) => {
    const suffix = Date.now().toString(36);
    const admin = await ensureAdmin(request);
    const device = await apiJson(request, 'POST', '/api/v1/admin/devices', {
      token: admin.accessToken,
      data: { name: `D5-${suffix}`, serialNumber: `SN5-${suffix}` },
    });
    const patient = await registerPatient(request, `p5_${suffix}@example.com`);
    await apiJson(request, 'POST', `/api/v1/admin/patients/${patient.userId}/consent`, {
      token: admin.accessToken,
    });
    const doctor = await createDoctor(request, admin.accessToken, `d5_${suffix}@example.com`);
    await apiJson(request, 'PUT', '/api/v1/admin/assignments', {
      token: admin.accessToken,
      data: { doctorId: doctor.id, patientId: patient.userId },
    });
    await apiJson(request, 'PUT', `/api/v1/medical-cards/${patient.userId}`, {
      token: doctor.token,
      data: {
        lastName: 'B',
        firstName: 'B',
        middleName: null,
        birthDate: '15.03.1990',
        sex: 'мужской',
      },
    });

    const bad = await uploadCsv(request, patient.accessToken, device.json.id, buildCsv(2500, 5));
    expect(bad.status).toBe(422);
    expect(bad.json.code).toBe('CSV_CHECK_FAILED');
    expect(bad.json.details.check).toBe(5);
    await new Promise((r) => setTimeout(r, 1500));
    const logs = await apiJson(request, 'GET', '/api/v1/admin/logs?module=CsvValidation&code=CSV_CHECK_5', {
      token: admin.accessToken,
    });
    expect(logs.status).toBe(200);
    expect(logs.json.items.length).toBeGreaterThan(0);

    const err31 = Array.from({ length: 2500 }, (_, i) =>
      i < 775 ? 'Error' : 'NoError',
    );
    // 31% reject via helper-like body
    const lines31 = ['timestamp,value,errors'];
    const start = new Date(2026, 0, 1, 8, 0, 0, 0).getTime();
    for (let i = 0; i < 2500; i++) {
      const t = new Date(start + i * 4);
      const stamp = `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}-${String(t.getDate()).padStart(2, '0')} ${String(t.getHours()).padStart(2, '0')}:${String(t.getMinutes()).padStart(2, '0')}:${String(t.getSeconds()).padStart(2, '0')}.${String(t.getMilliseconds()).padStart(3, '0')}`;
      lines31.push(`${stamp},100,${err31[i]}`);
    }
    const up31 = await uploadCsv(request, patient.accessToken, device.json.id, lines31.join('\n'));
    expect(up31.status).toBe(422);
    expect(up31.json.details.check).toBe(12);

    const block = await registerPatient(request, `blk_${suffix}@example.com`);
    expect(
      (
        await apiJson(request, 'POST', `/api/v1/admin/users/${block.userId}/block`, {
          token: admin.accessToken,
        })
      ).status,
    ).toBe(204);
    const blockedCall = await apiJson(request, 'GET', '/api/v1/ecg-records', {
      token: block.accessToken,
    });
    expect([401, 403]).toContain(blockedCall.status);
  });

  test('7 temp password PDF one-shot already covered in createDoctor helper', async ({ request }) => {
    const admin = await ensureAdmin(request);
    await createDoctor(request, admin.accessToken, `pdf_${Date.now().toString(36)}@example.com`);
  });

  test('9 soft delete medical card', async ({ request }) => {
    const suffix = Date.now().toString(36);
    const admin = await ensureAdmin(request);
    const patient = await registerPatient(request, `p9_${suffix}@example.com`);
    const doctor = await createDoctor(request, admin.accessToken, `d9_${suffix}@example.com`);
    await apiJson(request, 'PUT', '/api/v1/admin/assignments', {
      token: admin.accessToken,
      data: { doctorId: doctor.id, patientId: patient.userId },
    });
    await apiJson(request, 'PUT', `/api/v1/medical-cards/${patient.userId}`, {
      token: doctor.token,
      data: {
        lastName: 'C',
        firstName: 'C',
        middleName: null,
        birthDate: '15.03.1990',
        sex: 'мужской',
      },
    });
    expect(
      (await apiJson(request, 'GET', `/api/v1/medical-cards/${patient.userId}`, { token: patient.accessToken }))
        .status,
    ).toBe(200);
    expect(
      (await apiJson(request, 'DELETE', `/api/v1/medical-cards/${patient.userId}`, { token: admin.accessToken }))
        .status,
    ).toBe(204);
    expect(
      (await apiJson(request, 'GET', `/api/v1/medical-cards/${patient.userId}`, { token: patient.accessToken }))
        .status,
    ).toBe(404);
  });
});

test.describe('Smoke auth screens', () => {
  test('login page renders in Russian', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByText('СА-ЭКГ')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Войти' })).toBeVisible();
  });

  test('register page available', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByRole('button', { name: 'Зарегистрироваться' })).toBeVisible();
  });
});

test.describe('Adaptivity 360px (§8.12)', () => {
  const screens = ['/login', '/register', '/change-password'];

  for (const route of screens) {
    test(`${route} has no horizontal overflow at 360`, async ({ page }, testInfo) => {
      test.skip(testInfo.project.name !== 'mobile-360', 'only mobile-360');
      await page.goto(route);
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
      expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 1);
    });
  }
});
