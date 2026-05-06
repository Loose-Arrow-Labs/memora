using Memora.Core.Import;

namespace Memora.Import.Git;

public sealed record LocalGitEvidenceImportRequest
{
    public LocalGitEvidenceImportRequest(
        string projectId,
        ImportMode importMode,
        string? attachmentId = null,
        int maxCommits = 200)
    {
        if (maxCommits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCommits), "Max commits must be greater than zero.");
        }

        ProjectId = string.IsNullOrWhiteSpace(projectId)
            ? throw new ArgumentException("Project id is required.", nameof(projectId))
            : projectId.Trim();
        ImportMode = importMode;
        AttachmentId = string.IsNullOrWhiteSpace(attachmentId) ? null : attachmentId.Trim();
        MaxCommits = maxCommits;
    }

    public string ProjectId { get; }

    public ImportMode ImportMode { get; }

    public string? AttachmentId { get; }

    public int MaxCommits { get; }
}

public sealed record LocalGitImportProgress(
    int TotalRecords,
    int CreatedRecords,
    int ExistingRecords,
    int CommitCount,
    int BranchCount,
    int TagCount,
    int ChangelogSignalCount);

public sealed record LocalGitEvidenceImportResult(
    IReadOnlyList<ImportedEvidenceRecord> Records,
    LocalGitImportProgress Progress,
    IReadOnlyList<LocalGitImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<ImportedEvidenceRecord> Records { get; } =
        Records?.ToArray() ?? throw new ArgumentNullException(nameof(Records));
    public IReadOnlyList<LocalGitImportDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public bool IsSuccess => !Diagnostics.Any(diagnostic => diagnostic.Severity == LocalGitImportDiagnosticSeverity.Error);
}
