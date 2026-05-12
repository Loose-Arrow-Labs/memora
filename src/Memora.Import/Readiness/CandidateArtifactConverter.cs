using Memora.Core.Artifacts;

namespace Memora.Import.Readiness;

// Converts non-canonical CandidateMemoryRecord objects into Draft ArtifactDocuments
// for inspection by the review workflow. Converted artifacts remain Draft (non-canonical)
// until a human review decision promotes or deprecates them.
public sealed class CandidateArtifactConverter
{
    public IReadOnlyList<ArtifactDocument> ConvertAll(
        string projectId,
        IReadOnlyList<CandidateMemoryRecord> candidates,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Select(candidate => Convert(projectId, candidate, now))
            .OrderBy(artifact => artifact.Type.ToSchemaValue(), StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Title, StringComparer.Ordinal)
            .ToArray();
    }

    public ArtifactDocument Convert(string projectId, CandidateMemoryRecord candidate, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(candidate);

        var tags = BuildTags(candidate);
        var body = BuildBody(candidate);
        var sections = BuildSections(candidate);
        var links = ArtifactLinks.Empty;

        return candidate.Kind switch
        {
            CandidateMemoryKind.Decision => new ArchitectureDecisionArtifact(
                Id: candidate.CandidateId,
                ProjectId: projectId,
                Status: ArtifactStatus.Draft,
                Title: candidate.Title,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                Revision: 1,
                Tags: tags,
                Provenance: candidate.ExtractionReason,
                Reason: candidate.Summary,
                Links: links,
                Body: body,
                Sections: sections,
                DecisionDate: now.ToString("yyyy-MM-dd")),

            CandidateMemoryKind.OpenQuestion => new OpenQuestionArtifact(
                Id: candidate.CandidateId,
                ProjectId: projectId,
                Status: ArtifactStatus.Draft,
                Title: candidate.Title,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                Revision: 1,
                Tags: tags,
                Provenance: candidate.ExtractionReason,
                Reason: candidate.Summary,
                Links: links,
                Body: body,
                Sections: sections,
                QuestionStatus: QuestionStatus.Open,
                Priority: ArtifactPriority.Normal),

            CandidateMemoryKind.RepoStructure => new RepoStructureArtifact(
                Id: candidate.CandidateId,
                ProjectId: projectId,
                Status: ArtifactStatus.Draft,
                Title: candidate.Title,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                Revision: 1,
                Tags: tags,
                Provenance: candidate.ExtractionReason,
                Reason: candidate.Summary,
                Links: links,
                Body: body,
                Sections: sections,
                SnapshotSource: SnapshotSource.Generated),

            CandidateMemoryKind.Outcome => new OutcomeArtifact(
                Id: candidate.CandidateId,
                ProjectId: projectId,
                Status: ArtifactStatus.Draft,
                Title: candidate.Title,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                Revision: 1,
                Tags: tags,
                Provenance: candidate.ExtractionReason,
                Reason: candidate.Summary,
                Links: links,
                Body: body,
                Sections: sections,
                Outcome: OutcomeKind.Mixed),

            _ => new ConstraintArtifact(
                Id: candidate.CandidateId,
                ProjectId: projectId,
                Status: ArtifactStatus.Draft,
                Title: candidate.Title,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                Revision: 1,
                Tags: tags,
                Provenance: candidate.ExtractionReason,
                Reason: candidate.Summary,
                Links: links,
                Body: body,
                Sections: sections,
                ConstraintKind: MapConstraintKind(candidate.Kind),
                Severity: MapSeverity(candidate.Kind))
        };
    }

    private static IReadOnlyList<string> BuildTags(CandidateMemoryRecord candidate)
    {
        var tags = new List<string>
        {
            $"source:{candidate.Source.ToString().ToLowerInvariant()}",
            $"kind:{candidate.Kind.ToString().ToLowerInvariant()}"
        };

        if (candidate.EvidenceStableIds.Count > 0)
        {
            tags.Add("has-evidence");
        }

        return tags
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildBody(CandidateMemoryRecord candidate)
    {
        var confidencePct = (int)(candidate.Confidence * 100);
        return $"""
            {candidate.Summary}

            **Confidence:** {confidencePct}%
            **Ambiguity:** {candidate.Ambiguity}
            **Extraction:** {candidate.ExtractionReason}
            **Source classification:** {candidate.Source}
            """;
    }

    private static IReadOnlyDictionary<string, string> BuildSections(CandidateMemoryRecord candidate)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ambiguity"] = candidate.Ambiguity,
            ["extraction_reason"] = candidate.ExtractionReason,
            ["source_classification"] = candidate.Source.ToString()
        };

        if (candidate.EvidenceStableIds.Count > 0)
        {
            sections["evidence_ids"] = string.Join(", ", candidate.EvidenceStableIds);
        }

        return sections;
    }

    private static ConstraintKind MapConstraintKind(CandidateMemoryKind kind) =>
        kind switch
        {
            CandidateMemoryKind.Risk => ConstraintKind.Operational,
            CandidateMemoryKind.ContributionStyle => ConstraintKind.Workflow,
            CandidateMemoryKind.Contract => ConstraintKind.Workflow,
            CandidateMemoryKind.BuildCommand or CandidateMemoryKind.TestCommand => ConstraintKind.Technical,
            _ => ConstraintKind.Technical
        };

    private static ConstraintSeverity MapSeverity(CandidateMemoryKind kind) =>
        kind switch
        {
            CandidateMemoryKind.Risk => ConstraintSeverity.High,
            _ => ConstraintSeverity.Normal
        };
}
