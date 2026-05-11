# Memora.Storage

## Purpose
Handles artifact file parsing and persistence.

## Responsibilities
- markdown + frontmatter parsing
- workspace file layout
- canonical and draft persistence
- revision file handling

## Does NOT contain
- lifecycle rules
- API logic
- ranking logic

## Key Areas

- `Parsing/ArtifactMarkdownParser.cs`: main markdown-to-artifact entry point
- `Persistence/ArtifactFileStore.cs`: saves canonical and draft revisions
- `Persistence/ApprovalDecisionFilePersistence.cs`: applies approved and
  superseded approval outputs as one rollback-capable filesystem transaction
- `Persistence/ArtifactMarkdownWriter.cs`: writes structured markdown output
- `Workspaces/WorkspaceDiscovery.cs`: discovers project workspaces from `project.json`
