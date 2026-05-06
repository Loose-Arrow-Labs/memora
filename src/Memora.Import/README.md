# Memora.Import

## Purpose
Coordinates first-run import use cases across filesystem workspaces, source
repository inspection, evidence intake, safety filtering, and readiness output.

## Responsibilities
- repository attachment services
- local Git and GitHub evidence intake boundaries
- import-mode-aware placement decisions
- deterministic first-run candidate and readiness generation

## Does NOT contain
- canonical artifact lifecycle bypasses
- provider-specific business rules in domain models
- MCP, OpenAPI, or UI protocol handling

## Current Scope
- repository attachment records are persisted in app-managed workspace metadata
- source repositories remain evidence sources instead of Memora workspace roots
- local Git evidence import stores commits, branches, tags, changed-file
  summaries, and changelog or release signals as idempotent workspace evidence
- GitHub evidence import normalizes issues, pull requests, reviews, review
  comments, commits, releases, and available discussion metadata into the same
  evidence store
- later M10 slices build safety filtering, candidate memory, and readiness
  reporting on this shared layer
