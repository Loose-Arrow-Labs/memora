using Memora.Core.Artifacts;

namespace Memora.Core.Tests.Artifacts;

internal static class ArtifactTestBuilder
{
    public static Dictionary<string, object?> CreateFrontmatter(ArtifactType artifactType)
    {
        var frontmatter = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = $"{ArtifactIdPrefix(artifactType)}-001",
            ["project_id"] = "memora",
            ["type"] = artifactType.ToSchemaValue(),
            ["status"] = ArtifactStatus.Draft.ToSchemaValue(),
            ["title"] = $"{artifactType.ToSchemaValue()} title",
            ["created_at"] = "2026-04-14T12:00:00Z",
            ["updated_at"] = "2026-04-14T12:30:00Z",
            ["revision"] = 1,
            ["tags"] = new List<object?> { "foundation", "schema" },
            ["provenance"] = "user",
            ["reason"] = "milestone implementation",
            ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["depends_on"] = new List<object?>(),
                ["affects"] = new List<object?> { "ADR-001" },
                ["derived_from"] = new List<object?>(),
                ["supersedes"] = new List<object?>()
            }
        };

        switch (artifactType)
        {
            case ArtifactType.Plan:
                frontmatter["priority"] = ArtifactPriority.Normal.ToSchemaValue();
                frontmatter["active"] = true;
                break;
            case ArtifactType.Decision:
                frontmatter["decision_date"] = "2026-04-14";
                break;
            case ArtifactType.Constraint:
                frontmatter["constraint_kind"] = ConstraintKind.Technical.ToSchemaValue();
                frontmatter["severity"] = ConstraintSeverity.High.ToSchemaValue();
                break;
            case ArtifactType.Question:
                frontmatter["question_status"] = QuestionStatus.Open.ToSchemaValue();
                frontmatter["priority"] = ArtifactPriority.High.ToSchemaValue();
                break;
            case ArtifactType.Outcome:
                frontmatter["outcome"] = OutcomeKind.Mixed.ToSchemaValue();
                break;
            case ArtifactType.RepoStructure:
                frontmatter["snapshot_source"] = SnapshotSource.Generated.ToSchemaValue();
                break;
            case ArtifactType.SessionSummary:
                frontmatter["session_type"] = SessionType.Execution.ToSchemaValue();
                frontmatter["canonical"] = false;
                break;
        }

        return frontmatter;
    }

    public static Dictionary<string, string> CreateSections(ArtifactType artifactType)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var section in Memora.Core.Validation.ArtifactBodyRules.GetRequiredSections(artifactType))
        {
            sections[section] = section == "Acceptance Criteria"
                ? "- artifact parses\n- artifact validates"
                : $"{section} content";
        }

        if (artifactType == ArtifactType.Question)
        {
            sections["Resolution"] = string.Empty;
        }

        return sections;
    }

    public static string CreateBody(IReadOnlyDictionary<string, string> sections)
    {
        var blocks = sections.Select(section => $"## {section.Key}\n{section.Value}");
        return string.Join("\n\n", blocks);
    }

    private static string ArtifactIdPrefix(ArtifactType artifactType) =>
        artifactType switch
        {
            ArtifactType.Charter => "CHR",
            ArtifactType.Plan => "PLN",
            ArtifactType.Decision => "ADR",
            ArtifactType.Constraint => "CNS",
            ArtifactType.Question => "QST",
            ArtifactType.Outcome => "OUT",
            ArtifactType.RepoStructure => "REP",
            ArtifactType.SessionSummary => "SUM",
            ArtifactType.Note => "NTE",
            _ => throw new ArgumentOutOfRangeException(nameof(artifactType), artifactType, "Unsupported artifact type.")
        };
}
