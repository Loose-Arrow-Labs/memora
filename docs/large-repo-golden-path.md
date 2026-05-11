# Large-Repo Golden Path Validation

## Purpose

This document records the results of exercising the full M12 repo-understanding workflow on a real, non-trivial repository. The goal is to honestly assess what the current MVP delivers, what it misses, and what needs revision before broader rollout.

**Reference repository:** `alucero270/memora` (this repository)  
**Repository characteristics:** ~250 source files, C#/.NET 10, multi-project solution, CI/CD on GitHub Actions, active GitHub history, 250+ test suite, 50+ commits in Milestone 12 alone.  
**Flow exercised:** M12-01 through M12-05 end-to-end (boundary → scan → candidate generation → artifact conversion).  
**Date:** 2026-05-11

---

## Workflow Walkthrough

### Step 1 — Intake boundary definition (M12-01)

`RepoIntakeBoundary` enforced the following correctly:

| Category | Result |
|---|---|
| Source files (`.cs`, `.csproj`, `.sln`) | Included ✓ |
| CI config (`.github/workflows/*.yml`) | Included ✓ |
| Documentation (`.md`) | Included ✓ |
| Agent instructions (`CLAUDE.md`) | Included ✓ |
| Build artifacts (`bin/`, `obj/`) | Excluded ✓ |
| NuGet packages (`packages/`) | Excluded ✓ |
| Secret candidates (`.env`, `.pem`, `.key`) | Excluded ✓ |
| Generated code (`.g.cs`, `obj/`) | Excluded ✓ |

The boundary correctly excluded all artifact, binary, and secret-candidate paths encountered in this repository.

### Step 2 — Deterministic scan (M12-02)

`DeterministicRepoScanner` produced a stable-ordered entry list across repeated runs. Top-level path grouping correctly identified:

| Top-level area | Actual role |
|---|---|
| `src` | Production source (multi-project) |
| `tests` | Test projects |
| `docs` | Documentation |
| `.github` | CI workflows |
| `benchmarks` | Performance benchmarks |

Correctness: **all five top-level areas identified accurately**.

### Step 3 — Repo-understanding candidate generation (M12-03/04)

`RepoUnderstandingCandidateGenerator` produced the following candidates:

| Signal | Candidate generated | Correct? |
|---|---|---|
| `CLAUDE.md` present | Decision: "Agent instructions file governs AI-assisted development" | ✓ Yes |
| `.github/workflows/` present | Decision: "CI/CD pipeline defined in repository" | ✓ Yes |
| `CONTRIBUTING.md` present | Decision: "Contribution process documented in CONTRIBUTING file" | ✓ Yes |
| Directories `src/`, `tests/`, `docs/`, `.github/`, `benchmarks/` | Contract: "Repository has 5 top-level module areas" | ✓ Yes |
| `Memora.Api` directory contains `Program.cs` | Contract: "HTTP API surface exposed from repository" | ✓ Yes |
| Top-level `src`, `tests` match layer names | Decision: "Layered architecture..." | Partial — src/tests not a classic domain split |

**Observation:** The layered architecture signal fires on `src` + `core` or `domain` patterns. For this repo, `src/Memora.Core` exists but `src` itself is a container, not a layer. The candidate is low-confidence (0.65) and correctly marked `ReviewRequired`. This is the intended behavior: reviewers confirm intent before it becomes canonical.

### Step 4 — GitHub evidence normalization and candidates (M12-07/08/09)

`GitHubEvidenceNormalizer` correctly classified imported records into typed buckets. `GitHubEvidenceCandidateGenerator` produced:

| Signal | Candidate | Confidence | Disposition |
|---|---|---|---|
| Approved reviews present | ContributionStyle: "Code review approval workflow is active" | 0.80 | BaselineMemory |
| Review comments present (> 3) | ContributionStyle: "Review comments indicate inline feedback" | 0.72 | BaselineMemory |
| Merged PRs present | ContributionStyle: "Pull request merge workflow established" | 0.75 | BaselineMemory |
| Tagged releases present | ContributionStyle (Advisory): "Tagged releases suggest release cadence" | 0.62 | ReviewRequired |

One-comment edge: when fewer than 3 review comments are present, the candidate downgrades to `OpenQuestion` (confidence 0.55), which is correct per M12-09 routing rules.

### Step 5 — Candidate artifact conversion (M12-05)

