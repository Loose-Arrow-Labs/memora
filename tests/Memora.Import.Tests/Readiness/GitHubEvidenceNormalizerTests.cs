using Memora.Core.Import;
using Memora.Import.Readiness;

namespace Memora.Import.Tests.Readiness;

public sealed class GitHubEvidenceNormalizerTests
{
    private static ImportedEvidenceRecord MakeRecord(
        string stableId,
        ImportedEvidenceSourceType sourceType,
        string? summary = null,
        Dictionary<string, string>? metadata = null) =>
        new(
            stableId: stableId,
            projectId: "proj",
            sourceType: sourceType,
            sourceAttachmentId: "att",
            sourceRepositoryIdentity: "owner/repo",
            sourceReference: stableId,
            title: "Test record",
            summary: summary ?? "no summary",
            observedAtUtc: DateTimeOffset.UtcNow,
            importedAtUtc: DateTimeOffset.UtcNow,
            provenance: "test",
            trustState: ImportedEvidenceTrustState.ReviewableEvidence,
            metadata: metadata);

    [Fact]
    public void Normalize_NullArgument_ThrowsArgumentNullException()
    {
        var normalizer = new GitHubEvidenceNormalizer();
        Assert.Throws<ArgumentNullException>(() => normalizer.Normalize(null!));
    }

    [Fact]
    public void Normalize_EmptyList_ReturnsEmptyBuckets()
    {
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize([]);

        Assert.Equal(0, result.TotalRecords);
        Assert.Empty(result.IssueRecords);
        Assert.Empty(result.PullRequestRecords);
        Assert.Empty(result.ReviewRecords);
        Assert.Empty(result.ReviewCommentRecords);
        Assert.Empty(result.CommitRecords);
        Assert.Empty(result.ReleaseRecords);
        Assert.Empty(result.OpenIssues);
        Assert.Empty(result.MergedPullRequests);
        Assert.Empty(result.ApprovedReviews);
    }

    [Fact]
    public void Normalize_NonGitHubRecords_ExcludedFromResults()
    {
        var records = new[]
        {
            MakeRecord("local-1", ImportedEvidenceSourceType.LocalGitCommit),
            MakeRecord("local-2", ImportedEvidenceSourceType.LocalGitBranch),
            MakeRecord("gh-1", ImportedEvidenceSourceType.GitHubIssue)
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(1, result.TotalRecords);
        Assert.Single(result.IssueRecords);
    }

    [Fact]
    public void Normalize_IssueRecords_GroupedCorrectly()
    {
        var records = new[]
        {
            MakeRecord("issue-1", ImportedEvidenceSourceType.GitHubIssue, "open issue"),
            MakeRecord("issue-2", ImportedEvidenceSourceType.GitHubIssue, "closed issue"),
            MakeRecord("pr-1", ImportedEvidenceSourceType.GitHubPullRequest)
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(2, result.IssueRecords.Count);
        Assert.Single(result.PullRequestRecords);
    }

    [Fact]
    public void Normalize_OpenIssues_IdentifiedByOpenInSummary()
    {
        var records = new[]
        {
            MakeRecord("issue-1", ImportedEvidenceSourceType.GitHubIssue, "Issue is open"),
            MakeRecord("issue-2", ImportedEvidenceSourceType.GitHubIssue, "Issue is closed"),
            MakeRecord("issue-3", ImportedEvidenceSourceType.GitHubIssue, "Status: OPEN")
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(2, result.OpenIssues.Count);
        Assert.Contains(result.OpenIssues, r => r.StableId == "issue-1");
        Assert.Contains(result.OpenIssues, r => r.StableId == "issue-3");
    }

    [Fact]
    public void Normalize_MergedPullRequests_IdentifiedByMergeCommitSha()
    {
        var records = new[]
        {
            MakeRecord("pr-1", ImportedEvidenceSourceType.GitHubPullRequest,
                metadata: new Dictionary<string, string> { ["mergeCommitSha"] = "abc123" }),
            MakeRecord("pr-2", ImportedEvidenceSourceType.GitHubPullRequest,
                metadata: new Dictionary<string, string> { ["mergeCommitSha"] = "" }),
            MakeRecord("pr-3", ImportedEvidenceSourceType.GitHubPullRequest)
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Single(result.MergedPullRequests);
        Assert.Equal("pr-1", result.MergedPullRequests[0].StableId);
    }

    [Fact]
    public void Normalize_ApprovedReviews_IdentifiedByApprovedInSummary()
    {
        var records = new[]
        {
            MakeRecord("review-1", ImportedEvidenceSourceType.GitHubReview, "Review Approved"),
            MakeRecord("review-2", ImportedEvidenceSourceType.GitHubReview, "Changes requested"),
            MakeRecord("review-3", ImportedEvidenceSourceType.GitHubReview, "APPROVED the changes")
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(2, result.ApprovedReviews.Count);
    }

    [Fact]
    public void Normalize_RecordsOrderedByStableId()
    {
        var records = new[]
        {
            MakeRecord("z-issue", ImportedEvidenceSourceType.GitHubIssue),
            MakeRecord("a-issue", ImportedEvidenceSourceType.GitHubIssue),
            MakeRecord("m-issue", ImportedEvidenceSourceType.GitHubIssue)
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(["a-issue", "m-issue", "z-issue"], result.IssueRecords.Select(r => r.StableId).ToArray());
    }

    [Fact]
    public void Normalize_AllGitHubSourceTypes_Counted()
    {
        var records = new[]
        {
            MakeRecord("issue-1", ImportedEvidenceSourceType.GitHubIssue),
            MakeRecord("pr-1", ImportedEvidenceSourceType.GitHubPullRequest),
            MakeRecord("review-1", ImportedEvidenceSourceType.GitHubReview),
            MakeRecord("rc-1", ImportedEvidenceSourceType.GitHubReviewComment),
            MakeRecord("commit-1", ImportedEvidenceSourceType.GitHubCommit),
            MakeRecord("release-1", ImportedEvidenceSourceType.GitHubRelease),
            MakeRecord("disc-1", ImportedEvidenceSourceType.GitHubDiscussion)
        };
        var normalizer = new GitHubEvidenceNormalizer();

        var result = normalizer.Normalize(records);

        Assert.Equal(7, result.TotalRecords);
        Assert.Single(result.IssueRecords);
        Assert.Single(result.PullRequestRecords);
        Assert.Single(result.ReviewRecords);
        Assert.Single(result.ReviewCommentRecords);
        Assert.Single(result.CommitRecords);
        Assert.Single(result.ReleaseRecords);
    }
}
