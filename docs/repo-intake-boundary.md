# Existing-Repo Intake Boundary

This document defines which repository inputs participate in Memora's
existing-repo understanding scan and which are excluded from deterministic
intake.

## Purpose

The intake boundary controls what the deterministic repo scan reads when
building project understanding from an existing codebase. A well-defined
boundary keeps scan behavior predictable, avoids noisy or unstable inputs, and
ensures that inferred candidates stay grounded in meaningful signal.

## Included Paths

The following input categories are in scope for repo understanding intake:

| Category | Examples |
|----------|---------|
| Source files | `*.cs`, `*.ts`, `*.py`, `*.go`, `*.java`, `*.rs`, `*.rb`, `*.cpp` |
| Configuration files | `*.json`, `*.yaml`, `*.yml`, `*.toml`, `*.ini`, `*.env.example` |
| Project / build files | `*.csproj`, `*.sln`, `*.gradle`, `package.json`, `Cargo.toml`, `go.mod` |
| Documentation files | `*.md`, `*.txt`, `*.rst`, `*.adoc` |
| CI / pipeline files | `.github/workflows/*.yml`, `Jenkinsfile`, `.gitlab-ci.yml`, `Makefile` |
| Agent instruction files | `AGENTS.md`, `CLAUDE.md`, `CONTRIBUTING.md`, `CODEOWNERS` |
| Lock files (as signals) | `package-lock.json`, `yarn.lock`, `Cargo.lock`, `go.sum` |

Included files are read for structural signals (type, location, name patterns)
and metadata (size class, extension, top-level path). Full file bodies are not
read by the deterministic scan in v1.

## Excluded Paths

The following are explicitly excluded from intake:

| Category | Examples |
|----------|---------|
| Build artifacts | `bin/`, `obj/`, `dist/`, `out/`, `build/`, `target/`, `.gradle/` |
| Dependency stores | `node_modules/`, `vendor/`, `packages/`, `.cargo/`, `__pycache__/` |
| Generated code | `*.g.cs`, `*.generated.*`, any path containing `/generated/` |
| IDE state | `.vs/`, `.idea/`, `.vscode/settings.json` |
| Binary files | images, videos, compiled objects, archives (`.zip`, `.tar`, `.dll`, `.exe`) |
| Secrets and credentials | `.env` (actual env files, not `.env.example`), `*.pem`, `*.key`, `*.p12` |
| Memora workspace files | `evidence/`, `canonical/`, `drafts/`, `summaries/`, `indexes/` |
| Hidden system folders | `.git/` (Git history is read via Git commands, not filesystem scan) |

## Boundary Rules

1. **Determinism**: the same repository at the same state must produce the same
   inventory on every scan. Traversal order must be stable (sort by path).

2. **No body reads in v1**: the intake boundary controls which files are
   *inventoried*. Full file content is not read by the deterministic scan in
   v1. Content-level signals come from evidence import (Git history, GitHub
   metadata), not from file system reads of source bodies.

3. **Non-canonical outputs**: all understanding inferred from intake is
   non-canonical until reviewed and approved. The intake boundary defines what
   is observed, not what becomes project truth.

4. **Configurable exclusions**: the default exclusion list should be
   authoritative for common layouts. Repo-specific overrides are a follow-up
   concern outside v1 scope.

5. **Large-repo stability**: the boundary must not perform unbounded recursive
   reads. Excluded directories must be pruned before recursion (not filtered
   after). This keeps scan time and memory stable as repo size grows.

## Validation

Design review is the acceptance vehicle for this document. The boundary should
be validated against at least one real repository layout before M12-02
implementation begins.

## Relationship To M12-02

M12-02 (Build deterministic repo scan) implements the traversal logic that
enforces this boundary. Any ambiguity in this document should be resolved before
M12-02 implementation so the scan is built against a stable contract.

## Non-Canonical Reminder

All outputs of the existing-repo scan are non-canonical by default. The scan
produces evidence and candidates. Only human review and explicit approval
through Memora's governed lifecycle can promote understanding to canonical
project memory.
