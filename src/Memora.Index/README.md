# Memora.Index

## Purpose
Maintains the derived SQLite index for Memora artifacts.

## Responsibilities
- SQLite schema
- indexing metadata
- indexing links
- rebuild from files
- relationship indexing
- traceability query support

## Does NOT contain
- canonical truth decisions
- provider-specific integrations

## Key Areas

- `Schema/SqliteIndexSchema.cs`: schema creation and rebuildable index shape
- `Rebuild/SqliteIndexRebuilder.cs`: rebuilds the index from filesystem truth
- `Rebuild/IndexRebuildResult.cs`: diagnostics and rebuild summary models
- `Relationships/ArtifactRelationshipIndex.cs`: persists and reads approved artifact relationships
- `Traceability/TraceabilityQueryService.cs`: builds direct, dependency, and impact paths from approved relationships

## Rebuild Diagnostics

Failed rebuilds report the filesystem project and artifact-file counts that
were scanned, identify the file/path that caused each diagnostic, and make clear
that SQLite is a derived index. When diagnostics are present, derived index rows
are cleared and not repopulated until the filesystem issues are fixed.
