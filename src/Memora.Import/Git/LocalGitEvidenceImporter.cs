using System.Security.Cryptography;
using System.Text;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Prerequisites;
using Memora.Import.Safety;
using Memora.Storage.Workspaces;

namespace Memora.Import.Git;

public sealed class LocalGitEvidenceImporter
{
    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery;
    private readonly ILocalGitHistoryReader _historyReader;
    private readonly IImportedEvidenceStore _evidenceStore;
    private readonly ImportContentSafetyFilter _safetyFilter;
    private readonly RuntimePrerequisiteChecker _prerequisiteChecker;

    public LocalGitEvidenceImporter(string workspacesRootPath)
        : this(
            workspacesRootPath,
            new WorkspaceDiscovery(),
            new ProcessLocalGitHistoryReader(),
            new FileBackedImportedEvidenceStore(),
            new ImportContentSafetyFilter())
    {
    }

    public LocalGitEvidenceImporter(
        string workspacesRootPath,
        WorkspaceDiscovery workspaceDiscovery,
        ILocalGitHistoryReader historyReader,
        IImportedEvidenceStore evidenceStore,
        ImportContentSafetyFilter? safetyFilter = null,
        RuntimePrerequisiteChecker? prerequisiteChecker = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);

        _workspacesRootPath = Path.GetFullPath(workspacesRootPath);
        _workspaceDiscovery = workspaceDiscovery ?? throw new ArgumentNullException(nameof(workspaceDiscovery));
        _historyReader = historyReader ?? throw new ArgumentNullException(nameof(historyReader));
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _safetyFilter = safetyFilter ?? new ImportContentSafetyFilter();
        _prerequisiteChecker = prerequisiteChecker ?? new RuntimePrerequisiteChecker();
    }

    public LocalGitEvidenceImportResult Import(LocalGitEvidenceImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prerequisites = _prerequisiteChecker.Check();
        if (!prerequisites.IsReady)
        {
            return Failed(
                LocalGitImportDiagnostic.Error(
                    prerequisites.Diagnostics[0].Code,
                    prerequisites.Diagnostics[0].Message,
                    prerequisites.Diagnostics[0].Tool));
        }

        var workspace = TryFindWorkspace(request.ProjectId, out var workspaceDiagnostic);
        if (workspace is null)
        {
            return Failed(workspaceDiagnostic!);
        }

        var attachment = ResolveLocalGitAttachment(workspace, request);
        if (attachment is null)
        {
            return Failed(
                LocalGitImportDiagnostic.Error(
                    "local_git.attachment.missing",
                    $"Project '{workspace.ProjectId}' does not have a matching local Git attachment.",
                    "attachment_id"));
        }

        if (string.IsNullOrWhiteSpace(attachment.LocalPath) || !Directory.Exists(attachment.LocalPath))
        {
            return Failed(
                LocalGitImportDiagnostic.Error(
                    "local_git.repo.unsupported",
                    $"Attached repository path '{attachment.LocalPath}' was not found.",
                    "local_path"));
        }

        var historyResult = _historyReader.Read(attachment.LocalPath, request.MaxCommits);
        if (!historyResult.IsSuccess || historyResult.Snapshot is null)
        {
            return Failed(historyResult.Diagnostics.ToArray());
        }

        var importedAtUtc = DateTimeOffset.UtcNow;
        var trustState = ImportModeTrustPolicy.GetEvidenceTrustState(request.ImportMode);
        var records = BuildEvidenceRecords(workspace.ProjectId, attachment, historyResult.Snapshot, importedAtUtc, trustState);
        var diagnostics = historyResult.Diagnostics.ToList();
        var safetyResult = _safetyFilter.Filter(records);
        diagnostics.AddRange(safetyResult.Diagnostics.Select(MapSafetyDiagnostic));

        var persistence = _evidenceStore.Save(new ProjectEvidenceWriteRequest(workspace.RootPath, safetyResult.Records));
        diagnostics.Add(
            LocalGitImportDiagnostic.Info(
                "local_git.import.completed",
                $"Imported {safetyResult.Records.Count} local Git evidence record(s). {persistence.CreatedCount} new, {persistence.ExistingCount} already present.",
                "evidence"));

        return new LocalGitEvidenceImportResult(
            safetyResult.Records,
            new LocalGitImportProgress(
                records.Count,
                persistence.CreatedCount,
                persistence.ExistingCount,
                historyResult.Snapshot.Commits.Count,
                historyResult.Snapshot.Branches.Count,
                historyResult.Snapshot.Tags.Count,
                historyResult.Snapshot.ChangelogSignals.Count),
            diagnostics);
    }

    private static IReadOnlyList<ImportedEvidenceRecord> BuildEvidenceRecords(
        string projectId,
        ProjectRepositoryAttachment attachment,
        LocalGitHistorySnapshot snapshot,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState)
    {
        var records = new List<ImportedEvidenceRecord>();

        records.AddRange(snapshot.Commits
            .OrderByDescending(commit => commit.CommittedAtUtc)
            .ThenBy(commit => commit.Sha, StringComparer.Ordinal)
            .Select(commit => CreateCommitRecord(projectId, attachment, commit, importedAtUtc, trustState)));
        records.AddRange(snapshot.Branches
            .OrderBy(branch => branch.Name, StringComparer.Ordinal)
            .Select(branch => CreateBranchRecord(projectId, attachment, branch, importedAtUtc, trustState)));
        records.AddRange(snapshot.Tags
            .OrderBy(tag => tag.Name, StringComparer.Ordinal)
            .Select(tag => CreateTagRecord(projectId, attachment, tag, importedAtUtc, trustState)));
        records.AddRange(snapshot.ChangelogSignals
            .OrderBy(signal => signal.Path, StringComparer.Ordinal)
            .Select(signal => CreateChangelogRecord(projectId, attachment, signal, importedAtUtc, trustState)));

        return records;
    }

    private static ImportedEvidenceRecord CreateCommitRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        LocalGitCommit commit,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.LocalGitCommit, attachment.RepositoryIdentity, commit.Sha),
            projectId,
            ImportedEvidenceSourceType.LocalGitCommit,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            commit.Sha,
            commit.Subject,
            $"{commit.Subject} ({commit.ChangedFiles.Count} changed file(s)).",
            commit.CommittedAtUtc,
            importedAtUtc,
            $"local git commit {commit.Sha}",
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authorName"] = commit.AuthorName,
                ["authorEmail"] = commit.AuthorEmail,
                ["changedFiles"] = string.Join("\n", commit.ChangedFiles)
            });

    private static ImportedEvidenceRecord CreateBranchRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        LocalGitBranch branch,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.LocalGitBranch, attachment.RepositoryIdentity, branch.Name),
            projectId,
            ImportedEvidenceSourceType.LocalGitBranch,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            branch.Name,
            $"Branch {branch.Name}",
            $"Branch {branch.Name} points at {branch.TargetSha}.",
            importedAtUtc,
            importedAtUtc,
            $"local git branch {branch.Name}",
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetSha"] = branch.TargetSha
            });

    private static ImportedEvidenceRecord CreateTagRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        LocalGitTag tag,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.LocalGitTag, attachment.RepositoryIdentity, tag.Name),
            projectId,
            ImportedEvidenceSourceType.LocalGitTag,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            tag.Name,
            $"Tag {tag.Name}",
            $"Tag {tag.Name} points at {tag.TargetSha}.",
            tag.TaggedAtUtc ?? importedAtUtc,
            importedAtUtc,
            $"local git tag {tag.Name}",
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetSha"] = tag.TargetSha
            });

    private static ImportedEvidenceRecord CreateChangelogRecord(
        string projectId,
        ProjectRepositoryAttachment attachment,
        LocalGitChangelogSignal signal,
        DateTimeOffset importedAtUtc,
        ImportedEvidenceTrustState trustState) =>
        new(
            CreateStableId(projectId, ImportedEvidenceSourceType.LocalGitChangelogSignal, attachment.RepositoryIdentity, signal.Path),
            projectId,
            ImportedEvidenceSourceType.LocalGitChangelogSignal,
            attachment.AttachmentId,
            attachment.RepositoryIdentity,
            signal.Path,
            $"Changelog signal {signal.Path}",
            signal.Summary,
            importedAtUtc,
            importedAtUtc,
            $"local repository file {signal.Path}",
            trustState,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = signal.Path
            });

    private ProjectWorkspace? TryFindWorkspace(string projectId, out LocalGitImportDiagnostic? diagnostic)
    {
        if (!Directory.Exists(_workspacesRootPath))
        {
            diagnostic = LocalGitImportDiagnostic.Error(
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
                diagnostic = LocalGitImportDiagnostic.Error(
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
            diagnostic = LocalGitImportDiagnostic.Error(
                "workspace.invalid",
                $"Workspace metadata could not be loaded: {exception.Message}",
                "workspace");
            return null;
        }
    }

    private static ProjectRepositoryAttachment? ResolveLocalGitAttachment(
        ProjectWorkspace workspace,
        LocalGitEvidenceImportRequest request)
    {
        var localGitAttachments = workspace.Metadata.RepositoryAttachments
            .Where(attachment => attachment.Kind == RepositoryAttachmentKind.LocalGit)
            .OrderBy(attachment => attachment.AttachmentId, StringComparer.Ordinal)
            .ToArray();

        return string.IsNullOrWhiteSpace(request.AttachmentId)
            ? localGitAttachments.FirstOrDefault()
            : localGitAttachments.SingleOrDefault(attachment =>
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

    private static LocalGitImportDiagnostic MapSafetyDiagnostic(ImportSafetyDiagnostic diagnostic) =>
        new(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Severity == ImportSafetyDiagnosticSeverity.Error
                ? LocalGitImportDiagnosticSeverity.Error
                : LocalGitImportDiagnosticSeverity.Warning,
            $"{diagnostic.StableEvidenceId}:{diagnostic.Field}");

    private static LocalGitEvidenceImportResult Failed(params LocalGitImportDiagnostic[] diagnostics) =>
        new(
            [],
            new LocalGitImportProgress(0, 0, 0, 0, 0, 0, 0),
            diagnostics);
}
