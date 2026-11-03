using SaEkg.Domain.Constants;
using SaEkg.Domain.Enums;

namespace SaEkg.Application.Analysis;

public static class MetricEngine
{
    public static (AnalysisSummary Summary, List<DetectedAnomaly> Anomalies) Analyze(
        IReadOnlyList<long> rPeaksMs,
        IReadOnlyList<FiducialPoints> fiducials,
        string sex)
    {
        var anomalies = new List<DetectedAnomaly>();
        var rrList = new List<double>();
        for (var i = 0; i < rPeaksMs.Count - 1; i++)
        {
            rrList.Add(rPeaksMs[i + 1] - rPeaksMs[i]);
        }

        var rAmps = fiducials.Select(f => (double)f.RAmpMv).ToList();
        var qtcList = new List<double>();
        var undetermined = 0;

        foreach (var f in fiducials)
        {
            var rr = FindRr(rPeaksMs, f.RMs);
            if (rr is null)
            {
                undetermined++;
                continue;
            }

            AddCycleAnomaly(anomalies, "RR", rr.Value, ReferenceRanges.RrMinMs, ReferenceRanges.RrMaxMs, "мс", f.RMs, (long)(f.RMs + rr.Value));

            if (rr.Value / 1000.0 >= ReferenceRanges.PauseSec)
            {
                anomalies.Add(Make("PAUSE", AnomalyDirection.High, "Пауза", f.RMs, (long)(f.RMs + rr.Value), rr.Value / 1000.0, "с", 1));
            }

            if (Math.Abs(f.RAmpMv) < ReferenceRanges.RAmpMinMv || Math.Abs(f.RAmpMv) > ReferenceRanges.RAmpMaxMv)
            {
                var dir = Math.Abs(f.RAmpMv) < ReferenceRanges.RAmpMinMv ? AnomalyDirection.Low : AnomalyDirection.High;
                anomalies.Add(Make("R_AMP", dir, "Амплитуда R", f.RMs, f.RMs, f.RAmpMv, "мВ", 1));
            }

            if (f.QOnMs is not null && f.SOffMs is not null)
            {
                var qrs = f.SOffMs.Value - f.QOnMs.Value;
                AddCycleAnomaly(anomalies, "QRS_DUR", qrs, ReferenceRanges.QrsDurMinMs, ReferenceRanges.QrsDurMaxMs, "мс", f.QOnMs.Value, f.SOffMs.Value);
            }
            else
            {
                undetermined++;
            }

            if (f.POnMs is not null && f.QOnMs is not null)
            {
                var pr = f.QOnMs.Value - f.POnMs.Value;
                AddCycleAnomaly(anomalies, "PR", pr, ReferenceRanges.PrMinMs, ReferenceRanges.PrMaxMs, "мс", f.POnMs.Value, f.QOnMs.Value);
            }

            if (f.POnMs is not null && f.POffMs is not null)
            {
                var width = f.POffMs.Value - f.POnMs.Value;
                if (width >= ReferenceRanges.PWidthMaxMs)
                {
                    anomalies.Add(Make("P_WAVE", AnomalyDirection.High, "Зубец P", f.POnMs.Value, f.POffMs.Value, width, "мс", 1));
                }
            }

            if (f.QOnMs is not null && f.TOffMs is not null)
            {
                var qt = f.TOffMs.Value - f.QOnMs.Value;
                AddCycleAnomaly(anomalies, "QT", qt, ReferenceRanges.QtMinMs, ReferenceRanges.QtMaxMs, "мс", f.QOnMs.Value, f.TOffMs.Value);
                var qtc = qt / Math.Sqrt(rr.Value / 1000.0);
                qtcList.Add(qtc);
                var qtcLimit = sex == Sex.Female ? ReferenceRanges.QtcFemaleMs : ReferenceRanges.QtcMaleMs;
                if (qtc >= qtcLimit)
                {
                    anomalies.Add(Make("QTC", AnomalyDirection.High, "QTc", f.QOnMs.Value, f.TOffMs.Value, qtc, "мс", 1));
                }
            }

            if (f.JMs is not null)
            {
                var stMs = f.JMs.Value + 60;
                var stSampleApprox = f.BaselineMv;
                if (Math.Abs(stSampleApprox) > ReferenceRanges.StMaxAbsMv)
                {
                }
            }
        }

        var windowMs = AnalysisConstants.WindowSec * 1000L;
        var stepMs = AnalysisConstants.WindowStepSec * 1000L;
        if (rPeaksMs.Count > 0)
        {
            var start = rPeaksMs[0];
            var end = rPeaksMs[^1];
            for (var w = start; w + windowMs <= end + windowMs; w += stepMs)
            {
                var wEnd = w + windowMs;
                var windowRr = new List<double>();
                for (var i = 0; i < rPeaksMs.Count - 1; i++)
                {
                    if (rPeaksMs[i] >= w && rPeaksMs[i + 1] <= wEnd)
                    {
                        windowRr.Add(rPeaksMs[i + 1] - rPeaksMs[i]);
                    }
                }

                if (windowRr.Count == 0)
                {
                    continue;
                }

                var meanRr = windowRr.Average();
                var hr = 60000.0 / meanRr;
                if (hr < ReferenceRanges.HrMin)
                {
                    anomalies.Add(Make("HR", AnomalyDirection.Low, "Брадикардия", w, wEnd, hr, "уд/мин", windowRr.Count));
                }
                else if (hr > ReferenceRanges.HrMax)
                {
                    anomalies.Add(Make("HR", AnomalyDirection.High, "Тахикардия", w, wEnd, hr, "уд/мин", windowRr.Count));
                }

                foreach (var rr in windowRr)
                {
                    var dev = Math.Abs((rr - meanRr) / meanRr * 100.0);
                    if (dev > ReferenceRanges.RrDevPct)
                    {
                        anomalies.Add(Make("RR_DEV", AnomalyDirection.High, "Отклонение RR", w, wEnd, dev, "%", 1));
                    }
                }
            }
        }

        var merged = AnomalyMerger.Merge(anomalies);
        var summary = new AnalysisSummary(
            rrList.Count > 0 ? 60000.0 / rrList.Average() : null,
            rrList.Count > 0 ? 60000.0 / rrList.Max() : null,
            rrList.Count > 0 ? 60000.0 / rrList.Min() : null,
            rrList.Count > 0 ? rrList.Average() : null,
            qtcList.Count > 0 ? qtcList.Average() : null,
            rAmps.Count > 0 ? rAmps.Average() : null,
            fiducials.Count,
            undetermined);

        return (summary, merged);
    }

