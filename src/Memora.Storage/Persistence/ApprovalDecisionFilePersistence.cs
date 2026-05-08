using Memora.Core.Artifacts;
using Memora.Storage.Workspaces;

namespace Memora.Storage.Persistence;

public sealed class ApprovalDecisionFilePersistence
{
    private readonly ArtifactFileStore _artifactFileStore;

    public ApprovalDecisionFilePersistence()
        : this(new ArtifactFileStore())
    {
    }

    public ApprovalDecisionFilePersistence(ArtifactFileStore artifactFileStore)
    {
        _artifactFileStore = artifactFileStore ?? throw new ArgumentNullException(nameof(artifactFileStore));
    }

    public ApprovalDecisionPersistenceResult SaveApproved(
        ProjectWorkspace workspace,
        string pendingFilePath,
        ArtifactDocument approvedArtifact,
        ArtifactDocument? supersededArtifact = null,
        string? currentApprovedFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(pendingFilePath);
        ArgumentNullException.ThrowIfNull(approvedArtifact);

        var pendingBackupPath = CreateBackupPath(pendingFilePath, ".approving");
        var currentApprovedBackupPath = currentApprovedFilePath is null
            ? null
            : CreateBackupPath(currentApprovedFilePath, ".superseding");
        string? approvedPath = null;
        string? supersededPath = null;

        try
        {
            File.Move(pendingFilePath, pendingBackupPath);

            if (currentApprovedFilePath is not null && currentApprovedBackupPath is not null)
            {
                File.Move(currentApprovedFilePath, currentApprovedBackupPath);
            }

            if (supersededArtifact is not null)
            {
                supersededPath = _artifactFileStore.Save(workspace, supersededArtifact);
            }

            approvedPath = _artifactFileStore.Save(workspace, approvedArtifact);
            DeleteIfExists(pendingBackupPath);
            DeleteIfExists(currentApprovedBackupPath);

            return ApprovalDecisionPersistenceResult.Success(approvedPath, supersededPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            RollBackApprovalPersistence(
                pendingFilePath,
                pendingBackupPath,
                currentApprovedFilePath,
                currentApprovedBackupPath,
                approvedPath,
                supersededPath);

            return ApprovalDecisionPersistenceResult.Failed(exception.Message);
        }
    }

    private static string CreateBackupPath(string path, string suffix)
    {
        var backupPath = path + suffix;
        if (File.Exists(backupPath))
        {
            throw new IOException($"Approval backup file '{backupPath}' already exists.");
        }

        return backupPath;
    }

    private static void RollBackApprovalPersistence(
        string pendingPath,
        string pendingBackupPath,
        string? currentApprovedPath,
        string? currentApprovedBackupPath,
        string? approvedPath,
        string? supersededPath)
    {
        DeleteIfExists(approvedPath);
        DeleteIfExists(supersededPath);
        RestoreIfMissing(currentApprovedBackupPath, currentApprovedPath);
        RestoreIfMissing(pendingBackupPath, pendingPath);
    }

    private static void RestoreIfMissing(string? backupPath, string? restorePath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(restorePath))
        {
            return;
        }

        if (File.Exists(backupPath) && !File.Exists(restorePath))
        {
            File.Move(backupPath, restorePath);
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public sealed class ApprovalDecisionPersistenceResult
{
    private ApprovalDecisionPersistenceResult(
        bool isSuccess,
        string? approvedPath,
        string? supersededPath,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        ApprovedPath = approvedPath;
        SupersededPath = supersededPath;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? ApprovedPath { get; }

    public string? SupersededPath { get; }

    public string? ErrorMessage { get; }

    public static ApprovalDecisionPersistenceResult Success(string approvedPath, string? supersededPath) =>
        new(true, approvedPath, supersededPath, null);

    public static ApprovalDecisionPersistenceResult Failed(string errorMessage) =>
        new(false, null, null, errorMessage);
}
