namespace Memora.Import.Readiness;

public enum CandidateMemoryKind
{
    RepoStructure,
    BuildCommand,
    TestCommand,
    Constraint,
    Outcome,
    ContributionStyle,
    Risk,
    OpenQuestion
}

public enum CandidateMemoryDisposition
{
    BaselineMemory,
    ReviewRequired,
    GroupedBaselineReview
}

public enum CandidateMemorySource
{
    EvidenceDerived,
    Inferred,
    Advisory
}

public sealed record CandidateMemoryRecord(
    string CandidateId,
    CandidateMemoryKind Kind,
    CandidateMemorySource Source,
    string Title,
    string Summary,
    double Confidence,
    string Ambiguity,
    string ExtractionReason,
    CandidateMemoryDisposition Disposition,
    IReadOnlyList<string> EvidenceStableIds)
{
    public IReadOnlyList<string> EvidenceStableIds { get; } =
        EvidenceStableIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray()
        ?? throw new ArgumentNullException(nameof(EvidenceStableIds));
}
