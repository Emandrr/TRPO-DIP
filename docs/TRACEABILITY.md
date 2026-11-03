# Трассируемость ТЗ → задачи → проверки

Сводка по разделу 9 плана. Статус на 2026-10-04.

| Пункт ТЗ | Задачи | Проверка | Статус |
|---|---|---|---|
| 4.1.1 архитектура, REST, JSON, ORM, HTTPS | P0.2, P0.3, P0.5, P8.5 | build, Swagger, headers | OK |
| 4.1.1 журнал ошибок загрузки | P1.2, P3.4, P7.2 | CSV 422 + logs | OK |
| 4.1.2 персонал | P9.1 | user/admin guide | OK |
| 4.1.3 надёжность | P8.1, P8.3, P3.1, P5.1, P9.3 | worker resume, validators, protocol | OK |
| 4.1.4 безопасность | P2.*, P8.4–P8.7 | JWT, rate limit, SQL-safety test | OK |
| 4.1.5 эргономика, 360 px, русский | P7.0, P7.6 | UI RU; e2e #12 вручную | Частично |
| 4.1.8 резервное копирование | P8.2 | backup/restore scripts | OK |
| 4.1.10 лицензии | P0.6 | LICENSES.md | OK |
| 4.1.11 REST, OpenAPI | P0.5 | Swagger | OK |
| 4.2.1 аутентификация | P2.1–P2.6 | AuthFlowTests | OK |
| 4.2.2 ЭКГ | P3.*, P4.* | upload + analysis unit | OK |
| 4.2.3 медкарты | P5.* | MedicalDataService + tests | OK |
| 4.2.4 служебная информация | P1.2, P1.4, P1.5 | devices/logs | OK |
| 4.2.5 ЛК пациента | P7.2 | frontend patient routes | OK |
| 4.2.6 РМ врача | P7.3, P7.4, P4.10 | doctor UI + report API | OK |
| 4.2.7 админ-панель | P6.*, P7.5, P2.6 | admin UI + PDF | OK |
| 4.3.1 математика (Д.1) | P4.1–P4.7 | Metric/QRS/Filter tests | OK* |
| 4.3.2 информационное обеспечение | P1.1 | schema/migrations | OK |
| 4.3.4 ПО (стек) | §2 плана | net10 + Angular 21 | OK |
| 4.3.7 подтверждения, руководства | P7.4, P7.5, P9.1 | confirm dialogs + guides | OK |
| 5.1–5.2 испытания и приёмка | P9.2–P9.4 | PMI, protocol, act | OK |
| Приложение А | P3.1 | CsvValidatorTests | OK |
| Приложение В | P5.1, P7.6 | MedDataValidators + UI | OK |
| Приложение Г | P4.2–P4.4 | filter/QRS tests | OK |
| Приложение Д | P4.5–P4.6 | metrics tests | OK* |

\* Упрощены/частичны: ST, T_WAVE, QRS_SHAPE; полный e2e 24 ч и perf — вне текущего объёма.

## Definition of Done (план §10) — чеклист

- [x] Задачи P0–P9 в основном закрыты; unit зелёные локально  
- [ ] Покрытие % по плану (формальный coverage report)  
- [ ] Все 12 e2e пройдены на стенде (см. test-protocol)  
- [x] Матрица прав реализована + частично покрыта integration  
- [ ] Запись 24 ч / 1 ГБ — perf-прогон  
- [ ] Экспорт `openapi.json` в репозиторий  
- [x] LICENSES.md  
- [x] Политика логов (без паролей/токенов в коде путей логирования)  
- [ ] Восстановление из бэкапа подтверждено на стенде  
- [x] Руководства, ПМИ, протокол, акт (шаблоны) подготовлены  
