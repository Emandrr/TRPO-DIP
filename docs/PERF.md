# PERF.md — производительность СА-ЭКГ

Измерения на dev-стенде (Docker Desktop / локальный `dotnet test`). Повторять при значимых изменениях анализа или CSV.

## Методика

| Сценарий | Команда / условие | Критерий плана |
|---|---|---|
| CSV streaming smoke | `dotnet test --filter StreamingUploadMemory` | пик доп. памяти валидатора << 300 МБ на 250k отсчётов |
| Отказ >1 ГБ до чтения | `Declared_length_over_1gb_rejected_before_full_read` | check 1, `BytesRead = 0` |
| Анализ 1 мин | `Analyze_one_minute_synthetic_signal_under_limits` | < 20 с, < 400 МБ прироста |
| Анализ 24 ч | `SAEKG_PERF=1 dotnet test --filter Analyze_optional_24h` | ≤ 15 мин, < 1.5 ГБ (P4.9) |
| DuckDB 1M | Integration `Append_one_million_filtered_samples…` | запись < 10 с, чтение < 500 мс |

## Зафиксированные результаты (2026-10-11)

| Сценарий | Результат | Статус |
|---|---|---|
| CSV 250k streaming validate | OK, < 30 с, прирост < 80 МБ | pass |
| Declared >1 ГБ | check=1, без чтения потока | pass |
| Анализ 1 мин (72 bpm) | OK, < 20 с | pass |
| Анализ 24 ч | запускать с `SAEKG_PERF=1` на выделенной машине | optional |
| DuckDB 1M append+read | OK | pass |

## Примечания

- Полный прогон 24 ч и файл 1 ГБ не гоняется в каждом CI-job из‑за времени/диска; smoke-критерии закрывают потоковость и лимиты.
- При провале 24 ч: батчи, `ArrayPool`, уменьшение параллелизма QRS, профилирование `EcgAnalyzer`.
