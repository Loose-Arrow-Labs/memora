# Milestones

## Milestone 1 - Memory Core

Goal: build the durable core that defines and protects Memora's project truth.

Includes:

- repository scaffold and solution structure
- artifact base schema and enums
- Markdown plus frontmatter parsing
- artifact-specific validation
- lifecycle transition validation
- multi-project workspace model
- canonical and draft filesystem storage
- typed relationship parsing
- SQLite index schema
- rebuild-from-files indexing path
- core schema, lifecycle, parsing, and rebuild tests

Outcome: Memora can store, validate, version, and index structured project
artifacts using filesystem truth and a rebuildable SQLite index.

## Milestone 2 - Relationship + Traceability Layer

Goal: persist typed relationships and expose deterministic traceability,
dependency, and impact queries across approved artifacts.

Includes:

- relationship persistence
- relationship query model
- incoming and outgoing relationship queries
- traceability query model
- dependency and impact traceability queries
- tests and validation for relationship and traceability behavior

Outcome: Memora can store and query explicit artifact relationships as durable,
explainable understanding data without taking on broad retrieval or graph
system responsibilities.

## Milestone 3 - Context Assembly Core

Goal: assemble deterministic context bundles from approved artifacts with
explicit inclusion reasoning and stable ordering.

Includes:

- deterministic context bundle builder
- inclusion reasoning and deterministic ranking rules
- context bundle models
- OpenAPI context bundle endpoints
- MCP context assembly surface
- tests and validation for context assembly behavior

Outcome: Memora can build grounded, task-oriented context bundles that explain
why each artifact was included and preserve deterministic understanding-first
behavior.

## Milestone 4 - Understanding Outputs

Goal: produce basic human-readable understanding outputs from context and
traceability data without changing Memora's core responsibilities.

Includes:

- understanding output models
- context views
- traceability views
- component understanding outputs
- output strategy documentation
- validation for understanding outputs

Outcome: Memora can turn approved artifacts, relationships, and context bundles
into clear understanding outputs that remain grounded in canonical project
memory.

## Milestone 5 - Workflow Hardening

Goal: improve day-to-day usability, trust, and operator confidence.

Includes:

- approval UX improvements
- clearer revision diff handling
- stronger validation error reporting
- rebuild and consistency diagnostics
- workflow-focused operator guidance
- end-to-end human-loop test expansion

Outcome: Memora becomes smoother to operate and easier to trust during regular
use.

## Milestone 6 - Controlled Automation

Goal: introduce carefully bounded automation without weakening governance.

Includes:

- definition of low-risk artifact classes for future direct-write
- policy model for controlled automation
- safer event handling for automation triggers
- selective direct-write prototype behind guardrails
- safety validation for policy-governed writes

Outcome: Memora begins moving from proposal-only interaction toward selective
trusted automation.

## Milestone 7 - Advanced Retrieval Evolution

Goal: improve retrieval depth and efficiency without changing the canonical
truth model.

Includes:

- deterministic retrieval optimization
- cached context package support
- expanded relationship traversal
- retrieval evolution documentation

Outcome: Memora retrieval becomes faster and richer while keeping deterministic
project truth intact.

## Milestone 8 - Machina Alignment

Goal: make Memora a stable cognition and governance layer for future runtime
integration.

Includes:

- external runtime contract definition
- Machina-to-Memora interaction model
- runtime-facing context and proposal prototype
- shared contract compatibility validation across runtimes

Outcome: Memora is ready to serve as a memory and governance substrate for
Machina and other runtimes.

## Milestone 9 - Deterministic Project State View

Goal: name and document the runtime-facing state view already carried by the
shared context contract without adding a second project-state model.

Includes:

- project-state view documentation
- state-view boundary rules
- agent interpretation guidance
- serialized contract normalization through `GetContextResponse.bundle`
- compatibility expectations across MCP and OpenAPI paths

Outcome: external runtimes can treat the current `get_context` response as a
bounded, explainable project-state view while still respecting lifecycle,
approval, and filesystem-first truth rules.

## Milestone 10 - Project Import And First Run

Goal: make the first ten minutes excellent by letting an operator install
Memora without Docker, attach a local or GitHub repository, import project
evidence, and see immediate reviewable direction for agent work.

Includes:

- app-managed multi-project workspace setup
- local Git and GitHub repository attachment
- first-run import modes: Fast Baseline, Strict Governance, Evidence
  Canonical, and Bulk Approval
- deterministic repo scan and Git evidence import
- direct GitHub issue, PR, review, commit, release, and discussion evidence
  intake where available
- secret and privacy filtering before evidence persistence
- first-run project import dashboard with scan progress and evidence counts
- initial candidate project memory: repo structure, build/test commands,
  constraints, outcomes, contribution style, risks, and open questions
- agent readiness report that highlights missing project memory and next review
  steps
- hybrid retrieval boundary for imported projects: deterministic evidence
  scans, reviewable advisory candidates, deterministic grounded context
  retrieval, readiness-state exposure, and lifecycle-safe proposal paths

Outcome: a legacy repo can be attached and scanned quickly, with lived project
evidence imported into a governed Memora workspace and derived meaning prepared
for review or baseline approval according to the selected import mode.

M10 should sequence toward a polished happy-path first-run experience, not every
legacy import edge case at once. The first target is one attached local or
GitHub repository, bounded evidence import, candidate memory generation,
readiness reporting, and MCP/OpenAPI exposure that respects the selected import
mode and the hybrid retrieval boundary.

