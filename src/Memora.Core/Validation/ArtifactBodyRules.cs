using Memora.Core.Artifacts;

namespace Memora.Core.Validation;

public static class ArtifactBodyRules
{
    public static IReadOnlyList<string> GetRequiredSections(ArtifactType artifactType) =>
        artifactType switch
        {
            ArtifactType.Charter =>
            [
                "Problem Statement",
                "Primary Users / Stakeholders",
                "Current Pain",
                "Desired Outcome",
                "Definition of Success"
            ],
            ArtifactType.Plan =>
            [
                "Goal",
                "Scope",
                "Acceptance Criteria",
                "Notes"
            ],
            ArtifactType.Decision =>
            [
                "Context",
                "Decision",
                "Alternatives Considered",
                "Consequences"
            ],
            ArtifactType.Constraint =>
            [
                "Constraint",
                "Why It Exists",
                "Implications"
            ],
            ArtifactType.Question =>
            [
                "Question",
                "Context",
                "Possible Directions",
                "Resolution"
            ],
            ArtifactType.Outcome =>
            [
                "What Happened",
                "Why",
                "Impact",
                "Follow-up"
            ],
            ArtifactType.RepoStructure =>
            [
                "Root",
                "Key Directories",
                "Key Files",
                "Notes"
            ],
            ArtifactType.SessionSummary =>
            [
                "Summary",
                "Artifacts Created",
                "Artifacts Updated",
                "Open Threads"
            ],
            ArtifactType.Note =>
            [
                // Notes are intentionally low-ceremony: any non-empty body is
                // acceptable. No required sections.
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(artifactType), artifactType, "Unsupported artifact type.")
        };
}
