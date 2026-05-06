using System.Net;
using System.Text;
using Memora.Core.Artifacts;

namespace Memora.Ui.Rendering;

internal static class ReviewUiComponents
{
    public static string RenderPanel(string title, string supportingText, string body, string? cssClass = null)
    {
        var html = new StringBuilder();
        var className = string.IsNullOrWhiteSpace(cssClass) ? "panel" : $"panel {Encode(cssClass)}";
        html.AppendLine($"<section class=\"{className}\">");
        html.AppendLine($"<div class=\"panel-header\"><h2>{Encode(title)}</h2><p class=\"muted\">{Encode(supportingText)}</p></div>");
        html.AppendLine(body);
        html.AppendLine("</section>");
        return html.ToString();
    }

    public static string RenderArticlePanel(string title, string supportingText, string body, string? cssClass = null)
    {
        var html = new StringBuilder();
        var className = string.IsNullOrWhiteSpace(cssClass) ? "panel" : $"panel {Encode(cssClass)}";
        html.AppendLine($"<article class=\"{className}\">");
        html.AppendLine($"<div class=\"panel-header\"><h2>{Encode(title)}</h2><p class=\"muted\">{Encode(supportingText)}</p></div>");
        html.AppendLine(body);
        html.AppendLine("</article>");
        return html.ToString();
    }

    public static string RenderMetadataGrid(IEnumerable<ReviewMetadataItem> items)
    {
        var html = new StringBuilder();
        html.AppendLine("<dl class=\"meta-grid\">");
        foreach (var item in items)
        {
            var value = item.IsHtml
                ? item.Value
                : item.TreatAsCode
                    ? $"<code>{Encode(item.Value)}</code>"
                    : Encode(item.Value);

            html.AppendLine($"<div><dt>{Encode(item.Label)}</dt><dd>{value}</dd></div>");
        }

        html.AppendLine("</dl>");
        return html.ToString();
    }

    public static string RenderStatusBadge(ArtifactStatus status) =>
        $"<span class=\"badge badge-{status.ToSchemaValue()}\">{Encode(status.ToSchemaValue())}</span>";

    public static string RenderActionGroup(IEnumerable<string> actions)
    {
        var html = new StringBuilder();
        html.AppendLine("<div class=\"decision-actions\">");
        foreach (var action in actions)
        {
            html.AppendLine(action);
        }

        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}

internal sealed record ReviewMetadataItem(string Label, string Value, bool TreatAsCode = false, bool IsHtml = false);
