namespace SaEkg.Application.Analysis;

public static class ReferenceRanges
{
    public const double HrMin = 60;
    public const double HrMax = 100;
    public const double RrMinMs = 600;
    public const double RrMaxMs = 1000;
    public const double RrDevPct = 10;
    public const double PauseSec = 2.0;
    public const double RAmpMinMv = 0.5;
    public const double RAmpMaxMv = 2.5;
    public const double QrsShapePct = 20;
    public const double QrsDurMinMs = 60;
    public const double QrsDurMaxMs = 100;
    public const double PrMinMs = 120;
    public const double PrMaxMs = 200;
    public const double PWidthMaxMs = 110;
    public const double PAmpMaxMv = 0.25;
    public const double QtMinMs = 350;
    public const double QtMaxMs = 450;
    public const double QtcMaleMs = 450;
    public const double QtcFemaleMs = 460;
    public const double StMaxAbsMv = 0.1;
    public const double TAmpMaxMv = 1.0;

    public static string Text(string code) => code switch
    {
        "HR" => "60–100",
        "RR" => "600–1000",
        "RR_DEV" => "≤10%",
        "PAUSE" => "<2.0 с",
        "R_AMP" => "0.5–2.5 мВ",
        "QRS_SHAPE" => "≤20%",
        "QRS_DUR" => "60–100 мс",
        "PR" => "120–200 мс",
        "P_WAVE" => "ширина <110 мс, амп. <0.25 мВ",
        "QT" => "350–450 мс",
        "QTC" => "муж. <450 / жен. <460",
        "ST" => "|ST| ≤ 0.1 мВ",
        "T_WAVE" => "положит., ≤1.0 мВ",
        _ => string.Empty
    };
}
