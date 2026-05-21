using Memora.Core.Import;
using Memora.Import.Readiness;

namespace Memora.Import.Tests.Readiness;

public sealed class GitHubEvidenceCandidateGeneratorTests
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
            title: "Test",
            summary: summary ?? "summary",
            observedAtUtc: DateTimeOffset.UtcNow,
            importedAtUtc: DateTimeOffset.UtcNow,
            provenance: "test",
            trustState: ImportedEvidenceTrustState.ReviewableEvidence,
            metadata: metadata);

    private static GitHubEvidenceNormalization Normalize(IReadOnlyList<ImportedEvidenceRecord> records)
    {
        var normalizer = new GitHubEvidenceNormalizer();
        return normalizer.Normalize(records);
    }

    [Fact]
    public void Generate_EmptyNormalization_ProducesNoCandidates()
    {
        var normalization = Normalize([]);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Generate_NullProjectId_ThrowsArgumentException()
    {
        var normalization = Normalize([]);
        var generator = new GitHubEvidenceCandidateGenerator();

        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!, ImportMode.FastBaseline, normalization));
    }

    [Fact]
    public void Generate_NullNormalization_ThrowsArgumentNullException()
    {
        var generator = new GitHubEvidenceCandidateGenerator();

        Assert.Throws<ArgumentNullException>(() => generator.Generate("project", ImportMode.FastBaseline, null!));
    }

    [Fact]
    public void Generate_ApprovedReviews_ProducesContributionStyleCandidate()
    {
        var records = new[]
        {
            MakeRecord("r1", ImportedEvidenceSourceType.GitHubReview, "Approved")
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c =>
            c.Kind == CandidateMemoryKind.ContributionStyle &&
            c.Title.Contains("review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_FewReviewComments_LowConfidenceRoutedAsOpenQuestion()
    {
        var records = new[]
        {
            MakeRecord("rc1", ImportedEvidenceSourceType.GitHubReviewComment)
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        var candidate = candidates.FirstOrDefault(c => c.Title.Contains("inline", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(candidate);
        Assert.Equal(CandidateMemoryKind.OpenQuestion, candidate.Kind);
        Assert.Equal(CandidateMemoryDisposition.ReviewRequired, candidate.Disposition);
    }

    [Fact]
    public void Generate_ManyReviewComments_RoutedAsContributionStyle()
    {
        var records = Enumerable.Range(1, 5)
            .Select(i => MakeRecord($"rc{i}", ImportedEvidenceSourceType.GitHubReviewComment))
            .ToArray();
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c =>
            c.Kind == CandidateMemoryKind.ContributionStyle &&
            c.Title.Contains("inline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_StrictGovernance_CandidatesRequireReview()
    {
        var records = new[]
        {
            MakeRecord("r1", ImportedEvidenceSourceType.GitHubReview, "Approved")
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.StrictGovernance, normalization);

        Assert.All(candidates, c => Assert.Equal(CandidateMemoryDisposition.ReviewRequired, c.Disposition));
    }

    [Fact]
    public void Generate_SameInputsProduceSameCandidateIds()
    {
        var records = new[]
        {
            MakeRecord("r1", ImportedEvidenceSourceType.GitHubReview, "Approved"),
            MakeRecord("pr1", ImportedEvidenceSourceType.GitHubPullRequest,
                metadata: new Dictionary<string, string> { ["mergeCommitSha"] = "abc" })
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var first = generator.Generate("project", ImportMode.FastBaseline, normalization)
            .Select(c => c.CandidateId).ToArray();
        var second = generator.Generate("project", ImportMode.FastBaseline, normalization)
            .Select(c => c.CandidateId).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_AllCandidatesHaveRequiredMetadata()
    {
        var records = new[]
        {
            MakeRecord("r1", ImportedEvidenceSourceType.GitHubReview, "Approved"),
            MakeRecord("pr1", ImportedEvidenceSourceType.GitHubPullRequest,
                metadata: new Dictionary<string, string> { ["mergeCommitSha"] = "abc" })
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.All(candidates, c =>
        {
            Assert.NotEmpty(c.CandidateId);
            Assert.NotEmpty(c.ExtractionReason);
            Assert.NotEmpty(c.Ambiguity);
            Assert.True(c.Confidence > 0 && c.Confidence <= 1.0);
        });
    }

    [Fact]
    public void Generate_EvidenceStableIdsLinkedToCandidates()
    {
        var records = new[]
        {
            MakeRecord("r1", ImportedEvidenceSourceType.GitHubReview, "Approved")
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c => c.EvidenceStableIds.Contains("r1"));
    }

    [Fact]
    public void Generate_LargeOpenIssueBacklog_ProducesRiskCandidate()
    {
        var records = Enumerable.Range(1, 6)
            .Select(i => MakeRecord($"issue{i}", ImportedEvidenceSourceType.GitHubIssue, "open issue"))
            .ToArray();
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c => c.Kind == CandidateMemoryKind.Risk);
    }

    [Fact]
    public void Generate_Releases_ProducesAdvisoryCandidate()
    {
        var records = new[]
        {
            MakeRecord("rel1", ImportedEvidenceSourceType.GitHubRelease)
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c =>
            c.Source == CandidateMemorySource.Advisory &&
            c.Title.Contains("release", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_MergedPullRequests_ProducesContributionCandidate()
    {
        var records = new[]
        {
            MakeRecord("pr1", ImportedEvidenceSourceType.GitHubPullRequest,
                metadata: new Dictionary<string, string> { ["mergeCommitSha"] = "abc123" })
        };
        var normalization = Normalize(records);
        var generator = new GitHubEvidenceCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, normalization);

        Assert.Contains(candidates, c =>
            c.Kind == CandidateMemoryKind.ContributionStyle &&
            c.Title.Contains("merge", StringComparison.OrdinalIgnoreCase));
    }
}
