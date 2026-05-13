using System.Globalization;
using System.Text;
using Memora.Mobile.Models;

namespace Memora.Mobile.Services;

public sealed record MobilePacketDraft(
    MobileCaptureIntent Intent,
    string Title,
    string Body,
    string TagsCsv,
    string TargetProjectHint,
    string DeviceLabel,
    string ProposedArtifactType);

public sealed record MobilePacketComposition(
    string Markdown,
    IReadOnlyList<string> Errors);

public static class MobilePacketComposer
{
    public const int PacketVersion = 1;

    public static MobilePacketComposition Compose(string packetId, DateTimeOffset createdAtUtc, MobilePacketDraft draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packetId);
        ArgumentNullException.ThrowIfNull(draft);

        var info = MobileCaptureIntentCatalog.GetByIntent(draft.Intent);
        var errors = new List<string>();

        var trimmedTitle = draft.Title?.Trim() ?? string.Empty;
        var trimmedBody = (draft.Body ?? string.Empty).TrimEnd();
        var trimmedTargetHint = draft.TargetProjectHint?.Trim() ?? string.Empty;
        var trimmedDeviceLabel = draft.DeviceLabel?.Trim() ?? string.Empty;
        var trimmedProposedType = draft.ProposedArtifactType?.Trim() ?? string.Empty;
        var tags = ParseTags(draft.TagsCsv);

        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            errors.Add("body is required");
        }

        if (!string.IsNullOrEmpty(trimmedProposedType)
            && !MobileCaptureIntentCatalog.IsProposalKind(draft.Intent))
        {
            errors.Add("proposed_artifact_type is only allowed for decision_draft and proposal_draft");
        }

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"packet_version: {PacketVersion.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"packet_id: {EscapeYamlScalar(packetId)}");
        builder.AppendLine($"created_at: {EscapeYamlScalar(createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
        builder.AppendLine("source: mobile");
        builder.AppendLine($"intent: {info.SchemaValue}");
        builder.AppendLine($"lifecycle_target: {info.LifecycleTargetSchemaValue}");
        builder.AppendLine("canonical: false");

        if (!string.IsNullOrEmpty(trimmedTitle))
        {
            builder.AppendLine($"title: {EscapeYamlScalar(trimmedTitle)}");
        }

        if (!string.IsNullOrEmpty(trimmedDeviceLabel))
        {
            builder.AppendLine($"device_label: {EscapeYamlScalar(trimmedDeviceLabel)}");
        }

        if (!string.IsNullOrEmpty(trimmedTargetHint))
        {
            builder.AppendLine($"target_project_hint: {EscapeYamlScalar(trimmedTargetHint)}");
        }

        if (tags.Count > 0)
        {
            builder.AppendLine("tags:");
            foreach (var tag in tags)
            {
                builder.AppendLine($"  - {EscapeYamlScalar(tag)}");
            }
        }

        if (!string.IsNullOrEmpty(trimmedProposedType)
            && MobileCaptureIntentCatalog.IsProposalKind(draft.Intent))
        {
            builder.AppendLine($"proposed_artifact_type: {trimmedProposedType}");
        }

        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(trimmedBody);

        return new MobilePacketComposition(builder.ToString(), errors);
    }

    public static string GenerateFileName(string packetId, DateTimeOffset createdAtUtc, MobileCaptureIntent intent, string title, string body)
    {
        var compact = createdAtUtc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var info = MobileCaptureIntentCatalog.GetByIntent(intent);
        var slug = Slugify(title);
        if (string.IsNullOrEmpty(slug))
        {
            slug = Slugify(ExtractFirstHeading(body));
        }

        if (string.IsNullOrEmpty(slug))
        {
            slug = packetId.Length >= 8 ? packetId[..8] : packetId;
        }

        return $"{compact}-{info.SchemaValue}-{slug}.md";
    }

    private static IReadOnlyList<string> ParseTags(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var part in csv.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static string EscapeYamlScalar(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "''";
        }

        var lowered = value.ToLowerInvariant();
        if (lowered is "true" or "false" or "null" or "yes" or "no" or "on" or "off" or "~")
        {
            return $"'{value}'";
        }

        if (NeedsDoubleQuotes(value))
        {
            return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }

    private static bool NeedsDoubleQuotes(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return true;
        }

        foreach (var ch in value)
        {
            if (ch is ':' or '#' or '\n' or '\r' or '\t' or '"' or '\'' or '\\' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`' or '{' or '}' or '[' or ']' or ',')
            {
                return true;
            }
        }

        if (value[0] is '-' or '?')
        {
            return true;
        }

        return false;
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastDash = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastDash = false;
                continue;
            }

            if (!lastDash && builder.Length > 0)
            {
                builder.Append('-');
                lastDash = true;
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        return slug.Length > 40 ? slug[..40] : slug;
    }

    private static string ExtractFirstHeading(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("##", StringComparison.Ordinal))
            {
                var text = trimmed.TrimStart('#').Trim();
                if (text.Length > 0)
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }
}
