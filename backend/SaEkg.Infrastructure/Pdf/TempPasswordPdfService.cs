using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SaEkg.Application.Auth;

namespace SaEkg.Infrastructure.Pdf;

public sealed class TempPasswordPdfService : ITempPasswordPdfService
{
    private static int _resolverSet;

    public TempPasswordPdfService()
    {
        if (Interlocked.Exchange(ref _resolverSet, 1) == 0)
        {
            GlobalFontSettings.FontResolver = new SaEkgFontResolver();
        }
    }

    public byte[] Render(string email, string temporaryPassword)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Временный пароль СА-ЭКГ";
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);
        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("SaEkg", 18, XFontStyleEx.Bold);
        var bodyFont = new XFont("SaEkg", 12, XFontStyleEx.Regular);
        var monoFont = new XFont("SaEkg", 14, XFontStyleEx.Bold);

        var y = 40.0;
        var contentWidth = page.Width.Point - 80;
        gfx.DrawString("СА-ЭКГ — временный пароль", titleFont, XBrushes.Black, new XRect(40, y, contentWidth, 30), XStringFormats.TopLeft);
        y += 40;
        gfx.DrawString("Учётная запись:", bodyFont, XBrushes.Black, new XRect(40, y, contentWidth, 20), XStringFormats.TopLeft);
        y += 22;
        gfx.DrawString(email, monoFont, XBrushes.Black, new XRect(40, y, contentWidth, 24), XStringFormats.TopLeft);
        y += 36;
        gfx.DrawString("Временный пароль (сохраните и смените после входа):", bodyFont, XBrushes.Black, new XRect(40, y, contentWidth, 20), XStringFormats.TopLeft);
        y += 22;
        gfx.DrawString(temporaryPassword, monoFont, XBrushes.Black, new XRect(40, y, contentWidth, 24), XStringFormats.TopLeft);
        y += 40;
        gfx.DrawString("Документ одноразовый. Повторная выдача PDF недоступна.", bodyFont, XBrushes.Black, new XRect(40, y, contentWidth, 20), XStringFormats.TopLeft);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}
