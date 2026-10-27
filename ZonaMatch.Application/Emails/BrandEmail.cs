using System.Text;

namespace ZonaMatch.Application.Emails;

// A button of the email: the text is plain, the URL is encoded here.
public sealed record BrandEmailButton(string Text, string Url);

// Content of an email. Paragraphs are HTML: callers encode anything the user typed.
public sealed record BrandEmailContent(
    string Preheader,
    string Title,
    IReadOnlyList<string> Paragraphs,
    BrandEmailButton? Button = null,
    string? Footnote = null);

// Every outgoing email shares this layout, following the brand manual: light background, a white card with the
// "gota" shape, the logo on top, Arial (Nunito Sans is not available in mail clients), the blue for actions and
// the dark neutral for text. Tables and inline styles, so it looks the same in Gmail, Outlook and phones.
public static class BrandEmail
{
    // The email service attaches the logo with this Content-ID
    public const string LogoContentId = "zonamatch-logo";

    private const string Blue = "#0058C8";
    private const string Ink = "#131C2D";
    private const string Text = "#364257";
    private const string Muted = "#67758C";
    private const string Background = "#F6F8FB";
    private const string Border = "#DCE3EC";
    private const string Font = "Arial, Helvetica, sans-serif";
    private const string Gota = "20px 20px 20px 6px";
    private const string GotaSmall = "14px 14px 14px 4px";

    // Escapes only what HTML needs, so accents and "¡" stay readable in the source of the email
    public static string Encode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    public static string Render(BrandEmailContent content)
    {
        var paragraphs = new StringBuilder();
        foreach (var paragraph in content.Paragraphs)
            paragraphs.Append($"""<p style="margin:0 0 16px;font:16px/1.5 {Font};color:{Text}">{paragraph}</p>""");

        var button = content.Button is { } b
            ? $"""
              <table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 24px"><tr>
                <td style="background:{Blue};border-radius:{GotaSmall}">
                  <a href="{Encode(b.Url)}" style="display:inline-block;padding:14px 24px;font:bold 16px/1 {Font};color:#FFFFFF;text-decoration:none;border-radius:{GotaSmall}">{Encode(b.Text)}</a>
                </td>
              </tr></table>
              """
            : string.Empty;

        var footnote = content.Footnote is { } note
            ? $"""<p style="margin:16px 0 0;padding-top:16px;border-top:1px solid {Border};font:13px/1.5 {Font};color:{Muted}">{note}</p>"""
            : string.Empty;

        return $"""
            <!doctype html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{Encode(content.Title)}</title></head>
            <body style="margin:0;padding:0;background:{Background}">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0">{Encode(content.Preheader)}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Background}">
                <tr><td align="center" style="padding:32px 16px">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px">
                    <tr><td style="padding:0 8px 20px">
                      <img src="cid:{LogoContentId}" width="160" height="30" alt="ZonaMatch" style="display:block;border:0;width:160px;height:30px">
                    </td></tr>
                    <tr><td style="background:#FFFFFF;border:1px solid {Border};border-radius:{Gota};padding:32px 28px">
                      <h1 style="margin:0 0 16px;font:bold 24px/1.15 {Font};color:{Ink}">{Encode(content.Title)}</h1>
                      {paragraphs}
                      {button}
                      {footnote}
                    </td></tr>
                    <tr><td style="padding:20px 8px 0;font:13px/1.5 {Font};color:{Muted}">
                      <strong style="color:{Blue}">ZonaMatch</strong> · Encontrá dónde vivir, con datos reales.
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
