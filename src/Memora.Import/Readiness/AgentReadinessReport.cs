namespace Memora.Import.Readiness;

public sealed record AgentReadinessReport(
    string ProjectId,
    DateTimeOffset GeneratedAtUtc,
    int EvidenceRecordCount,
    int CandidateCount,
    bool ReadyForAgentUse,
    IReadOnlyList<string> MissingContext,
    IReadOnlyList<string> MissingTests,
    IReadOnlyList<string> RiskyModules,
    IReadOnlyList<string> AdvisoryDiscoveryGaps,
    IReadOnlyList<string> NextReviewSteps)
{
    public IReadOnlyList<string> MissingContext { get; } = Normalize(MissingContext);
    public IReadOnlyList<string> MissingTests { get; } = Normalize(MissingTests);
    public IReadOnlyList<string> RiskyModules { get; } = Normalize(RiskyModules);
    public IReadOnlyList<string> AdvisoryDiscoveryGaps { get; } = Normalize(AdvisoryDiscoveryGaps);
    public IReadOnlyList<string> NextReviewSteps { get; } = Normalize(NextReviewSteps);

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values) =>
        values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
}

public sealed record FirstRunMemoryGenerationResult(
    IReadOnlyList<CandidateMemoryRecord> Candidates,
    AgentReadinessReport ReadinessReport)
{
    public IReadOnlyList<CandidateMemoryRecord> Candidates { get; } =
        Candidates?.OrderBy(candidate => candidate.Kind).ThenBy(candidate => candidate.Title, StringComparer.Ordinal).ToArray()
        ?? throw new ArgumentNullException(nameof(Candidates));
}
