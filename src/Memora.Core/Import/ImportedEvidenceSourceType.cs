namespace Memora.Core.Import;

public enum ImportedEvidenceSourceType
{
    LocalGitCommit,
    LocalGitBranch,
    LocalGitTag,
    LocalGitChangelogSignal,
    GitHubIssue,
    GitHubPullRequest,
    GitHubReview,
    GitHubReviewComment,
    GitHubCommit,
    GitHubRelease,
    GitHubDiscussion
}

public static class ImportedEvidenceSourceTypeExtensions
{
    public static string ToSchemaValue(this ImportedEvidenceSourceType sourceType) =>
        sourceType switch
        {
            ImportedEvidenceSourceType.LocalGitCommit => "local_git_commit",
            ImportedEvidenceSourceType.LocalGitBranch => "local_git_branch",
            ImportedEvidenceSourceType.LocalGitTag => "local_git_tag",
            ImportedEvidenceSourceType.LocalGitChangelogSignal => "local_git_changelog_signal",
            ImportedEvidenceSourceType.GitHubIssue => "github_issue",
            ImportedEvidenceSourceType.GitHubPullRequest => "github_pull_request",
            ImportedEvidenceSourceType.GitHubReview => "github_review",
            ImportedEvidenceSourceType.GitHubReviewComment => "github_review_comment",
            ImportedEvidenceSourceType.GitHubCommit => "github_commit",
            ImportedEvidenceSourceType.GitHubRelease => "github_release",
            ImportedEvidenceSourceType.GitHubDiscussion => "github_discussion",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown imported evidence source type.")
        };

    public static bool TryParseSchemaValue(string? value, out ImportedEvidenceSourceType sourceType)
    {
        sourceType = ImportedEvidenceSourceType.LocalGitCommit;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "local_git_commit":
                sourceType = ImportedEvidenceSourceType.LocalGitCommit;
                return true;
            case "local_git_branch":
                sourceType = ImportedEvidenceSourceType.LocalGitBranch;
                return true;
            case "local_git_tag":
                sourceType = ImportedEvidenceSourceType.LocalGitTag;
                return true;
            case "local_git_changelog_signal":
                sourceType = ImportedEvidenceSourceType.LocalGitChangelogSignal;
                return true;
            case "github_issue":
                sourceType = ImportedEvidenceSourceType.GitHubIssue;
                return true;
            case "github_pull_request":
                sourceType = ImportedEvidenceSourceType.GitHubPullRequest;
                return true;
            case "github_review":
                sourceType = ImportedEvidenceSourceType.GitHubReview;
                return true;
            case "github_review_comment":
                sourceType = ImportedEvidenceSourceType.GitHubReviewComment;
                return true;
            case "github_commit":
                sourceType = ImportedEvidenceSourceType.GitHubCommit;
                return true;
            case "github_release":
                sourceType = ImportedEvidenceSourceType.GitHubRelease;
                return true;
            case "github_discussion":
                sourceType = ImportedEvidenceSourceType.GitHubDiscussion;
                return true;
            default:
                return false;
        }
    }
}
