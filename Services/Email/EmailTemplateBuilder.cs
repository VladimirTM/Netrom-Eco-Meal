using System.Net;
using System.Text;

namespace Netrom_Eco_Meal.Services.Email;

// Wraps every outgoing email (order lifecycle, back-in-stock, account confirmation, password
// reset) in one branded HTML shell, so a customer's inbox reads as the same product as the app
// instead of four inconsistent one-off snippets. Table-based layout with inline styles
// throughout — the only approach that survives Outlook/older webmail clients, which ignore
// <style> blocks and most modern CSS; nothing here depends on app.css or the Google Fonts link
// in App.razor actually loading. Heading/paragraph/CTA text is HTML-encoded here, once, rather
// than leaving every call site responsible for remembering to escape user-controlled text (a
// customer's display name, a package name...).
public static class EmailTemplateBuilder
{
    private const string Forest = "#0e2117";
    private const string Ink = "#1b2a1f";
    private const string Muted = "#6f6656";
    private const string Paper = "#f5efe2";
    private const string Leaf = "#1f8a4c";
    private const string RescueTint = "#fbe4d3";
    private const string RescueText = "#a4531f";

    // Georgia/Segoe are the closest widely-installed stand-ins for the app's Fraunces/Hanken
    // Grotesk pairing — most mail clients strip web-font @font-face rules entirely, so the
    // design has to hold up on system fonts alone rather than assume Fraunces ever loads.
    private const string DisplayFont = "Georgia, 'Iowan Old Style', 'Palatino Linotype', serif";
    private const string BodyFont = "'Segoe UI', Helvetica, Arial, sans-serif";

    public static string Build(string heading, IEnumerable<string> paragraphs, string? eyebrow = null, string? ctaLabel = null, string? ctaUrl = null)
    {
        var sb = new StringBuilder();

        sb.Append($"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
            <title>Eco Meal</title>
            </head>
            <body style="margin:0;padding:0;background-color:{Paper};">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{Paper};">
            <tr><td align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:520px;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 8px 24px rgba(14,33,23,0.08);">
            <tr><td style="background-color:{Forest};padding:28px 32px;text-align:center;">
            <div style="font-size:26px;line-height:1;margin-bottom:6px;">&#127860;</div>
            <div style="font-family:{DisplayFont};font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-0.01em;">Eco Meal</div>
            <div style="font-family:{BodyFont};font-size:11px;color:rgba(255,255,255,0.55);margin-top:4px;letter-spacing:0.06em;">SURPLUS FOOD, RESCUED</div>
            </td></tr>
            <tr><td style="padding:36px 32px 8px;">
            """);

        if (!string.IsNullOrWhiteSpace(eyebrow))
        {
            sb.Append($"""
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 14px;">
                <tr><td style="background-color:{RescueTint};border-radius:999px;padding:5px 12px;">
                <span style="font-family:{BodyFont};font-size:11px;font-weight:700;letter-spacing:0.05em;color:{RescueText};">{Encode(eyebrow)}</span>
                </td></tr>
                </table>
                """);
        }

        sb.Append($"""<h1 style="margin:0 0 16px;font-family:{DisplayFont};font-size:25px;line-height:1.3;font-weight:700;color:{Ink};">{Encode(heading)}</h1>""");

        foreach (var paragraph in paragraphs)
        {
            sb.Append($"""<p style="margin:0 0 14px;font-family:{BodyFont};font-size:15px;line-height:1.6;color:{Ink};">{Encode(paragraph)}</p>""");
        }

        sb.Append("</td></tr>");

        if (!string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl))
        {
            sb.Append($"""
                <tr><td style="padding:8px 32px 12px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto;">
                <tr><td style="background-color:{Leaf};border-radius:10px;">
                <a href="{ctaUrl}" style="display:inline-block;padding:12px 30px;font-family:{BodyFont};font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">{Encode(ctaLabel)}</a>
                </td></tr>
                </table>
                </td></tr>
                """);
        }

        sb.Append($"""
            <tr><td style="padding:28px 32px 32px;">
            <div style="border-top:1px dashed #e4dcc9;padding-top:20px;font-family:{BodyFont};font-size:12px;line-height:1.6;color:{Muted};">
            You're getting this because of activity on your Eco Meal account.
            </div>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
