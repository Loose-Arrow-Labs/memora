namespace Memora.Core.Import;

public enum ImportedEvidenceTrustState
{
    ReviewableEvidence,
    BaselineEvidence,
    CanonicalEvidence
}

public static class ImportedEvidenceTrustStateExtensions
{
    public static string ToSchemaValue(this ImportedEvidenceTrustState trustState) =>
        trustState switch
        {
            ImportedEvidenceTrustState.ReviewableEvidence => "reviewable_evidence",
            ImportedEvidenceTrustState.BaselineEvidence => "baseline_evidence",
            ImportedEvidenceTrustState.CanonicalEvidence => "canonical_evidence",
            _ => throw new ArgumentOutOfRangeException(nameof(trustState), trustState, "Unknown imported evidence trust state.")
        };

    public static bool TryParseSchemaValue(string? value, out ImportedEvidenceTrustState trustState)
    {
        trustState = ImportedEvidenceTrustState.ReviewableEvidence;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "reviewable_evidence":
                trustState = ImportedEvidenceTrustState.ReviewableEvidence;
                return true;
            case "baseline_evidence":
                trustState = ImportedEvidenceTrustState.BaselineEvidence;
                return true;
            case "canonical_evidence":
                trustState = ImportedEvidenceTrustState.CanonicalEvidence;
                return true;
            default:
                return false;
        }
    }
}
