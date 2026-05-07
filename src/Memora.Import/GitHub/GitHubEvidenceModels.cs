namespace Memora.Import.GitHub;

public sealed record GitHubIssueEvidence(
    int Number,
    string Url,
    string Title,
    string State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GitHubPullRequestEvidence(
    int Number,
    string Url,
    string Title,
    string State,
    string? MergeCommitSha,
    DateTimeOffset? MergedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GitHubReviewEvidence(
    int PullRequestNumber,
    string ReviewId,
    string Url,
    string State,
    string? Author,
    DateTimeOffset? SubmittedAtUtc);

public sealed record GitHubReviewCommentEvidence(
    int PullRequestNumber,
    string CommentId,
    string Url,
    string? Path,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GitHubCommitEvidence(
    string Sha,
    string Url,
    string Message,
    string? Author,
    DateTimeOffset AuthoredAtUtc);

public sealed record GitHubReleaseEvidence(
    string ReleaseId,
    string Url,
    string Name,
    string TagName,
    DateTimeOffset? PublishedAtUtc);

public sealed record GitHubDiscussionEvidence(
    string DiscussionId,
    string Url,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GitHubEvidenceSnapshot(
    IReadOnlyList<GitHubIssueEvidence> Issues,
    IReadOnlyList<GitHubPullRequestEvidence> PullRequests,
    IReadOnlyList<GitHubReviewEvidence> Reviews,
    IReadOnlyList<GitHubReviewCommentEvidence> ReviewComments,
    IReadOnlyList<GitHubCommitEvidence> Commits,
    IReadOnlyList<GitHubReleaseEvidence> Releases,
    IReadOnlyList<GitHubDiscussionEvidence> Discussions,
    bool IsPartial,
    IReadOnlyList<GitHubImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<GitHubIssueEvidence> Issues { get; } = Issues?.ToArray() ?? throw new ArgumentNullException(nameof(Issues));
    public IReadOnlyList<GitHubPullRequestEvidence> PullRequests { get; } = PullRequests?.ToArray() ?? throw new ArgumentNullException(nameof(PullRequests));
    public IReadOnlyList<GitHubReviewEvidence> Reviews { get; } = Reviews?.ToArray() ?? throw new ArgumentNullException(nameof(Reviews));
    public IReadOnlyList<GitHubReviewCommentEvidence> ReviewComments { get; } = ReviewComments?.ToArray() ?? throw new ArgumentNullException(nameof(ReviewComments));
    public IReadOnlyList<GitHubCommitEvidence> Commits { get; } = Commits?.ToArray() ?? throw new ArgumentNullException(nameof(Commits));
    public IReadOnlyList<GitHubReleaseEvidence> Releases { get; } = Releases?.ToArray() ?? throw new ArgumentNullException(nameof(Releases));
    public IReadOnlyList<GitHubDiscussionEvidence> Discussions { get; } = Discussions?.ToArray() ?? throw new ArgumentNullException(nameof(Discussions));
    public IReadOnlyList<GitHubImportDiagnostic> Diagnostics { get; } = Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));
}

public sealed record GitHubEvidenceClientResult(
    GitHubEvidenceSnapshot? Snapshot,
    IReadOnlyList<GitHubImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<GitHubImportDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public bool IsSuccess => Snapshot is not null && !Diagnostics.Any(diagnostic => diagnostic.Severity == GitHubImportDiagnosticSeverity.Error);

    public static GitHubEvidenceClientResult Succeeded(GitHubEvidenceSnapshot snapshot) =>
        new(snapshot, snapshot.Diagnostics);

    public static GitHubEvidenceClientResult Failed(params GitHubImportDiagnostic[] diagnostics) =>
        new(null, diagnostics);
}

public interface IGitHubEvidenceClient
{
    GitHubEvidenceClientResult Fetch(string remoteUrl, int maxItems);
}
