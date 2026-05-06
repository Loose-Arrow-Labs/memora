# Import And Workspace Strategy

## Purpose

This document captures the planned first-run import and workspace model for the
next Memora phase. It is roadmap guidance, not current implementation.

The product goal is simple:

- install Memora without Docker
- attach a local repository or GitHub repository
- scan project evidence immediately
- produce reviewable or baseline project memory within the first ten minutes
- make agents safer to use in legacy codebases

## Workspace Placement

Memora should run as one local app or service that can manage many projects.
Each project should still have an isolated Memora workspace.

Recommended default:

```text
<memora-app-data>/
  workspaces/
    <project-id>/
      canonical/
      drafts/
      summaries/
      evidence/
      indexes/
      project.json
```

Source repositories remain evidence sources, not Memora storage roots.

For example, a user may keep code under:

```text
C:/Users/<user>/source/repos/<repo>
```

Memora may scan that repo, read its Git history, and attach GitHub evidence, but
the governed Memora workspace should live in an app-managed workspace root by
default. This keeps source checkout state clean and avoids mixing generated
memory, imports, review state, and derived indexes into application code.

An optional repo-local pointer may be useful later:

```text
<repo>/.memora/project.json
```

That pointer should only identify the Memora project/workspace. It should not
become the canonical artifact store unless the operator explicitly chooses a
repo-local mode.

Placement rules:

- default workspace placement is app-managed under the Memora workspace root
- attached source repositories are scanned as evidence sources
- imported evidence, generated candidates, review state, and derived indexes
  live in the Memora workspace, not in the source checkout
- repo-local files may only point to an existing Memora project; they must not
  become a second canonical store
- a source repository can be detached or moved without changing the canonical
  Memora workspace identity

## Multi-Project Model

Memora should use:

- one installed local app/service
- many project workspaces
- one workspace per attached project
- scoped SQLite indexes per workspace or an equivalent clearly scoped index
- explicit project resolution before agents retrieve context

Agents must identify or resolve the target project before calling context,
proposal, update, outcome, replay, or evidence tools.

Project resolution expectations:

- integrations list or resolve Memora projects by workspace-backed project id
- an attached source repository is one resolution hint, not the project itself
- if more than one Memora project points at the same source remote or local
  path, the operator or agent must choose the project explicitly
- MCP and OpenAPI must expose the same project identity and import/readiness
  state through shared contracts
- proposal and write paths remain scoped to the resolved Memora project and
  continue to obey lifecycle and approval rules

## Import Sources

The first-run import flow should support:

- local Git repositories
- GitHub repositories
- commits, branches, tags, and release history
- GitHub issues, pull requests, review comments, and discussions when available
- repository files, docs, manifests, build scripts, and test configuration
- CODEOWNERS and other directly sourced ownership signals
- agent run packets and mobile contribution packets in later milestones

Imported source material is evidence. Derived project meaning is generated from
that evidence and must follow the selected import mode.

## Evidence And Meaning Classes

M10 import uses three trust classes:

- baseline evidence: directly observed project facts such as commits, tags,
  issue metadata, PR metadata, files, and release records that may be trusted
  according to the selected import mode
- baseline memory: deterministic project facts derived from direct observation,
  such as repository structure, detected build commands, or detected test
  commands, when the selected mode permits baseline promotion
- reviewable inferred meaning: decisions, constraints, contribution style,
  risks, bug patterns, ownership claims, and open questions that require review
  unless a later approved bulk policy explicitly promotes them

The boundary is intentionally conservative. Evidence says what was observed.
Memory says what Memora is allowed to preserve as project understanding.
Inferred meaning stays visible, provenance-backed, and reviewable.

## Hybrid Retrieval Boundary

Project import is the first place Memora's hybrid retrieval strategy becomes
visible.

M10 combines:

- deterministic scans over attached local repositories and GitHub evidence
- evidence-derived candidate memory with provenance and confidence
- later advisory discovery hooks that can suggest additional candidates
- deterministic, lifecycle-aware assembly when agents request grounded context

Advisory discovery cannot bypass import modes, lifecycle, approval, safety
filtering, or provenance. It may help find candidate material, but Memora's
agent-facing grounded output still comes from approved or explicitly allowed
artifacts assembled through deterministic context rules.

## GitHub Import Risks

GitHub import is a real integration surface, not just another file scan. The
design must account for:

- authentication and token scope
- private repository access
- organization permissions
- rate limits and pagination
- partial imports and resumability
- freshness and re-import behavior
- deleted, force-pushed, or rewritten remote history
- issue and PR edits after import
- review comments that conflict or become outdated

These cases should produce explicit diagnostics. A partial GitHub import may
still be useful, but it must not silently present incomplete evidence as a
complete project history.

