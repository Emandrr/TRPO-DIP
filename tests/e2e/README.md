# E2E сценарии СА-ЭКГ (Playwright)

Сценарии соответствуют разделу 8 `docs/PLAN.md` и бланку `docs/test-protocol.md`.

## Подготовка

1. Поднять API + БД: `docker compose -f infra/docker-compose.yml up -d --build`
2. Frontend: `cd frontend && npm start`
3. Установить Playwright (из этой папки):

```bash
cd tests/e2e
npm init -y
npm install -D @playwright/test
npx playwright install chromium
```

## Запуск

```bash
npx playwright test
```

Переменные:

| Переменная | По умолчанию |
|---|---|
| `BASE_URL` | `http://localhost:4200` |
| `API_URL` | `http://localhost:8080` |

## Статус сценариев

Автотесты-заготовки помечены `test.fix` / требуют сид-данных. Прогон вручную — по `docs/test-protocol.md`.
