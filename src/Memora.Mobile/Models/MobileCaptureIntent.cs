namespace Memora.Mobile.Models;

public enum MobileCaptureIntent
{
    Question,
    DecisionDraft,
    PlanningNote,
    ProposalDraft
}

public sealed record MobileCaptureIntentInfo(
    MobileCaptureIntent Intent,
    string SchemaValue,
    string DisplayName,
    string LifecycleTargetSchemaValue,
    string IntentHint,
    string BodyTemplate);

public static class MobileCaptureIntentCatalog
{
    public static IReadOnlyList<MobileCaptureIntentInfo> All { get; } =
    [
        new(
            MobileCaptureIntent.Question,
            "question",
            "Question",
            "planning_input",
            "A question about the project. Maps to planning_input.",
            BuildTemplate("## Question", "## Context", "## Possible Directions")),
        new(
            MobileCaptureIntent.DecisionDraft,
            "decision_draft",
            "Decision draft",
            "proposal_draft",
            "A decision direction worth reviewing. Maps to proposal_draft.",
            BuildTemplate("## Context", "## Decision Direction", "## Alternatives Considered", "## Open Concerns")),
        new(
            MobileCaptureIntent.PlanningNote,
            "planning_note",
            "Planning note",
            "planning_input",
            "Free-form note to retain as planning input. Maps to planning_input.",
            BuildTemplate("## Note", "## Why This Matters")),
        new(
            MobileCaptureIntent.ProposalDraft,
            "proposal_draft",
            "Proposal draft",
            "proposal_draft",
            "A sketch of a future artifact for review. Maps to proposal_draft.",
            BuildTemplate("## Summary", "## Proposed Content", "## Notes For Review"))
    ];

    public static MobileCaptureIntentInfo GetByIntent(MobileCaptureIntent intent)
    {
        foreach (var info in All)
        {
            if (info.Intent == intent)
            {
                return info;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown mobile capture intent.");
    }

    public static bool IsProposalKind(MobileCaptureIntent intent) =>
        intent is MobileCaptureIntent.DecisionDraft or MobileCaptureIntent.ProposalDraft;

    private static string BuildTemplate(params string[] headings)
    {
        var lines = new List<string>(headings.Length * 2);
        foreach (var heading in headings)
        {
            lines.Add(heading);
            lines.Add(string.Empty);
        }

        return string.Join('\n', lines);
    }
}

public static class MobileProposedArtifactTypes
{
    public static IReadOnlyList<string> All { get; } =
    [
        "charter",
        "plan",
        "decision",
        "constraint",
        "question",
        "outcome",
        "repo_structure",
        "session_summary"
    ];
}
