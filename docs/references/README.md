# UI references (view + report)

## Viewer

Source: `ecg-24h-viewer.html`

Interaction model for `EcgChart` (P7.3):

- 24h overview trend
- Interval selection (drag / hour click)
- 10-second detail window + scrubber
- Interval label + mean HR
- Notes/anomaly markers on overview

## Report

Sources: `ecg-report-example.png`, `ecg-report.pdf`, `ecg-report-bw.pdf`

Layout for analysis report UI/PDF (D19, data shape §6.7):

- Title: detected conditions for the recording period
- Per finding: numbered title, interval, mean HR, mini 24h trend with highlight, ~10s ECG strip
- Data contract unchanged; presentation only follows this layout