## Import Modes

### Fast Baseline

Default for most legacy onboarding.

- directly observed evidence becomes baseline project evidence
- deterministic repo facts can become approved baseline memory
- inferred decisions, constraints, contribution style, risks, and bug patterns
  become reviewable candidates
- the operator reviews grouped results instead of approving every commit or PR

Use this when the codebase has already lived with years of decisions and the
goal is to get useful agent context quickly.

Promotion behavior:

- baseline evidence may be stored as canonical evidence after safety filtering
- deterministic baseline memory may be eligible for approved baseline status
- inferred meaning is generated as reviewable candidate memory

### Strict Governance

Use for regulated or high-risk projects.

- all imported evidence and derived memory enters as proposed or draft
- no automatic canonical baseline is created
- the operator explicitly approves durable truth

Promotion behavior:

- baseline evidence remains reviewable until approval
- baseline memory remains proposed or draft
- inferred meaning remains reviewable candidate memory

### Evidence Canonical

Use when raw project history should be trusted but interpretations should not.

- commits, PRs, issues, files, tags, and direct metadata become canonical
  evidence records
- derived decisions, constraints, outcomes, and contribution-style rules remain
  proposed until reviewed

Promotion behavior:

- baseline evidence may become canonical evidence after safety filtering
- directly observed memory candidates remain reviewable unless they are
  represented as evidence records
- inferred meaning remains reviewable candidate memory

### Bulk Approval

Use for practical review after import.

- Memora groups candidates by source, type, confidence, and risk
- the operator approves batches such as repo structure, test commands,
  dependency inventory, merged PR outcomes, or CODEOWNERS-derived ownership
- the approval target is the batch policy and candidate class, not each row

Promotion behavior:

- baseline evidence is grouped for trust review
- baseline memory and inferred meaning are grouped by source, confidence, and
  risk
- approval applies to the selected batch and candidate class only, preserving
  provenance for every promoted item

## Trust Boundary

The governing rule:

> Evidence can be bulk-trusted. Meaning should be reviewable.

For legacy projects, the existing code, commits, releases, and merged PRs are
already-lived reality. Memora should not make users approve that the past
happened one item at a time.

Memora should still keep interpretation honest:

- directly observed facts may be baseline truth under the selected mode
- inferred intent remains candidate memory
- confidence and provenance must be visible
- secret and privacy filtering happens before persistence
- all generated candidates must identify their evidence sources

## First-Run Success Criteria

Within the first ten minutes, Memora should be able to show:

- attached project identity and repository source
- scan/import progress
- imported evidence counts
- detected build and test commands
- repo structure and high-level components
- GitHub issue/PR/review counts when connected
- candidate decisions, constraints, outcomes, and contribution-style rules
- agent readiness gaps
- baseline approval or review next steps
- MCP/OpenAPI readiness for the imported project, including project resolution
  and hybrid retrieval status plus deterministic grounded context retrieval

Example first-run result:

```text
Imported 18,240 commits, 1,420 pull requests, and 380 issues as project
evidence. Generated 64 candidate memories. 19 are eligible for baseline
approval, and 45 need review.
```

The first M10 implementation should target a narrow happy path before chasing
all legacy edge cases:

- one attached local or GitHub repository
- bounded evidence import
- secret/privacy filtering
- candidate memory generation
- readiness report
- MCP/OpenAPI exposure that respects import mode and lifecycle rules

Happy-path sequence:

1. create or select an app-managed Memora workspace
2. attach one local Git repository or one GitHub repository as a source
3. choose Fast Baseline, Strict Governance, Evidence Canonical, or Bulk
   Approval before promotion behavior runs
4. run a bounded import with progress, evidence counts, and safety diagnostics
5. persist safe evidence according to the selected import mode
6. generate hybrid candidate memory and an agent readiness report, separating
   deterministic evidence-derived findings from inferred or advisory candidates
7. show baseline evidence, baseline memory, and review-needed candidates
   separately
8. expose the imported project through MCP and OpenAPI project resolution and
   deterministic context retrieval

Runtime evidence remains inside this import boundary. Memora may ingest
observed runtime records later, but it must not host agents, orchestrate their
execution loops, or replay runtime behavior by re-executing it.

## Future Packaging Note

Deep legacy import is commercially valuable and may become a paid feature later.
The technical boundary should support that without changing the trust model:

- free/local: current repo scan and basic docs
- pro: full Git history import and agent readiness report
- team: GitHub issues, PRs, reviews, contribution style, and bulk approval
- enterprise: decades-scale import, bug escape analysis, retention policy, and
  compliance export

Pricing is not part of core architecture. Import mode and evidence governance
are.
