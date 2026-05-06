using Memora.Core.Import;

namespace Memora.Import.GitHub;

public sealed record GitHubEvidenceImportRequest
{
    public GitHubEvidenceImportRequest(
        string projectId,
        ImportMode importMode,
        string? attachmentId = null,
        int maxItems = 100)
    {
        if (maxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Max items must be greater than zero.");
        }

        ProjectId = string.IsNullOrWhiteSpace(projectId)
            ? throw new ArgumentException("Project id is required.", nameof(projectId))
            : projectId.Trim();
        ImportMode = importMode;
        AttachmentId = string.IsNullOrWhiteSpace(attachmentId) ? null : attachmentId.Trim();
        MaxItems = maxItems;
    }

    public string ProjectId { get; }

    public ImportMode ImportMode { get; }

    public string? AttachmentId { get; }

    public int MaxItems { get; }
}

public sealed record GitHubEvidenceImportProgress(
    int TotalRecords,
    int CreatedRecords,
    int ExistingRecords,
    int IssueCount,
    int PullRequestCount,
    int ReviewCount,
    int ReviewCommentCount,
    int CommitCount,
    int ReleaseCount,
    int DiscussionCount);

public sealed record GitHubEvidenceImportResult(
    IReadOnlyList<ImportedEvidenceRecord> Records,
    GitHubEvidenceImportProgress Progress,
    IReadOnlyList<GitHubImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<ImportedEvidenceRecord> Records { get; } =
        Records?.ToArray() ?? throw new ArgumentNullException(nameof(Records));
    public IReadOnlyList<GitHubImportDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public bool IsSuccess => !Diagnostics.Any(diagnostic => diagnostic.Severity == GitHubImportDiagnosticSeverity.Error);
}
