namespace Memora.Index.Rebuild;

public enum IndexRebuildStatus
{
    Succeeded,
    Partial,
    Failed
}

public sealed record IndexRebuildResult(
    int ProjectCount,
    int ArtifactCount,
    int RevisionCount,
    int RelationshipCount,
    IReadOnlyList<IndexRebuildDiagnostic> Diagnostics,
    int FilesystemProjectCount,
    int FilesystemArtifactFileCount,
    IndexRebuildStatus Status)
{
    public bool Success => Status == IndexRebuildStatus.Succeeded;

    public string Summary =>
        Status switch
        {
            IndexRebuildStatus.Succeeded =>
                $"Rebuilt derived SQLite index from filesystem truth: {ProjectCount} project(s), {ArtifactCount} artifact(s), {RevisionCount} revision(s), {RelationshipCount} relationship(s).",
            IndexRebuildStatus.Partial =>
                $"Partially rebuilt derived SQLite index from filesystem truth with {Diagnostics.Count} diagnostic(s): {ProjectCount} project(s), {ArtifactCount} artifact(s), {RevisionCount} revision(s), {RelationshipCount} relationship(s).",
            _ =>
                $"Rebuild failed with {Diagnostics.Count} diagnostic(s). Filesystem truth was scanned ({FilesystemProjectCount} project(s), {FilesystemArtifactFileCount} artifact file(s)); existing derived SQLite index rows were preserved."
        };
}