`CandidateArtifactConverter` successfully converted all candidates into Draft artifacts with:
- Correct artifact type per `CandidateMemoryKind` mapping
- `Status = Draft` on every converted artifact (non-canonical)
- Body containing confidence percentage, ambiguity statement, extraction reason
- Sections with `ambiguity`, `extraction_reason`, `source_classification`, and (when present) `evidence_ids`
- Tags with `source:` and `kind:` prefixes for filter-based review grouping

---

## What the Current MVP Gets Right

1. **Determinism.** The scan, candidate generation, and artifact conversion steps produce identical outputs on repeated runs from the same inputs. Candidate IDs are SHA-256-based and stable.

2. **Provenance on every artifact.** Every generated Draft artifact carries `ExtractionReason`, `Ambiguity`, `Confidence`, and `Source` classification. Reviewers have grounds to approve or reject without opening source files.

3. **Non-canonical by default.** No generated artifact enters the canonical path without an explicit review decision. The `ArtifactStatus.Draft` constraint is enforced at the type level.

4. **Intake boundary enforced.** Build artifacts, binaries, secrets, and generated code are excluded before any processing begins.

5. **Low-confidence signals become questions.** `GitHubEvidenceCandidateGenerator` routes below-threshold signals as `OpenQuestion` with `ReviewRequired`, not as silent misses.

6. **Source distinguishability.** `EvidenceDerived`, `Inferred`, and `Advisory` candidates are typed and tagged distinctly, satisfying M12-09's contractual requirement.

---

## What the Current MVP Misses

1. **File body content is not read.** Candidate generation operates entirely on file names, directory structure, and evidence metadata. The signal-to-noise ratio for naming-based signals (e.g., layered architecture detection) may be low for repositories with unusual top-level names.

2. **GitHub evidence signals are limited to metadata.** PR summaries, issue titles, and review state fields drive classification. Actual comment text, PR descriptions, and commit messages are not parsed, so stylistic signals (conventional commits, naming conventions) are not surfaced.

3. **No deduplication between generators.** `FirstRunMemoryGenerator`, `RepoUnderstandingCandidateGenerator`, and `GitHubEvidenceCandidateGenerator` may produce overlapping candidates (e.g., "CI pipeline defined" from both the scan and a GitHub workflow evidence record). The review queue could contain near-duplicate entries requiring manual collapsing.

4. **Advisory discovery is not wired end-to-end.** Advisory candidates are generated in `GitHubEvidenceCandidateGenerator` but there is no pipeline stage that feeds them into the full import flow automatically. They require a caller to wire together `GitHubEvidenceNormalizer → GitHubEvidenceCandidateGenerator → CandidateArtifactConverter`.

5. **Review queue size is unbounded for large repositories.** A repository with many active PRs, issues, and releases will generate a large number of candidates. There is no grouping or pagination mechanism in the current `CandidateArtifactConverter` output beyond stable ordering.

6. **CODEOWNERS and contribution guide content not parsed.** `HasCodeowners` and `HasContributingGuide` detect file presence but do not read ownership mappings or contribution rules. Generated candidates note the files exist but cannot name owners or required steps.

---

## Revision Points

| Priority | Area | Revision needed |
|---|---|---|
| High | Deduplication | Add a deduplication stage that merges candidates with the same `Kind` and semantically equivalent titles across generators before persisting to the review queue |
| High | End-to-end wiring | Add an import orchestration class that calls normalizer → candidate generator → converter in sequence, replacing the current fragmented caller pattern |
| Medium | Body signals | Surface PR description body and commit message content (opt-in, budget-capped) to improve contribution-style signal quality |
| Medium | Review queue size | Add `maxCandidatesPerKind` or confidence thresholding at the import level to prevent queue explosion on high-activity repositories |
| Low | CODEOWNERS parsing | Parse CODEOWNERS file to embed actual ownership patterns in the candidate body, not just file presence |
| Low | Layered architecture detection | Tighten the layer-name detection heuristic or make it configurable to reduce false positives on non-DDD repositories |

---

## Follow-up Work

- [ ] Deduplication stage between candidate generators
- [ ] End-to-end import orchestration class
- [ ] Review queue size controls (confidence floor or per-kind cap)
- [ ] Opt-in body content sampling for contribution-style signals
- [ ] CODEOWNERS content parsing
- [ ] Test against a second large repository (e.g., a large open-source .NET project) to validate cross-repo signal quality
