namespace Memora.Core.Import;

public sealed record ImportedEvidenceRecord
{
    public ImportedEvidenceRecord(
        string stableId,
        string projectId,
        ImportedEvidenceSourceType sourceType,
        string sourceAttachmentId,
        string sourceRepositoryIdentity,
        string sourceReference,
        string title,
        string summary,
        DateTimeOffset observedAtUtc,
        DateTimeOffset importedAtUtc,
        string provenance,
        ImportedEvidenceTrustState trustState,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        StableId = RequireValue(stableId, nameof(stableId));
        ProjectId = RequireValue(projectId, nameof(projectId));
        SourceType = sourceType;
        SourceAttachmentId = RequireValue(sourceAttachmentId, nameof(sourceAttachmentId));
        SourceRepositoryIdentity = RequireValue(sourceRepositoryIdentity, nameof(sourceRepositoryIdentity));
        SourceReference = RequireValue(sourceReference, nameof(sourceReference));
        Title = RequireValue(title, nameof(title));
        Summary = RequireValue(summary, nameof(summary));
        ObservedAtUtc = observedAtUtc;
        ImportedAtUtc = importedAtUtc;
        Provenance = RequireValue(provenance, nameof(provenance));
        TrustState = trustState;
        Metadata = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(
                metadata
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    public string StableId { get; }

    public string ProjectId { get; }

    public ImportedEvidenceSourceType SourceType { get; }

    public string SourceAttachmentId { get; }

    public string SourceRepositoryIdentity { get; }

    public string SourceReference { get; }

    public string Title { get; }

    public string Summary { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public DateTimeOffset ImportedAtUtc { get; }

    public string Provenance { get; }

    public ImportedEvidenceTrustState TrustState { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
