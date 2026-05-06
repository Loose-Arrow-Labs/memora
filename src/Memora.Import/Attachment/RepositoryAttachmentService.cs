using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memora.Core.Import;
using Memora.Core.Projects;
using Memora.Import.Git;
using Memora.Storage.Workspaces;

namespace Memora.Import.Attachment;

public sealed class RepositoryAttachmentService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery;
    private readonly IGitRepositoryInspector _gitRepositoryInspector;

    public RepositoryAttachmentService(string workspacesRootPath)
        : this(workspacesRootPath, new WorkspaceDiscovery(), new ProcessGitRepositoryInspector())
    {
    }

    public RepositoryAttachmentService(
        string workspacesRootPath,
        WorkspaceDiscovery workspaceDiscovery,
        IGitRepositoryInspector gitRepositoryInspector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);

        _workspacesRootPath = Path.GetFullPath(workspacesRootPath);
        _workspaceDiscovery = workspaceDiscovery ?? throw new ArgumentNullException(nameof(workspaceDiscovery));
        _gitRepositoryInspector = gitRepositoryInspector ?? throw new ArgumentNullException(nameof(gitRepositoryInspector));
    }

    public RepositoryAttachmentResult Attach(RepositoryAttachmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspace = TryFindWorkspace(request.ProjectId, out var workspaceError);
        if (workspace is null)
        {
            return RepositoryAttachmentResult.Failed(workspaceError!);
        }

        var attachmentResult = request.Kind switch
        {
            RepositoryAttachmentKind.LocalGit => BuildLocalGitAttachment(request, workspace),
            RepositoryAttachmentKind.GitHub => BuildGitHubAttachment(request, workspace),
            _ => RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError(
                    "attachment.kind.unsupported",
                    $"Repository attachment kind '{request.Kind}' is not supported.",
                    "kind"))
        };

        if (!attachmentResult.IsSuccess || attachmentResult.Attachment is null)
        {
            return attachmentResult;
        }

        if (IsDuplicate(workspace.Metadata.RepositoryAttachments, attachmentResult.Attachment))
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError(
                    "attachment.duplicate",
                    $"Repository '{attachmentResult.Attachment.RepositoryIdentity}' is already attached to project '{workspace.ProjectId}'.",
                    "repository_identity"));
        }

        var updatedMetadata = new ProjectMetadata(
            workspace.ProjectId,
            workspace.Metadata.Name,
            workspace.Metadata.Status,
            workspace.Metadata.RepositoryAttachments.Concat([attachmentResult.Attachment]).ToArray());

        SaveMetadata(workspace.ProjectMetadataPath, updatedMetadata);
        return attachmentResult;
    }

    private ProjectWorkspace? TryFindWorkspace(string projectId, out RepositoryAttachmentError? error)
    {
        if (!Directory.Exists(_workspacesRootPath))
        {
            error = new RepositoryAttachmentError(
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
                error = new RepositoryAttachmentError(
                    "workspace.not_found",
                    $"Project workspace '{projectId}' was not found.",
                    "project_id");
                return null;
            }

            error = null;
            return workspace;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = new RepositoryAttachmentError(
                "workspace.invalid",
                $"Workspace metadata could not be loaded: {exception.Message}",
                "workspace");
            return null;
        }
    }

    private RepositoryAttachmentResult BuildLocalGitAttachment(RepositoryAttachmentRequest request, ProjectWorkspace workspace)
    {
        if (string.IsNullOrWhiteSpace(request.LocalPath))
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError("attachment.repo.missing", "Local repository path is required.", "local_path"));
        }

        if (!Directory.Exists(request.LocalPath))
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError(
                    "attachment.repo.missing",
                    $"Repository path '{request.LocalPath}' was not found.",
                    "local_path"));
        }

        var inspectionResult = _gitRepositoryInspector.Inspect(request.LocalPath);
        if (!inspectionResult.IsSuccess || inspectionResult.Inspection is null)
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError(
                    inspectionResult.ErrorCode ?? "attachment.git_command.failed",
                    inspectionResult.ErrorMessage ?? "Git repository inspection failed.",
                    "local_path"));
        }

        var inspection = inspectionResult.Inspection;
        var normalizedRootPath = Path.GetFullPath(inspection.WorkingTreeRootPath);
        var identity = $"local:{NormalizePathIdentity(normalizedRootPath)}";

        return RepositoryAttachmentResult.Succeeded(
            new ProjectRepositoryAttachment(
                CreateAttachmentId(workspace.ProjectId, RepositoryAttachmentKind.LocalGit, identity),
                workspace.ProjectId,
                RepositoryAttachmentKind.LocalGit,
                identity,
                normalizedRootPath,
                inspection.OriginUrl,
                inspection.DefaultBranch,
                inspection.OriginRemoteName,
                inspection.OriginUrl,
                DateTimeOffset.UtcNow));
    }

    private RepositoryAttachmentResult BuildGitHubAttachment(RepositoryAttachmentRequest request, ProjectWorkspace workspace)
    {
        if (string.IsNullOrWhiteSpace(request.RemoteUrl))
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError("attachment.remote.missing", "GitHub remote URL is required.", "remote_url"));
        }

        if (!TryNormalizeGitHubRemote(request.RemoteUrl, out var normalizedRemoteUrl))
        {
            return RepositoryAttachmentResult.Failed(
                new RepositoryAttachmentError(
                    "attachment.remote.unsupported",
                    "Only GitHub repository remotes are supported for GitHub attachments.",
                    "remote_url"));
        }

        var defaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch)
            ? "main"
            : request.DefaultBranch.Trim();
        var identity = $"github:{normalizedRemoteUrl}";

        return RepositoryAttachmentResult.Succeeded(
            new ProjectRepositoryAttachment(
                CreateAttachmentId(workspace.ProjectId, RepositoryAttachmentKind.GitHub, identity),
                workspace.ProjectId,
                RepositoryAttachmentKind.GitHub,
                identity,
                null,
                normalizedRemoteUrl,
                defaultBranch,
                "origin",
                normalizedRemoteUrl,
                DateTimeOffset.UtcNow));
    }

    private static bool IsDuplicate(
        IReadOnlyList<ProjectRepositoryAttachment> existingAttachments,
        ProjectRepositoryAttachment candidate) =>
        existingAttachments.Any(existing =>
            existing.Kind == candidate.Kind &&
            string.Equals(existing.RepositoryIdentity, candidate.RepositoryIdentity, StringComparison.Ordinal));

    private static string CreateAttachmentId(string projectId, RepositoryAttachmentKind kind, string identity)
    {
        var input = $"{projectId}\n{kind.ToSchemaValue()}\n{identity}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"ATT-{Convert.ToHexString(hash)[..12]}";
    }

    private static string NormalizePathIdentity(string path) =>
        path
            .Trim()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');

    private static bool TryNormalizeGitHubRemote(string remoteUrl, out string normalizedRemoteUrl)
    {
        normalizedRemoteUrl = string.Empty;
        var trimmed = remoteUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.AbsolutePath.Trim('/');
            return TryBuildGitHubHttpsUrl(path, out normalizedRemoteUrl);
        }

        const string sshPrefix = "git@github.com:";
        if (trimmed.StartsWith(sshPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = trimmed[sshPrefix.Length..];
            return TryBuildGitHubHttpsUrl(path, out normalizedRemoteUrl);
        }

        return false;
    }

    private static bool TryBuildGitHubHttpsUrl(string path, out string normalizedRemoteUrl)
    {
        normalizedRemoteUrl = string.Empty;
        var normalizedPath = path.Trim('/');
        if (normalizedPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath[..^4];
        }

        var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        normalizedRemoteUrl = $"https://github.com/{parts[0]}/{parts[1]}.git";
        return true;
    }

    private static void SaveMetadata(string metadataPath, ProjectMetadata metadata)
    {
        var payload = new
        {
            projectId = metadata.ProjectId,
            name = metadata.Name,
            status = metadata.Status,
            repositoryAttachments = metadata.RepositoryAttachments
                .OrderBy(attachment => attachment.AttachmentId, StringComparer.Ordinal)
                .Select(attachment => new
                {
                    attachmentId = attachment.AttachmentId,
                    projectId = attachment.ProjectId,
                    kind = attachment.Kind.ToSchemaValue(),
                    repositoryIdentity = attachment.RepositoryIdentity,
                    localPath = attachment.LocalPath,
                    remoteUrl = attachment.RemoteUrl,
                    defaultBranch = attachment.DefaultBranch,
                    originRemoteName = attachment.OriginRemoteName,
                    originUrl = attachment.OriginUrl,
                    attachedAtUtc = attachment.AttachedAtUtc.ToString("O")
                })
                .ToArray()
        };

        File.WriteAllText(metadataPath, JsonSerializer.Serialize(payload, MetadataJsonOptions));
    }
}