## Milestone 11 - Human Review And Trust UI

Goal: make review, approval, and trust inspection usable from the operator UI
and IDE surfaces before deeper automation expands.

Includes:

- proposal review interface with provenance preview
- approval and rejection persistence through governed lifecycle rules
- deterministic state view rendering
- validation and lifecycle failure display
- initial trust dashboard for pending proposals, stale drafts, broken
  relationships, rebuild diagnostics, and missing project memory
- VS Code and Cursor review inbox and governed approve/reject bridge
- review UI structure that keeps granular views organized without making them
  competing sources of truth

Outcome: operators can inspect imported evidence, generated candidates, and
agent proposals in the places they already work, while Memora remains the only
governed authority for canonical memory.

## Milestone 12 - Existing Repo And GitHub Evidence Understanding MVP

Goal: turn imported local repo and GitHub evidence into useful, reviewable
project understanding for legacy codebases.

Includes:

- deterministic repo intake boundary and large-repo scan behavior
- GitHub evidence normalization for issues, PRs, diffs, reviews, comments,
  commits, releases, and linked discussions
- candidate decisions with provenance
- candidate constraints and contribution-style rules with provenance
- candidate integration contracts, APIs, modules, adapters, and ownership areas
- candidate outcomes from merged PRs, releases, bug fixes, and recurring review
  signals
- confidence and ambiguity reporting for inferred meaning
- conversion of candidates into reviewable Memora artifacts
- large legacy repo golden path validation

Outcome: Memora can explain what a legacy project appears to know from its code
and history without confusing imported evidence with approved interpretation.

## Milestone 13 - Mobile Contribution MVP

Goal: provide a local-first mobile capture path that feeds the same governed
intake and review model without requiring hosted sync or mobile approval.

Includes:

- portable contribution packet format
- simple local-first mobile capture surface
- copy, export, and local save flows
- desktop import path into non-canonical planning or proposal input
- Nextcloud-style shared-folder transfer workflow
- same-network sync feasibility spike only if it remains small and low-risk

Outcome: mobile notes can become structured Memora evidence or proposals
through the desktop review flow while preserving local-first boundaries.

## Milestone 14 - Observability And Replay Debugging

Goal: make Memora able to show what agents saw, what evidence was used, why a
proposal happened, and how bugs escaped.

Includes:

- agent run records linked to project, branch, issue, PR, commits, and context
  request
- deterministic context snapshot hashes and included artifact revisions
- timeline of context retrieval, file changes, commands, tests, proposals,
  outcomes, review comments, and validation failures
- replay/debugging UI for Memora interactions and imported agent run packets
- bug escape analysis that compares missed bugs against known constraints,
  outcomes, test expectations, and GitHub review evidence
- proposed follow-up artifacts for newly discovered constraints, regression
  tests, outcomes, or contribution-style rules

Outcome: Memora can help teams understand why an agent missed something and
what governed memory should be added so the same failure is less likely next
time.

Boundary: this milestone ingests and reconstructs runtime evidence. It must not
turn Memora into an agent execution host, workflow orchestrator, or system that
replays runtime behavior by re-executing it.

## Milestone 15 - Agentic Workflow Baseline

Goal: make Memora straightforward to attach to common agent and chat tools
after project import, review, and observability foundations exist.

Includes:

- setup packs for Codex, Claude Code, Cursor, Cline/Roo, Gemini CLI, OpenCode,
  Windsurf, and Aider where practical
- deterministic session handoff packages
- agent-facing contribution-style and project-state bundles
- MCP and OpenAPI workflow validation across representative clients
- local external workflow guidance updated around imported project workspaces
- agent readiness report v2 that verifies each configured tool can resolve the
  intended Memora project

Outcome: agents can reliably retrieve governed context, respect contribution
style, submit reviewable proposals, and record outcomes for imported legacy
projects.

## Milestone 16 - Remote Conversational Planning

Goal: define and validate the smallest real remote planning workflow in which
an external conversation client can create reviewable Memora artifacts without
weakening filesystem-first truth or approval governance.

Includes:

- remote reachability model
- client authentication and project scoping
- conversation-to-artifact proposal mapping
- remote review and approval fit
- first real remote planning-write prototype

Outcome: remote conversations can create reviewable planning artifacts while
canonical truth remains in governed Memora workspaces.

## Roadmap Bands

### Band 1 - Core Product Build

- Milestone 1 - Memory Core
- Milestone 2 - Relationship + Traceability Layer
- Milestone 3 - Context Assembly Core

### Band 2 - Usability And Ecosystem Fit

- Milestone 4 - Understanding Outputs
- Milestone 5 - Workflow Hardening

### Band 3 - Automation And Runtime Evolution

- Milestone 6 - Controlled Automation
- Milestone 7 - Advanced Retrieval Evolution
- Milestone 8 - Machina Alignment
- Milestone 9 - Deterministic Project State View
- Milestone 10 - Project Import And First Run
- Milestone 11 - Human Review And Trust UI
- Milestone 12 - Existing Repo And GitHub Evidence Understanding MVP
- Milestone 13 - Mobile Contribution MVP
- Milestone 14 - Observability And Replay Debugging
- Milestone 15 - Agentic Workflow Baseline
- Milestone 16 - Remote Conversational Planning

## Guidance

Milestones describe intended product slices and captured roadmap progression.
Use `docs/current-state.md` to distinguish implemented behavior from planned or
sample-captured next work.

Memora must remain filesystem-first, approval-governed, and deterministic at
its core even as integrations and automation expand.
