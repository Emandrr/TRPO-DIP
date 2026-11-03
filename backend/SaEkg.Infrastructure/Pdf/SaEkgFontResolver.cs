using PdfSharp.Fonts;

namespace SaEkg.Infrastructure.Pdf;

public sealed class SaEkgFontResolver : IFontResolver
{
    public const string FaceName = "SaEkg#";

    private static readonly string[] Candidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "Arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/freefont/FreeSans.ttf"
    ];

    private readonly Lazy<byte[]> _fontBytes = new(LoadFont);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(FaceName);

    public byte[]? GetFont(string faceName) =>
        faceName == FaceName ? _fontBytes.Value : null;

    private static byte[] LoadFont()
    {
        foreach (var path in Candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        throw new InvalidOperationException(
            "Не найден системный TTF с кириллицей (Arial/DejaVu/Liberation). Установите шрифт в контейнере.");
    }
}
