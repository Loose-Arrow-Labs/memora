using System.Text.RegularExpressions;
using Memora.Core.Import;

namespace Memora.Import.Safety;

public sealed class ImportContentSafetyFilter
{
    private const string RedactionText = "[REDACTED]";

    private static readonly IReadOnlyList<SecretRule> Rules =
    [
        new("openai_project_key", "OpenAI project API key", new Regex(@"sk-proj-[A-Za-z0-9_-]{10,}", RegexOptions.Compiled), BlocksPersistence: false),
        new("openai_api_key", "OpenAI API key", new Regex(@"sk-[A-Za-z0-9]{20,}", RegexOptions.Compiled), BlocksPersistence: false),
        new("github_token", "GitHub token", new Regex(@"(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})", RegexOptions.Compiled), BlocksPersistence: false),
        new("aws_access_key", "AWS access key id", new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled), BlocksPersistence: false),
        new("credential_assignment", "credential assignment", new Regex(@"(?i)(password|passwd|secret|token)\s*=\s*[^;\s]+", RegexOptions.Compiled), BlocksPersistence: false),
        new("private_key", "private key material", new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled | RegexOptions.Singleline), BlocksPersistence: true)
    ];

    public ImportSafetyFilterResult Filter(IReadOnlyList<ImportedEvidenceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var diagnostics = new List<ImportSafetyDiagnostic>();
        var safeRecords = new List<ImportedEvidenceRecord>();
        var anyBlocked = false;

        foreach (var record in records)
        {
            var recordBlocked = false;
            var redactedTitle = FilterValue(record, "title", record.Title, diagnostics, ref recordBlocked);
            var redactedSummary = FilterValue(record, "summary", record.Summary, diagnostics, ref recordBlocked);
            var redactedProvenance = FilterValue(record, "provenance", record.Provenance, diagnostics, ref recordBlocked);
            var redactedMetadata = record.Metadata
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => FilterValue(record, $"metadata.{pair.Key}", pair.Value, diagnostics, ref recordBlocked),
                    StringComparer.Ordinal);

            if (recordBlocked)
            {
                anyBlocked = true;
                continue;
            }

            safeRecords.Add(
                new ImportedEvidenceRecord(
                    record.StableId,
                    record.ProjectId,
                    record.SourceType,
                    record.SourceAttachmentId,
                    record.SourceRepositoryIdentity,
                    record.SourceReference,
                    redactedTitle,
                    redactedSummary,
                    record.ObservedAtUtc,
                    record.ImportedAtUtc,
                    redactedProvenance,
                    record.TrustState,
                    redactedMetadata));
        }

        return new ImportSafetyFilterResult(safeRecords, diagnostics, anyBlocked);
    }

    private static string FilterValue(
        ImportedEvidenceRecord record,
        string field,
        string value,
        ICollection<ImportSafetyDiagnostic> diagnostics,
        ref bool recordBlocked)
    {
        var redacted = value;

        foreach (var rule in Rules)
        {
            if (!rule.Pattern.IsMatch(redacted))
            {
                continue;
            }

            diagnostics.Add(
                new ImportSafetyDiagnostic(
                    rule.BlocksPersistence ? "import.secret.blocked" : "import.secret.redacted",
                    rule.BlocksPersistence
                        ? $"Unsafe imported evidence was blocked before persistence: {rule.Reason}."
                        : $"Sensitive imported evidence was redacted before persistence: {rule.Reason}.",
                    rule.BlocksPersistence ? ImportSafetyDiagnosticSeverity.Error : ImportSafetyDiagnosticSeverity.Warning,
                    record.StableId,
                    record.SourceType.ToSchemaValue(),
                    field,
                    rule.Reason));

            if (rule.BlocksPersistence)
            {
                recordBlocked = true;
            }

            redacted = rule.Pattern.Replace(redacted, RedactionText);
        }

        return redacted;
    }

    private sealed record SecretRule(string Code, string Reason, Regex Pattern, bool BlocksPersistence);
}

public sealed record ImportSafetyFilterResult(
    IReadOnlyList<ImportedEvidenceRecord> Records,
    IReadOnlyList<ImportSafetyDiagnostic> Diagnostics,
    bool BlocksPersistence)
{
    public IReadOnlyList<ImportedEvidenceRecord> Records { get; } =
        Records?.ToArray() ?? throw new ArgumentNullException(nameof(Records));
    public IReadOnlyList<ImportSafetyDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));
}
