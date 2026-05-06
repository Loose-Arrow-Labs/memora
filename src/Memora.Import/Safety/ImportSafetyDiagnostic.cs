namespace Memora.Import.Safety;

public enum ImportSafetyDiagnosticSeverity
{
    Warning,
    Error
}

public sealed record ImportSafetyDiagnostic(
    string Code,
    string Message,
    ImportSafetyDiagnosticSeverity Severity,
    string StableEvidenceId,
    string SourceType,
    string Field,
    string Reason)
{
    public string Code { get; } = RequireValue(Code, nameof(Code));
    public string Message { get; } = RequireValue(Message, nameof(Message));
    public ImportSafetyDiagnosticSeverity Severity { get; } = Severity;
    public string StableEvidenceId { get; } = RequireValue(StableEvidenceId, nameof(StableEvidenceId));
    public string SourceType { get; } = RequireValue(SourceType, nameof(SourceType));
    public string Field { get; } = RequireValue(Field, nameof(Field));
    public string Reason { get; } = RequireValue(Reason, nameof(Reason));

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
