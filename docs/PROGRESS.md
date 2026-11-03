# PROGRESS.md

## Текущая фаза

**Фаза 9 — документация и приёмка** (закрыта по артефактам)

## Сделано

### Реализация P0–P8
Backend + frontend (роли Patient/Doctor/Admin), анализ ЭКГ, медданные, PDF, purge, backup, security. Unit: **75**.

### P9
- `docs/user-guide.md` — пациент и врач  
- `docs/admin-guide.md` — эксплуатация, бэкапы, конфиг  
- `docs/PMI.md` — ПМИ (ГОСТ 34.603-92, матрица ТЗ)  
- `docs/test-protocol.md` — бланк прогона + e2e §8  
- `docs/acceptance-act.md` — шаблон акта  
- `docs/TRACEABILITY.md` — ТЗ → задачи → статус  
- `tests/e2e` — Playwright smoke + skip-заготовки сценариев §8  

## Осталось на стенде заказчика
- Проставить результаты в `test-protocol.md` (12 e2e, health ≥ 95 %, restore).  
- Подписать акт приёмки.  
- Опционально: perf 24 ч, экспорт openapi.json, доработка ST/T_WAVE.

Обновлено: 2026-10-04.
