using System.Security.Cryptography;
using System.Text;
using Memora.Core.Import;

namespace Memora.Import.Readiness;

public sealed class GitHubEvidenceCandidateGenerator
{
    private const double LowConfidenceThreshold = 0.60;

    public IReadOnlyList<CandidateMemoryRecord> Generate(
        string projectId,
        ImportMode importMode,
        GitHubEvidenceNormalization normalization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(normalization);

        var candidates = new List<CandidateMemoryRecord>();
        candidates.AddRange(BuildContributionStyleCandidates(projectId, importMode, normalization));
        candidates.AddRange(BuildRiskCandidates(projectId, importMode, normalization));
        candidates.AddRange(BuildAdvisoryCandidates(projectId, importMode, normalization));
        return candidates
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<CandidateMemoryRecord> BuildContributionStyleCandidates(
        string projectId, ImportMode importMode, GitHubEvidenceNormalization normalization)
    {
        if (normalization.ApprovedReviews.Count > 0)
        {
            var evidenceIds = normalization.ApprovedReviews.Select(r => r.StableId);
            yield return Create(
                projectId,
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.EvidenceDerived,
                "Code review approval workflow is active in this repository",
                "Pull request reviews with approvals are present, indicating an active code review practice.",
                0.80,
                "Approval presence is inferred from review summaries; actual review standards may vary.",
                $"Detected {normalization.ApprovedReviews.Count} approved review(s) in imported evidence.",
                importMode,
                evidenceIds);
        }

        if (normalization.ReviewCommentRecords.Count > 0)
        {
            var confidence = normalization.ReviewCommentRecords.Count >= 3 ? 0.72 : 0.55;
            var evidenceIds = normalization.ReviewCommentRecords.Select(r => r.StableId);
            yield return Create(
                projectId,
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.EvidenceDerived,
                "Review comments indicate inline code feedback expectations",
                "Inline review comments are present, suggesting code-level feedback is part of the contribution process.",
                confidence,
                "Comment content is not read in v1; review quality and standards are not assessed.",
                $"Detected {normalization.ReviewCommentRecords.Count} review comment(s) in imported evidence.",
                importMode,
                evidenceIds);
        }

        if (normalization.MergedPullRequests.Count > 0)
        {
            var evidenceIds = normalization.MergedPullRequests.Select(r => r.StableId);
            yield return Create(
                projectId,
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.EvidenceDerived,
                "Pull request merge workflow is established",
                "Merged pull requests indicate contributions are integrated via pull request workflow.",
                0.75,
                "Merge requirements and branch protection rules are not observable from metadata alone.",
                $"Detected {normalization.MergedPullRequests.Count} merged pull request(s).",
                importMode,
                evidenceIds);
        }
    }

    private static IEnumerable<CandidateMemoryRecord> BuildRiskCandidates(
        string projectId, ImportMode importMode, GitHubEvidenceNormalization normalization)
    {
        if (normalization.OpenIssues.Count > 5)
        {
            var evidenceIds = normalization.OpenIssues.Select(r => r.StableId);
            yield return Create(
                projectId,
                CandidateMemoryKind.Risk,
                CandidateMemorySource.EvidenceDerived,
                $"Repository has {normalization.OpenIssues.Count} open issues indicating active backlog",
                "A significant number of open issues suggests ongoing work items that may affect scope or planning.",
                0.65,
                "Issue content and priority are not observable from metadata; backlog health is not assessed.",
                $"Detected {normalization.OpenIssues.Count} open issue(s) in imported evidence.",
                importMode,
                evidenceIds);
        }
    }

    private static IEnumerable<CandidateMemoryRecord> BuildAdvisoryCandidates(
        string projectId, ImportMode importMode, GitHubEvidenceNormalization normalization)
    {
        if (normalization.ReleaseRecords.Count > 0)
        {
            var evidenceIds = normalization.ReleaseRecords.Select(r => r.StableId);
            yield return Create(
                projectId,
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.Advisory,
                "Tagged releases suggest a structured release cadence",
                "GitHub releases are present, which may indicate a versioned release process.",
                0.62,
                "Release frequency and branching strategy are not derivable from release count alone.",
                $"Detected {normalization.ReleaseRecords.Count} tagged release(s).",
                importMode,
                evidenceIds);
        }
    }

    private static CandidateMemoryRecord Create(
        string projectId,
        CandidateMemoryKind kind,
        CandidateMemorySource source,
        string title,
        string summary,
        double confidence,
        string ambiguity,
        string extractionReason,
        ImportMode importMode,
        IEnumerable<string> evidenceStableIds)
    {
        var evidenceIds = evidenceStableIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        // M12-09: route low-confidence candidates as open questions
        var effectiveKind = confidence < LowConfidenceThreshold ? CandidateMemoryKind.OpenQuestion : kind;
        var disposition = ResolveDisposition(source, confidence, importMode);

        var id = CreateCandidateId(projectId, effectiveKind, source, title, evidenceIds);
        return new CandidateMemoryRecord(
            id, effectiveKind, source, title, summary, confidence,
            ambiguity, extractionReason, disposition, evidenceIds);
    }

    private static CandidateMemoryDisposition ResolveDisposition(
        CandidateMemorySource source, double confidence, ImportMode importMode)
    {
        if (confidence < LowConfidenceThreshold)
            return CandidateMemoryDisposition.ReviewRequired;

        return source switch
        {
            CandidateMemorySource.EvidenceDerived => importMode switch
            {
                ImportMode.StrictGovernance => CandidateMemoryDisposition.ReviewRequired,
                ImportMode.BulkApproval => CandidateMemoryDisposition.GroupedBaselineReview,
                _ => CandidateMemoryDisposition.BaselineMemory
            },
            _ => importMode == ImportMode.BulkApproval
                ? CandidateMemoryDisposition.GroupedBaselineReview
                : CandidateMemoryDisposition.ReviewRequired
        };
    }

    private static string CreateCandidateId(
        string projectId,
        CandidateMemoryKind kind,
        CandidateMemorySource source,
        string title,
        IReadOnlyList<string> evidenceIds)
    {
        var input = $"{projectId}\n{kind}\n{source}\n{title}\n{string.Join("\n", evidenceIds)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"CAN-{Convert.ToHexString(hash)[..16]}";
    }
}
