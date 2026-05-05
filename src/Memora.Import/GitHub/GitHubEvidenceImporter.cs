using System.Security.Cryptography;
using System.Text;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Storage.Workspaces;

namespace Memora.Import.GitHub;

public sealed class GitHubEvidenceImporter
{
    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery;
    private readonly IGitHubEvidenceClient _client;
    private readonly IImportedEvidenceStore _evidenceStore;

    public GitHubEvidenceImporter(
        string workspacesRootPath,
        IGitHubEvidenceClient client)
        : this(
            workspacesRootPath,
            new WorkspaceDiscovery(),
            client,
            new FileBackedImportedEvidenceStore())
    {
    }

    public GitHubEvidenceImporter(
        string workspacesRootPath,
        WorkspaceDiscovery workspaceDiscovery,
        IGitHubEvidenceClient client,
        IImportedEvidenceStore evidenceStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);

        _workspacesRootPath = Path.GetFullPath(workspacesRootPath);
        _workspaceDiscovery = workspaceDiscovery ?? throw new ArgumentNullException(nameof(workspaceDiscovery));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
    }

    public GitHubEvidenceImportResult Import(GitHubEvidenceImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspace = TryFindWorkspace(request.ProjectId, out var workspaceDiagnostic);
        if (workspace is null)
        {
            return Failed(workspaceDiagnostic!);
        }

        var attachment = ResolveGitHubAttachment(workspace, request);
        if (attachment is null)
        {
            return Failed(
                GitHubImportDiagnostic.Error(
                    "github.attachment.missing",
                    $"Project '{workspace.ProjectId}' does not have a matching GitHub attachment.",
                    "attachment_id"));
        }

        if (string.IsNullOrWhiteSpace(attachment.RemoteUrl))
        {
            return Failed(
                GitHubImportDiagnostic.Error(
                    "github.remote.missing",
                    $"GitHub attachment '{attachment.AttachmentId}' does not include a remote URL.",
                    "remote_url"));
        }

        var clientResult = _client.Fetch(attachment.RemoteUrl, request.MaxItems);
        if (!clientResult.IsSuccess || clientResult.Snapshot is null)
        {
            return Failed(clientResult.Diagnostics.ToArray());
        }

        var importedAtUtc = DateTimeOffset.UtcNow;
        var trustState = ImportModeTrustPolicy.GetEvidenceTrustState(request.ImportMode);
        var records = BuildEvidenceRecords(workspace.ProjectId, attachment, clientResult.Snapshot, importedAtUtc, trustState);
        var persistence = _evidenceStore.Save(new ProjectEvidenceWriteRequest(workspace.RootPath, records));
        var diagnostics = clientResult.Diagnostics.ToList();
        diagnostics.Add(
            GitHubImportDiagnostic.Info(
                "github.import.completed",
                $"Imported {records.Count} GitHub evidence record(s). {persistence.CreatedCount} new, {persistence.ExistingCount} already present.",
                "evidence"));

        return new GitHubEvidenceImportResult(
            records,
            new GitHubEvidenceImportProgress(
                records.Count,
                persistence.CreatedCount,
                persistence.ExistingCount,
                clientResult.Snapshot.Issues.Count,
                clientResult.Snapshot.PullRequests.Count,
                clientResult.Snapshot.Reviews.Count,
                clientResult.Snapshot.ReviewComments.Count,
                clientResult.Snapshot.Commits.Count,
                clientResult.Snapshot.Releases.Count,
                clientResult.Snapshot.Discussions.Count),
            diagnostics);
    }

    private static IReadOnlyList<ImportedEvidenceRecord> BuildEvidenceRecords(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubEvidenceSnapshot snapshot,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState)
    {
        var records = new List<ImportedEvidenceRecord>();

        records.AddRange(snapshot.Issues
            .OrderBy(issue => issue.Number)
            .Select(issue => CreateIssueRecord(projectId, attachment, issue, importedAtUtc, trustState)));
        records.AddRange(snapshot.PullRequests
            .OrderBy(pullRequest => pullRequest.Number)
            .Select(pullRequest => CreatePullRequestRecord(projectId, attachment, pullRequest, importedAtUtc, trustState)));
        records.AddRange(snapshot.Reviews
            .OrderBy(review => review.PullRequestNumber)
            .ThenBy(review => review.ReviewId, StringComparer.Ordinal)
            .Select(review => CreateReviewRecord(projectId, attachment, review, importedAtUtc, trustState)));
        records.AddRange(snapshot.ReviewComments
            .OrderBy(comment => comment.PullRequestNumber)
            .ThenBy(comment => comment.CommentId, StringComparer.Ordinal)
            .Select(comment => CreateReviewCommentRecord(projectId, attachment, comment, importedAtUtc, trustState)));
        records.AddRange(snapshot.Commits
            .OrderByDescending(commit => commit.AuthoredAtUtc)
            .ThenBy(commit => commit.Sha, StringComparer.Ordinal)
            .Select(commit => CreateCommitRecord(projectId, attachment, commit, importedAtUtc, trustState)));
        records.AddRange(snapshot.Releases
            .OrderBy(release => release.TagName, StringComparer.Ordinal)
            .Select(release => CreateReleaseRecord(projectId, attachment, release, importedAtUtc, trustState)));
        records.AddRange(snapshot.Discussions
            .OrderBy(discussion => discussion.DiscussionId, StringComparer.Ordinal)
            .Select(discussion => CreateDiscussionRecord(projectId, attachment, discussion, importedAtUtc, trustState)));

        return records;
    }

    private static ImportedEvidenceRecord CreateIssueRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubIssueEvidence issue,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubIssue, attachment.RepositoryIdentity, issue.Number.ToString("D")),
            projectId,
            ImportedEvidenceSourceType.GitHubIssue,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            issue.Number.ToString("D"),
            $"Issue #{issue.Number}: {issue.Title}",
            $"GitHub issue #{issue.Number} is {issue.State}.",
            issue.UpdatedAtUtc,
            importedAtUtc,
            issue.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["number"] = issue.Number.ToString("D"),
                ["url"] = issue.Url,
                ["state"] = issue.State,
                ["createdAtUtc"] = issue.CreatedAtUtc.ToString("O"),
                ["updatedAtUtc"] = issue.UpdatedAtUtc.ToString("O")
            });

    private static ImportedEvidenceRecord CreatePullRequestRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubPullRequestEvidence pullRequest,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubPullRequest, attachment.RepositoryIdentity, pullRequest.Number.ToString("D")),
            projectId,
            ImportedEvidenceSourceType.GitHubPullRequest,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            pullRequest.Number.ToString("D"),
            $"PR #{pullRequest.Number}: {pullRequest.Title}",
            $"GitHub pull request #{pullRequest.Number} is {pullRequest.State}.",
            pullRequest.UpdatedAtUtc,
            importedAtUtc,
            pullRequest.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["number"] = pullRequest.Number.ToString("D"),
                ["url"] = pullRequest.Url,
                ["state"] = pullRequest.State,
                ["mergeCommitSha"] = pullRequest.MergeCommitSha ?? string.Empty,
                ["createdAtUtc"] = pullRequest.CreatedAtUtc.ToString("O"),
                ["updatedAtUtc"] = pullRequest.UpdatedAtUtc.ToString("O")
            });

    private static ImportedEvidenceRecord CreateReviewRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubReviewEvidence review,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubReview, attachment.RepositoryIdentity, $"{review.PullRequestNumber}:{review.ReviewId}"),
            projectId,
            ImportedEvidenceSourceType.GitHubReview,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            review.ReviewId,
            $"PR #{review.PullRequestNumber} review {review.ReviewId}",
            $"GitHub review {review.ReviewId} on PR #{review.PullRequestNumber} is {review.State}.",
            review.SubmittedAtUtc,
            importedAtUtc,
            review.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pullRequestNumber"] = review.PullRequestNumber.ToString("D"),
                ["reviewId"] = review.ReviewId,
                ["url"] = review.Url,
                ["state"] = review.State,
                ["author"] = review.Author ?? string.Empty,
                ["submittedAtUtc"] = review.SubmittedAtUtc.ToString("O")
            });

    private static ImportedEvidenceRecord CreateReviewCommentRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubReviewCommentEvidence comment,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubReviewComment, attachment.RepositoryIdentity, $"{comment.PullRequestNumber}:{comment.CommentId}"),
            projectId,
            ImportedEvidenceSourceType.GitHubReviewComment,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            comment.CommentId,
            $"PR #{comment.PullRequestNumber} review comment {comment.CommentId}",
            $"GitHub review comment {comment.CommentId} on PR #{comment.PullRequestNumber}.",
            comment.UpdatedAtUtc,
            importedAtUtc,
            comment.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pullRequestNumber"] = comment.PullRequestNumber.ToString("D"),
                ["commentId"] = comment.CommentId,
                ["url"] = comment.Url,
                ["path"] = comment.Path ?? string.Empty,
                ["createdAtUtc"] = comment.CreatedAtUtc.ToString("O"),
                ["updatedAtUtc"] = comment.UpdatedAtUtc.ToString("O")
            });

    private static ImportedEvidenceRecord CreateCommitRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubCommitEvidence commit,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubCommit, attachment.RepositoryIdentity, commit.Sha),
            projectId,
            ImportedEvidenceSourceType.GitHubCommit,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            commit.Sha,
            $"GitHub commit {commit.Sha[..Math.Min(7, commit.Sha.Length)]}",
            commit.Message,
            commit.AuthoredAtUtc,
            importedAtUtc,
            commit.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sha"] = commit.Sha,
                ["url"] = commit.Url,
                ["author"] = commit.Author ?? string.Empty,
                ["authoredAtUtc"] = commit.AuthoredAtUtc.ToString("O")
            });

    private static ImportedEvidenceRecord CreateReleaseRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubReleaseEvidence release,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubRelease, attachment.RepositoryIdentity, release.ReleaseId),
            projectId,
            ImportedEvidenceSourceType.GitHubRelease,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            release.ReleaseId,
            $"Release {release.Name}",
            $"GitHub release {release.Name} for tag {release.TagName}.",
            release.PublishedAtUtc ?? importedAtUtc,
            importedAtUtc,
            release.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["releaseId"] = release.ReleaseId,
                ["url"] = release.Url,
                ["tagName"] = release.TagName,
                ["publishedAtUtc"] = release.PublishedAtUtc?.ToString("O") ?? string.Empty
            });

    private static ImportedEvidenceRecord CreateDiscussionRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        GitHubDiscussionEvidence discussion,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.GitHubDiscussion, attachment.RepositoryIdentity, discussion.DiscussionId),
            projectId,
            ImportedEvidenceSourceType.GitHubDiscussion,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            discussion.DiscussionId,
            $"Discussion {discussion.Title}",
            $"GitHub discussion {discussion.DiscussionId}.",
            discussion.UpdatedAtUtc,
            importedAtUtc,
            discussion.Url,
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["discussionId"] = discussion.DiscussionId,
                ["url"] = discussion.Url,
                ["createdAtUtc"] = discussion.CreatedAtUtc.ToString("O"),
                ["updatedAtUtc"] = discussion.UpdatedAtUtc.ToString("O")
            });

    private ProjectWorkspace? TryFindWorkspace(string projectId, out GitHubImportDiagnostic? diagnostic)
    {
        if (!Directory.Exists(_workspacesRootPath))
        {
            diagnostic = GitHubImportDiagnostic.Error(
                "workspace.root.missing",
                $"Workspace root '{_workspacesRootPath}' was not found.",
                "workspaces_root");
            return null;
        }

        try
        {
            var workspace = _workspaceDiscovery
                .Discover(_workspacesRootPath)
                .SingleOrDefault(workspace => string.Equals(workspace.ProjectId, projectId, StringComparison.Ordinal));

            if (workspace is null)
            {
                diagnostic = GitHubImportDiagnostic.Error(
                    "workspace.not_found",
                    $"Project workspace '{projectId}' was not found.",
                    "project_id");
                return null;
            }

            diagnostic = null;
            return workspace;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            diagnostic = GitHubImportDiagnostic.Error(
                "workspace.invalid",
                $"Workspace metadata could not be loaded: {exception.Message}",
                "workspace");
            return null;
        }
    }

    private static ProjectRepositoryAttachment? ResolveGitHubAttachment(
        ProjectWorkspace workspace,
        GitHubEvidenceImportRequest request)
    {
        var githubAttachments = workspace.Metadata.RepositoryAttachments
            .Where(attachment => attachment.Kind == RepositoryAttachmentKind.GitHub)
            .OrderBy(attachment => attachment.AttachmentId, StringComparer.Ordinal)
            .ToArray();

        return string.IsNullOrWhiteSpace(request.AttachmentId)
            ? githubAttachments.FirstOrDefault()
            : githubAttachments.SingleOrDefault(attachment =>
                string.Equals(attachment.AttachmentId, request.AttachmentId, StringComparison.Ordinal));
    }

    private static string CreateStableId(
        string projectId,
        ImportedEvidenceSourceType sourceType,
        string repositoryIdentity,
        string sourceReference)
    {
        var input = $"{projectId}\n{sourceType.ToSchemaValue()}\n{repositoryIdentity}\n{sourceReference}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"EVD-{Convert.ToHexString(hash)[..16]}";
    }

    private static GitHubEvidenceImportResult Failed(params GitHubImportDiagnostic[] diagnostics) =>
        new(
            [],
            new GitHubEvidenceImportProgress(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            diagnostics);
}
