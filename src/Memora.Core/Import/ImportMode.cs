namespace Memora.Core.Import;

public enum ImportMode
{
    FastBaseline,
    StrictGovernance,
    EvidenceCanonical,
    BulkApproval
}

public static class ImportModeExtensions
{
    public static string ToSchemaValue(this ImportMode mode) =>
        mode switch
        {
            ImportMode.FastBaseline => "fast_baseline",
            ImportMode.StrictGovernance => "strict_governance",
            ImportMode.EvidenceCanonical => "evidence_canonical",
            ImportMode.BulkApproval => "bulk_approval",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown import mode.")
        };

    public static bool TryParseSchemaValue(string? value, out ImportMode mode)
    {
        mode = ImportMode.FastBaseline;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "fast_baseline":
                mode = ImportMode.FastBaseline;
                return true;
            case "strict_governance":
                mode = ImportMode.StrictGovernance;
                return true;
            case "evidence_canonical":
                mode = ImportMode.EvidenceCanonical;
                return true;
            case "bulk_approval":
                mode = ImportMode.BulkApproval;
                return true;
            default:
                return false;
        }
    }
}
