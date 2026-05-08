using System.Globalization;
using System.Text;
using Memora.Core.Artifacts;

namespace Memora.Storage.Persistence;

public sealed class ArtifactMarkdownWriter
{
    public string Write(ArtifactDocument artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var builder = new StringBuilder();
        builder.AppendLine("---");
        AppendScalar(builder, "id", artifact.Id);
        AppendScalar(builder, "project_id", artifact.ProjectId);
        AppendScalar(builder, "type", artifact.Type.ToSchemaValue());
        AppendScalar(builder, "status", artifact.Status.ToSchemaValue());
        AppendScalar(builder, "title", artifact.Title);
        AppendScalar(builder, "created_at", artifact.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendScalar(builder, "updated_at", artifact.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendScalar(builder, "revision", artifact.Revision);
        AppendStringList(builder, "tags", artifact.Tags);
        AppendScalar(builder, "provenance", artifact.Provenance);
        AppendScalar(builder, "reason", artifact.Reason);
        AppendLinks(builder, artifact.Links);
        AppendTypeSpecificFields(builder, artifact);
        builder.AppendLine("---");
        builder.Append(NormalizeNewlines(artifact.Body));
        return builder.ToString();
    }

    private static void AppendTypeSpecificFields(StringBuilder builder, ArtifactDocument artifact)
    {
        switch (artifact)
        {
            case PlanArtifact plan:
                AppendScalar(builder, "priority", plan.Priority.ToSchemaValue());
                AppendScalar(builder, "active", plan.Active);
                break;
            case ArchitectureDecisionArtifact decision:
                AppendScalar(builder, "decision_date", decision.DecisionDate);
                break;
            case ConstraintArtifact constraint:
                AppendScalar(builder, "constraint_kind", constraint.ConstraintKind.ToSchemaValue());
                AppendScalar(builder, "severity", constraint.Severity.ToSchemaValue());
                break;
            case OpenQuestionArtifact question:
                AppendScalar(builder, "question_status", question.QuestionStatus.ToSchemaValue());
                AppendScalar(builder, "priority", question.Priority.ToSchemaValue());
                break;
            case OutcomeArtifact outcome:
                AppendScalar(builder, "outcome", outcome.Outcome.ToSchemaValue());
                break;
            case RepoStructureArtifact repoStructure:
                AppendScalar(builder, "snapshot_source", repoStructure.SnapshotSource.ToSchemaValue());
                break;
            case SessionSummaryArtifact sessionSummary:
                AppendScalar(builder, "session_type", sessionSummary.SessionType.ToSchemaValue());
                AppendScalar(builder, "canonical", sessionSummary.Canonical);
                break;
        }
    }

    private static void AppendLinks(StringBuilder builder, ArtifactLinks links)
    {
        builder.AppendLine("links:");
        AppendStringList(builder, "depends_on", links.GetTargetArtifactIds(ArtifactRelationshipKind.DependsOn), 2);
        AppendStringList(builder, "affects", links.GetTargetArtifactIds(ArtifactRelationshipKind.Affects), 2);
        AppendStringList(builder, "derived_from", links.GetTargetArtifactIds(ArtifactRelationshipKind.DerivedFrom), 2);
        AppendStringList(builder, "supersedes", links.GetTargetArtifactIds(ArtifactRelationshipKind.Supersedes), 2);
    }

    private static void AppendStringList(StringBuilder builder, string key, IReadOnlyList<string> values, int indent = 0)
    {
        var prefix = new string(' ', indent);
        if (values.Count == 0)
        {
            builder.Append(prefix).Append(key).AppendLine(": []");
            return;
        }

        builder.Append(prefix).Append(key).AppendLine(":");
        foreach (var value in values)
        {
            builder.Append(prefix).Append("  - ").AppendLine(FormatString(value));
        }
    }

    private static void AppendScalar(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append(": ").AppendLine(FormatString(value));

    private static void AppendScalar(StringBuilder builder, string key, int value) =>
        builder.Append(key).Append(": ").Append(value).Append('\n');

    private static void AppendScalar(StringBuilder builder, string key, bool value) =>
        builder.Append(key).Append(": ").Append(value ? "true" : "false").Append('\n');

    private static string FormatString(string value)
    {
        if (!RequiresQuoting(value))
        {
            return value;
        }

        return !value.Contains('\'')
            ? $"'{value}'"
            : $"\"{EscapeDoubleQuotedString(value)}\"";
    }

    private static bool RequiresQuoting(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value != value.Trim() ||
               value == "[]" ||
               bool.TryParse(value, out _) ||
               int.TryParse(value, out _) ||
               value.Contains('\'') ||
               value.Contains('"') ||
               IsReservedYamlScalar(value) ||
               StartsWithReservedYamlIndicator(value) ||
               value.Contains("# ", StringComparison.Ordinal) ||
               value.Contains(": ", StringComparison.Ordinal);
    }

    private static string EscapeDoubleQuotedString(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private static bool IsReservedYamlScalar(string value) =>
        string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "~", StringComparison.Ordinal) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithReservedYamlIndicator(string value) =>
        value[0] is '!' or '&' or '*' or '>' or '|' or '?' or '@' or '`';

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
