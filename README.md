# СА-ЭКГ — Система анализа и хранения кардиологических исследований

Система принимает готовые CSV-файлы ЭКГ (250 Гц), валидирует и фильтрует сигнал, автоматически выявляет нарушения ритма и формирует отчёт; ведёт медкарту, диагнозы, рекомендации и назначения.

## Стек


| Слой           | Технология                                             |
| -------------- | ------------------------------------------------------ |
| Backend        | .NET 10, ASP.NET Core Web API, EF Core (Npgsql), JWT   |
| БД             | PostgreSQL 16; DuckDB (сигналы)                        |
| Frontend       | Angular 21, TypeScript strict, Angular Material, uPlot |
| Инфраструктура | Docker Compose                                         |


## Структура репозитория

```
/backend   — ASP.NET Core solution
/frontend  — Angular SPA
/docs      — план, руководства, ПМИ, приёмка, эталоны UX
/infra     — Docker Compose, backup/restore
/tests     — e2e (Playwright)
```

## Быстрый старт

```bash
docker compose -f infra/docker-compose.yml up --build
```

- API: `http://localhost:8080/health`
- Web (заглушка nginx): `http://localhost:4200`
- Swagger (Development): `http://localhost:8080/swagger`

Frontend в режиме разработки:

```bash
cd frontend && npm ci && npm start
```

Backend unit-тесты:

```bash
cd backend && dotnet test
```

Бэкап:

```bash
cd infra && docker compose --profile backup run --rm backup
```

## Документация


| Документ                                                        | Описание                       |
| --------------------------------------------------------------- | ------------------------------ |
| [План](docs/PLAN.md)                                            | фазы и требования              |
| [Прогресс](docs/PROGRESS.md)                                    | текущее состояние              |
| [Руководство пользователя](docs/user-guide.md)                  | пациент и врач                 |
| [Руководство администратора](docs/admin-guide.md)               | эксплуатация                   |
| [ПМИ](docs/PMI.md)                                              | программа и методика испытаний |
| [Протокол испытаний](docs/test-protocol.md)                     | бланк прогона                  |
| [Акт приёмки](docs/acceptance-act.md)                           | шаблон                         |
| [Трассируемость](docs/TRACEABILITY.md)                          | ТЗ → задачи → статус           |
| [Лицензии](docs/LICENSES.md)                                    | сторонние зависимости          |
| [Решения](docs/DECISIONS.md) / [Допущения](docs/ASSUMPTIONS.md) |                                |


Эталоны UX: [просмотрщик 24 ч](docs/references/ecg-24h-viewer.html), [пример отчёта](docs/references/ecg-report-example.png).

## Роли

- **Patient** — загрузка ЭКГ, просмотр принятых отчётов и медданных
- **Doctor** — закреплённые пациенты, принятие/отклонение отчётов, диагнозы
- **Admin** — пользователи, устройства, согласия, логи, бэкапы

Съём ЭКГ с датчика в систему не входит.