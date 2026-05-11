# Memora.Import

## Purpose
Coordinates first-run import use cases across filesystem workspaces, source
repository inspection, evidence intake, safety filtering, and readiness output.

## Responsibilities
- repository attachment services
- local Git and GitHub evidence intake boundaries
- import-mode-aware placement decisions
- hybrid first-run candidate and readiness generation, with deterministic
  evidence-derived findings separated from inferred or advisory candidates

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
- import safety filtering applies defensive metadata redaction for a narrow set
  of common token formats (OpenAI keys, GitHub tokens, AWS access keys,
  credential assignments, and private key material) before local Git or GitHub
  evidence is persisted; this is not a complete secret scanner and does not
  cover all token formats
- first-run candidate memory and agent readiness reports generate the
  deterministic evidence-derived side of Memora's hybrid retrieval model, with
  provenance and review state
- later M10 slices add UI and protocol exposure on this shared layer
