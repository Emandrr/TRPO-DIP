# Руководство администратора СА-ЭКГ

## 1. Развёртывание

### 1.1 Docker Compose

```bash
cd infra
docker compose up -d --build
```

Сервисы:

| Сервис | Порт | Назначение |
|---|---|---|
| `postgres` | 5432 | PostgreSQL 16 |
| `api` | 8080 | ASP.NET Core API |
| `web` | 4200 | веб (заглушка nginx или собранный frontend) |

Health: `GET http://localhost:8080/health` → 200.

Перезапуск: `restart: unless-stopped` для postgres/api/web.

### 1.2 Frontend (разработка)

```bash
cd frontend
npm ci
npm start
```

Прокси `/api` → `http://localhost:8080` (`proxy.conf.json`).

### 1.3 Конфигурация API

Ключевые параметры (`appsettings` / env):

| Ключ | Назначение |
|---|---|
| `ConnectionStrings__Postgres` | строка PostgreSQL |
| `DuckDb__Path` | путь к файлу DuckDB |
| `Jwt__SigningKey` | ≥ 32 символов |
| `Security__EnforceInternalNetworkCheck` | ограничение Doctor/Admin внутренней сетью |
| `Cors__Origins` | whitelist origin для браузера |

---

## 2. Админ-панель (`/admin`)

Роль `Admin`. Создаётся вручную в БД или через уже существующего админа.

### 2.1 Пользователи

- Список пользователей (роль, блок, дата создания).
- Создание **Doctor** / **Admin** → временный пароль + одноразовый PDF (`GET .../temp-password.pdf`, повтор → 410).
- Блокировка / разблокировка (refresh-токены отзываются при блоке).
- Для пациента: кнопка **Согласие**.

### 2.2 Закрепления

Выберите врача и пациента → **Закрепить**. Список закреплённых можно открепить.

### 2.3 Устройства

CRUD устройств (мягкое удаление). Пациент выбирает устройство при загрузке CSV.

### 2.4 Логи

Фильтры: level, module, code. Записи валидации CSV и медданных попадают с module/code.

### 2.5 Медданные

Просмотр медкарты и диагнозов пациента; soft delete с подтверждением.

---

## 3. Резервное копирование

Скрипты: `infra/backup.sh`, `infra/restore.sh`.

Однократный бэкап через compose-профиль:

```bash
cd infra
docker compose --profile backup run --rm backup
```

Восстановление:

```bash
docker compose run --rm -e BACKUP_ID=<stamp> backup /bin/sh /restore.sh <stamp>
```

Хранится до 14 копий (`BACKUP_KEEP`). Содержимое: `postgres.dump` + `ecg.duckdb`.

---

## 4. Фоновая очистка (D7)

| Данные | Срок | Результат |
|---|---|---|
| `raw` в DuckDB | 30 суток после загрузки | удалены; `RawPurged = true` |
| `filtered` + fiducials | 10 лет | удалены; `FilteredPurged = true` |

Метаданные записи, отчёт, аномалии, диагнозы сохраняются. Job: `PurgeJobsWorker` (цикл ~1 ч).

---

## 5. Эксплуатация

- Зависшие задания анализа (`Processing` с истекшим `locked_until`) возвращаются в `Queued` при работе worker.
- Rate limit на `/api/v1/auth/*`: 30 запросов/мин с IP.
- Заголовки: `X-Content-Type-Options`, `X-Frame-Options`, CSP, HSTS (при HTTPS).
- Логи не должны содержать пароли, JWT и содержимое медданных.
- Лицензии сторонних пакетов: `docs/LICENSES.md`.

### 5.1 Создание первого админа (SQL / seed)

Через регистрацию нельзя создать Admin. Вариант: временно создать пользователя в БД с ролью `Admin` и хешем пароля ASP.NET Identity, либо использовать тестовый сид в Integration-тестах как образец.

### 5.2 OpenAPI

В Development: Swagger UI на API (`/swagger`).
