using FluentAssertions;
using ZonaMatch.Application.Emails;

namespace ZonaMatch.Tests.Emails;

public class BrandEmailTests
{
    [Fact]
    public void Render_WhenAllContentIsProvided_IncludesLogoTitleParagraphsButtonAndFootnote()
    {
        var html = BrandEmail.Render(new BrandEmailContent(
            "Resumen", "¡Hola!", ["Primer párrafo", "Segundo"], new BrandEmailButton("Entrar", "https://zonamatch.ar/a?b=1&c=2"), "Nota"));

        html.Should().Contain($"cid:{BrandEmail.LogoContentId}");
        html.Should().Contain("¡Hola!");
        html.Should().Contain("Primer párrafo");
        html.Should().Contain("https://zonamatch.ar/a?b=1&amp;c=2");
        html.Should().Contain("Entrar");
        html.Should().Contain("Nota");
    }

    [Fact]
    public void Render_WhenTitleAndPreheaderContainMarkup_EncodesTheirContent()
    {
        var html = BrandEmail.Render(new BrandEmailContent("<b>x</b>", "<script>alert(1)</script>", []));

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().Contain("&lt;b&gt;x&lt;/b&gt;");
    }

    [Fact]
    public void Render_WhenButtonAndFootnoteAreMissing_OmitsTheirMarkup()
    {
        var html = BrandEmail.Render(new BrandEmailContent("Resumen", "Título", ["Texto"]));

        html.Should().NotContain("<a href");
        html.Should().NotContain("border-top");
    }
}
