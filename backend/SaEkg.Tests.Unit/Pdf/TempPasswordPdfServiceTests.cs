using FluentAssertions;
using SaEkg.Infrastructure.Pdf;
using Xunit;

namespace SaEkg.Tests.Unit.Pdf;

public class TempPasswordPdfServiceTests
{
    [Fact]
    public void Renders_pdf_bytes_with_header()
    {
        var pdf = new TempPasswordPdfService();
        var bytes = pdf.Render("doctor@example.com", "TempPass12!a");
        bytes.Length.Should().BeGreaterThan(100);
        System.Text.Encoding.ASCII.GetString(bytes.AsSpan(0, 4)).Should().Be("%PDF");
    }
}