    public static double QtcBazett(double qtMs, double rrMs) => qtMs / Math.Sqrt(rrMs / 1000.0);

    private static double? FindRr(IReadOnlyList<long> rPeaksMs, long rMs)
    {
        for (var i = 0; i < rPeaksMs.Count - 1; i++)
        {
            if (rPeaksMs[i] == rMs)
            {
                return rPeaksMs[i + 1] - rPeaksMs[i];
            }
        }

        return null;
    }

    private static void AddCycleAnomaly(
        List<DetectedAnomaly> list,
        string code,
        double value,
        double min,
        double max,
        string unit,
        long start,
        long end)
    {
        if (value < min)
        {
            list.Add(Make(code, AnomalyDirection.Low, Title(code), start, end, value, unit, 1));
        }
        else if (value > max)
        {
            list.Add(Make(code, AnomalyDirection.High, Title(code), start, end, value, unit, 1));
        }
    }

    private static DetectedAnomaly Make(
        string code,
        string direction,
        string title,
        long start,
        long end,
        double value,
        string unit,
        int cycles) =>
        new(code, direction, title, start, end, value, value, unit, ReferenceRanges.Text(code), cycles);

    private static string Title(string code) => code switch
    {
        "HR" => "ЧСС",
        "RR" => "Интервал RR",
        "QRS_DUR" => "Длительность QRS",
        "PR" => "Интервал PR",
        "QT" => "Интервал QT",
        _ => code
    };
}
