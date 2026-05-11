using Memora.Core.Import;

namespace Memora.Import.Readiness;

// Normalizes imported GitHub evidence records into structured classifications
// for project understanding. Produces evidence-derived, inferred, and
// advisory/future-advisory source classifications. Conflicting or ambiguous
// signals become low-confidence candidates or questions in the candidate layer.
public sealed class GitHubEvidenceNormalizer
{
    public GitHubEvidenceNormalization Normalize(IReadOnlyList<ImportedEvidenceRecord> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var githubEvidence = evidence
            .Where(IsGitHubRecord)
            .OrderBy(r => r.StableId, StringComparer.Ordinal)
            .ToArray();

        var issues = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubIssue).ToArray();
        var pullRequests = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubPullRequest).ToArray();
        var reviews = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubReview).ToArray();
        var reviewComments = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubReviewComment).ToArray();
        var commits = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubCommit).ToArray();
        var releases = githubEvidence.Where(r => r.SourceType == ImportedEvidenceSourceType.GitHubRelease).ToArray();

        return new GitHubEvidenceNormalization(
            totalRecords: githubEvidence.Length,
            issueRecords: issues,
            pullRequestRecords: pullRequests,
            reviewRecords: reviews,
            reviewCommentRecords: reviewComments,
            commitRecords: commits,
            releaseRecords: releases,
            openIssues: issues.Where(r => r.Summary?.Contains("open", StringComparison.OrdinalIgnoreCase) == true).ToArray(),
            mergedPullRequests: pullRequests.Where(r => r.Metadata.TryGetValue("mergeCommitSha", out var sha) && !string.IsNullOrEmpty(sha)).ToArray(),
            approvedReviews: reviews.Where(r => r.Summary?.Contains("approved", StringComparison.OrdinalIgnoreCase) == true).ToArray());
    }

    private static bool IsGitHubRecord(ImportedEvidenceRecord r) =>
        r.SourceType is
            ImportedEvidenceSourceType.GitHubIssue or
            ImportedEvidenceSourceType.GitHubPullRequest or
            ImportedEvidenceSourceType.GitHubReview or
            ImportedEvidenceSourceType.GitHubReviewComment or
            ImportedEvidenceSourceType.GitHubCommit or
            ImportedEvidenceSourceType.GitHubRelease or
            ImportedEvidenceSourceType.GitHubDiscussion;
}

public sealed class GitHubEvidenceNormalization
{
    public GitHubEvidenceNormalization(
        int totalRecords,
        IReadOnlyList<ImportedEvidenceRecord> issueRecords,
        IReadOnlyList<ImportedEvidenceRecord> pullRequestRecords,
        IReadOnlyList<ImportedEvidenceRecord> reviewRecords,
        IReadOnlyList<ImportedEvidenceRecord> reviewCommentRecords,
        IReadOnlyList<ImportedEvidenceRecord> commitRecords,
        IReadOnlyList<ImportedEvidenceRecord> releaseRecords,
        IReadOnlyList<ImportedEvidenceRecord> openIssues,
        IReadOnlyList<ImportedEvidenceRecord> mergedPullRequests,
        IReadOnlyList<ImportedEvidenceRecord> approvedReviews)
    {
        TotalRecords = totalRecords;
        IssueRecords = issueRecords ?? throw new ArgumentNullException(nameof(issueRecords));
        PullRequestRecords = pullRequestRecords ?? throw new ArgumentNullException(nameof(pullRequestRecords));
        ReviewRecords = reviewRecords ?? throw new ArgumentNullException(nameof(reviewRecords));
        ReviewCommentRecords = reviewCommentRecords ?? throw new ArgumentNullException(nameof(reviewCommentRecords));
        CommitRecords = commitRecords ?? throw new ArgumentNullException(nameof(commitRecords));
        ReleaseRecords = releaseRecords ?? throw new ArgumentNullException(nameof(releaseRecords));
        OpenIssues = openIssues ?? throw new ArgumentNullException(nameof(openIssues));
        MergedPullRequests = mergedPullRequests ?? throw new ArgumentNullException(nameof(mergedPullRequests));
        ApprovedReviews = approvedReviews ?? throw new ArgumentNullException(nameof(approvedReviews));
    }

    public int TotalRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> IssueRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> PullRequestRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> ReviewRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> ReviewCommentRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> CommitRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> ReleaseRecords { get; }
    public IReadOnlyList<ImportedEvidenceRecord> OpenIssues { get; }
    public IReadOnlyList<ImportedEvidenceRecord> MergedPullRequests { get; }
    public IReadOnlyList<ImportedEvidenceRecord> ApprovedReviews { get; }
}
