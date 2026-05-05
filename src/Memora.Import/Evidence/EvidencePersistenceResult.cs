using Memora.Core.Import;

namespace Memora.Import.Evidence;

public sealed record EvidencePersistenceResult(
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<string> WrittenPaths)
{
    public IReadOnlyList<string> WrittenPaths { get; } =
        WrittenPaths?.ToArray() ?? throw new ArgumentNullException(nameof(WrittenPaths));
}

public interface IImportedEvidenceStore
{
    EvidencePersistenceResult Save(ProjectEvidenceWriteRequest request);

    IReadOnlyList<ImportedEvidenceRecord> ReadAll(string workspaceRootPath);
}

public sealed record ProjectEvidenceWriteRequest(
    string WorkspaceRootPath,
    IReadOnlyList<ImportedEvidenceRecord> Records)
{
    public string WorkspaceRootPath { get; } = string.IsNullOrWhiteSpace(WorkspaceRootPath)
        ? throw new ArgumentException("Workspace root path is required.", nameof(WorkspaceRootPath))
        : Path.GetFullPath(WorkspaceRootPath);

    public IReadOnlyList<ImportedEvidenceRecord> Records { get; } =
        Records?.ToArray() ?? throw new ArgumentNullException(nameof(Records));
}
