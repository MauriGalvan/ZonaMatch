using ZonaMatch.Application.Emails;

namespace ZonaMatch.Tests.Emails;

public class BrandEmailTests
{
    [Fact]
    public void Renders_the_logo_title_paragraphs_button_and_footnote()
    {
        var html = BrandEmail.Render(new BrandEmailContent(
            "Resumen", "¡Hola!", ["Primer párrafo", "Segundo"], new BrandEmailButton("Entrar", "https://zonamatch.ar/a?b=1&c=2"), "Nota"));

        Assert.Contains($"cid:{BrandEmail.LogoContentId}", html);
        Assert.Contains("¡Hola!", html);
        Assert.Contains("Primer párrafo", html);
        Assert.Contains("https://zonamatch.ar/a?b=1&amp;c=2", html);
        Assert.Contains("Entrar", html);
        Assert.Contains("Nota", html);
    }

    [Fact]
    public void Encodes_the_title_and_the_preheader()
    {
        var html = BrandEmail.Render(new BrandEmailContent("<b>x</b>", "<script>alert(1)</script>", []));

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
    }

    [Fact]
    public void Without_button_or_footnote_they_are_left_out()
    {
        var html = BrandEmail.Render(new BrandEmailContent("Resumen", "Título", ["Texto"]));

        Assert.DoesNotContain("<a href", html);
        Assert.DoesNotContain("border-top", html);
    }
}
