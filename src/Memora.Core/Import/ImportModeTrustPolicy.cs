namespace Memora.Core.Import;

public static class ImportModeTrustPolicy
{
    public static ImportedEvidenceTrustState GetEvidenceTrustState(ImportMode mode) =>
        mode switch
        {
            ImportMode.FastBaseline => ImportedEvidenceTrustState.BaselineEvidence,
            ImportMode.StrictGovernance => ImportedEvidenceTrustState.ReviewableEvidence,
            ImportMode.EvidenceCanonical => ImportedEvidenceTrustState.CanonicalEvidence,
            ImportMode.BulkApproval => ImportedEvidenceTrustState.ReviewableEvidence,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown import mode.")
        };
}
