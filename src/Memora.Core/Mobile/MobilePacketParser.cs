using System.Globalization;

namespace Memora.Core.Mobile;

public static class MobilePacketParser
{
    private const int SupportedPacketVersion = 1;
    private const string ExpectedSource = "mobile";

    private static readonly IReadOnlySet<string> ReservedCanonicalFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "status",
        "revision",
        "project_id",
        "id",
        "approved_at",
        "approved_by"
    };

    private static readonly IReadOnlySet<string> KnownArtifactTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "charter",
        "plan",
        "decision",
        "constraint",
        "question",
        "outcome",
        "repo_structure",
        "session_summary"
    };

    public static MobilePacketParseResult Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var diagnostics = new List<MobilePacketParseDiagnostic>();
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.frontmatter.missing",
                "Mobile packet markdown must begin with a '---' frontmatter delimiter.",
                "frontmatter"));
            return new MobilePacketParseResult(null, diagnostics);
        }

        var closingIndex = normalized.IndexOf("\n---\n", StringComparison.Ordinal);
        var trimmedClosing = -1;
        if (closingIndex < 0)
        {
            if (normalized.EndsWith("\n---", StringComparison.Ordinal))
            {
                trimmedClosing = normalized.Length - 4;
            }
            else
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.missing_end",
                    "Mobile packet frontmatter block is missing a closing '---' delimiter.",
                    "frontmatter"));
                return new MobilePacketParseResult(null, diagnostics);
            }
        }

        var frontmatterEnd = closingIndex >= 0 ? closingIndex : trimmedClosing;
        var bodyStart = closingIndex >= 0 ? closingIndex + 5 : normalized.Length;

        var frontmatterContent = normalized.Substring(4, frontmatterEnd - 4);
        var body = bodyStart >= normalized.Length ? string.Empty : normalized[bodyStart..];

        var entries = ParseFrontmatter(frontmatterContent, diagnostics);
        if (diagnostics.Count > 0)
        {
            return new MobilePacketParseResult(null, diagnostics);
        }

        return BuildPacket(entries, body, diagnostics);
    }

    private static Dictionary<string, FrontmatterValue> ParseFrontmatter(
        string frontmatter,
        List<MobilePacketParseDiagnostic> diagnostics)
    {
        var lines = frontmatter.Split('\n');
        var result = new Dictionary<string, FrontmatterValue>(StringComparer.Ordinal);
        var lineNumber = 0;

        while (lineNumber < lines.Length)
        {
            var line = lines[lineNumber];
            if (string.IsNullOrWhiteSpace(line))
            {
                lineNumber++;
                continue;
            }

            if (line.StartsWith(" ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal))
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.parse",
                    $"Mobile packet frontmatter must not begin a line with whitespace on line {lineNumber + 1}.",
                    $"frontmatter.line.{lineNumber + 1}"));
                return result;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.parse",
                    $"Expected a 'key: value' entry on line {lineNumber + 1}.",
                    $"frontmatter.line.{lineNumber + 1}"));
                return result;
            }

            var key = line[..separator].Trim();
            var rawValue = line[(separator + 1)..].Trim();
            lineNumber++;

            if (string.IsNullOrEmpty(key))
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.parse",
                    $"Frontmatter key is empty on line {lineNumber}.",
                    $"frontmatter.line.{lineNumber}"));
                return result;
            }

            if (result.ContainsKey(key))
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.duplicate_key",
                    $"Duplicate frontmatter key '{key}' on line {lineNumber}.",
                    $"frontmatter.{key}"));
                return result;
            }

            if (rawValue.Length > 0)
            {
                result[key] = new FrontmatterValue(ScalarText: UnquoteScalar(rawValue), Sequence: null);
                continue;
            }

            var sequence = new List<string>();
            while (lineNumber < lines.Length)
            {
                var next = lines[lineNumber];
                if (string.IsNullOrWhiteSpace(next))
                {
                    lineNumber++;
                    continue;
                }

                if (!next.StartsWith("  - ", StringComparison.Ordinal) && !next.StartsWith(" - ", StringComparison.Ordinal))
                {
                    break;
                }

                var dashIndex = next.IndexOf("- ", StringComparison.Ordinal);
                var item = next[(dashIndex + 2)..].Trim();
                sequence.Add(UnquoteScalar(item));
                lineNumber++;
            }

            if (sequence.Count == 0)
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.frontmatter.parse",
                    $"Expected at least one sequence item for '{key}'.",
                    $"frontmatter.{key}"));
                return result;
            }

            result[key] = new FrontmatterValue(ScalarText: null, Sequence: sequence);
        }

        return result;
    }

    private static MobilePacketParseResult BuildPacket(
        Dictionary<string, FrontmatterValue> entries,
        string rawBody,
        List<MobilePacketParseDiagnostic> diagnostics)
    {
        foreach (var key in entries.Keys)
        {
            if (ReservedCanonicalFields.Contains(key))
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.envelope.reserved_field",
                    $"Mobile packets must not include canonical artifact field '{key}'.",
                    $"frontmatter.{key}"));
            }
        }

        if (!TryRequireScalar(entries, "packet_version", diagnostics, out var versionText))
        {
            return Fail(diagnostics);
        }

        if (!int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var packetVersion))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                "packet_version must be an integer.",
                "frontmatter.packet_version"));
            return Fail(diagnostics);
        }

        if (packetVersion != SupportedPacketVersion)
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.unsupported_version",
                $"packet_version {packetVersion} is not supported. Expected {SupportedPacketVersion}.",
                "frontmatter.packet_version"));
            return Fail(diagnostics);
        }

        if (!TryRequireScalar(entries, "packet_id", diagnostics, out var packetId)) return Fail(diagnostics);
        if (!TryRequireScalar(entries, "created_at", diagnostics, out var createdAtRaw)) return Fail(diagnostics);
        if (!TryRequireScalar(entries, "source", diagnostics, out var source)) return Fail(diagnostics);
        if (!TryRequireScalar(entries, "intent", diagnostics, out var intentRaw)) return Fail(diagnostics);
        if (!TryRequireScalar(entries, "lifecycle_target", diagnostics, out var lifecycleRaw)) return Fail(diagnostics);
        if (!TryRequireScalar(entries, "canonical", diagnostics, out var canonicalRaw)) return Fail(diagnostics);

        if (!string.Equals(source, ExpectedSource, StringComparison.Ordinal))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.source_must_be_mobile",
                $"source must equal 'mobile' but was '{source}'.",
                "frontmatter.source"));
        }

        if (!string.Equals(canonicalRaw, "false", StringComparison.Ordinal))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.canonical_must_be_false",
                "canonical must be the literal 'false'.",
                "frontmatter.canonical"));
        }

        if (!MobilePacketIntentExtensions.TryParseSchemaValue(intentRaw, out var intent))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                $"intent '{intentRaw}' is not one of question, decision_draft, planning_note, proposal_draft.",
                "frontmatter.intent"));
        }

        if (!MobilePacketLifecycleTargetExtensions.TryParseSchemaValue(lifecycleRaw, out var lifecycleTarget))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                $"lifecycle_target '{lifecycleRaw}' is not one of planning_input, proposal_draft.",
                "frontmatter.lifecycle_target"));
        }

        if (!DateTimeOffset.TryParse(
                createdAtRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var createdAt))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                "created_at must be an ISO-8601 UTC timestamp.",
                "frontmatter.created_at"));
        }

        var title = ReadOptionalScalar(entries, "title", diagnostics);
        var deviceLabel = ReadOptionalScalar(entries, "device_label", diagnostics);
        var targetHint = ReadOptionalScalar(entries, "target_project_hint", diagnostics);
        var proposedType = ReadOptionalScalar(entries, "proposed_artifact_type", diagnostics);

        IReadOnlyList<string> tags = [];
        if (entries.TryGetValue("tags", out var tagsValue))
        {
            if (tagsValue.Sequence is null)
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.envelope.invalid_value",
                    "tags must be a YAML sequence.",
                    "frontmatter.tags"));
            }
            else
            {
                tags = tagsValue.Sequence;
            }
        }

        if (diagnostics.Count == 0 && intent.ExpectedLifecycleTarget() != lifecycleTarget)
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.intent_lifecycle_mismatch",
                $"lifecycle_target '{lifecycleTarget.ToSchemaValue()}' does not match intent '{intent.ToSchemaValue()}'. "
                + $"Expected '{intent.ExpectedLifecycleTarget().ToSchemaValue()}'.",
                "frontmatter.lifecycle_target"));
        }

        if (!string.IsNullOrEmpty(proposedType))
        {
            if (!KnownArtifactTypes.Contains(proposedType))
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.envelope.invalid_value",
                    $"proposed_artifact_type '{proposedType}' is not a known artifact type.",
                    "frontmatter.proposed_artifact_type"));
            }
            else if (intent != MobilePacketIntent.DecisionDraft && intent != MobilePacketIntent.ProposalDraft)
            {
                diagnostics.Add(new MobilePacketParseDiagnostic(
                    "mobile_packet.envelope.proposed_type_not_allowed",
                    "proposed_artifact_type is only allowed when intent is decision_draft or proposal_draft.",
                    "frontmatter.proposed_artifact_type"));
            }
        }

        var trimmedBody = rawBody.Trim('\n', '\r');
        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.body.empty",
                "Mobile packet body must not be empty.",
                "body"));
        }

        if (diagnostics.Count > 0)
        {
            return Fail(diagnostics);
        }

        var packet = new MobilePacket(
            packetVersion,
            packetId,
            createdAt,
            intent,
            lifecycleTarget,
            trimmedBody,
            title: title,
            deviceLabel: deviceLabel,
            targetProjectHint: targetHint,
            tags: tags,
            proposedArtifactType: proposedType);

        return new MobilePacketParseResult(packet, []);
    }

    private static MobilePacketParseResult Fail(List<MobilePacketParseDiagnostic> diagnostics) =>
        new(null, diagnostics);

    private static bool TryRequireScalar(
        Dictionary<string, FrontmatterValue> entries,
        string key,
        List<MobilePacketParseDiagnostic> diagnostics,
        out string value)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.missing_required",
                $"Required field '{key}' is missing.",
                $"frontmatter.{key}"));
            value = string.Empty;
            return false;
        }

        if (entry.ScalarText is null)
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                $"Field '{key}' must be a scalar value.",
                $"frontmatter.{key}"));
            value = string.Empty;
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.ScalarText))
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.missing_required",
                $"Required field '{key}' must not be empty.",
                $"frontmatter.{key}"));
            value = string.Empty;
            return false;
        }

        value = entry.ScalarText;
        return true;
    }

    private static string? ReadOptionalScalar(
        Dictionary<string, FrontmatterValue> entries,
        string key,
        List<MobilePacketParseDiagnostic> diagnostics)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ScalarText is null)
        {
            diagnostics.Add(new MobilePacketParseDiagnostic(
                "mobile_packet.envelope.invalid_value",
                $"Field '{key}' must be a scalar value.",
                $"frontmatter.{key}"));
            return null;
        }

        return string.IsNullOrWhiteSpace(entry.ScalarText) ? null : entry.ScalarText;
    }

    private static string UnquoteScalar(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            var inner = raw[1..^1];
            return inner.Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
        {
            return raw[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return raw;
    }

    private sealed record FrontmatterValue(string? ScalarText, IReadOnlyList<string>? Sequence);
}
