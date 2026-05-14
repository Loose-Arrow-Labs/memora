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

Goal: make review, approval, and trust inspection usable from the operator UI,
IDE surfaces, and planning tools before deeper automation expands.

Includes:

- proposal review interface with provenance preview
- approval and rejection persistence through governed lifecycle rules
- proposed-to-draft promotion path so the full lifecycle is traversable from
  any review surface (closes PBR-01)
- deterministic state view rendering
- validation and lifecycle failure display
- initial trust dashboard for pending proposals, stale drafts, broken
  relationships, rebuild diagnostics, and missing project memory
- VS Code and Cursor review inbox and governed approve/reject bridge wired
  end-to-end against a real workspace
- Obsidian review inbox plugin: sidebar panel showing the approval queue,
  approve/reject commands routed through the governed API, read-only artifact
  preview — same governed workflow as VS Code, surfaced in the planning
  environment
- review UI structure that keeps granular views organized without making them
  competing sources of truth

Outcome: operators can inspect imported evidence, generated candidates, and
agent proposals in the places they already work — IDE for coding, Obsidian for
planning — while Memora remains the only governed authority for canonical
memory.

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

## Milestone 13 - Capture Bridge MVP

Goal: provide a tool-agnostic local-first capture path that lets operators use
the tools they already have — Obsidian, Notion, any markdown folder — as a
proposal input surface, without building or maintaining a custom mobile app.

The capture bridge is a folder-watch ingest path. Anything that can write a
markdown file to a watched folder is a valid capture source. The MVP targets
Obsidian because it is what the operator uses, syncs via Nextcloud, and already
has a mobile app that works. The architecture must not assume Obsidian.

Includes:

- desktop folder-watch service that monitors a configured inbox folder for new
  markdown files matching the contribution packet format
- automatic intake of watched packets as non-canonical planning or proposal
  input, routed into the existing governed review path
- Obsidian capture plugin (MVP): a community plugin that provides a "Send to
  Memora" command, writes a correctly-formatted contribution packet to the
  watched inbox folder, and respects the non-canonical packet contract
- packet format validation at ingest with clear diagnostics for malformed or
  non-compliant files
- documentation for connecting other tools via the folder-watch pattern:
  Notion markdown export, Logseq, Bear, Roam, or any tool that writes to a
  folder
- Nextcloud and shared-folder transfer as first-class supported transport given
  existing operator setup — no custom sync protocol required
- custom mobile app explicitly deferred: mobile capture is solved by Obsidian
  mobile plus Nextcloud sync; a dedicated Memora mobile app is not in scope
  unless the folder-watch pattern proves insufficient

Outcome: operators can capture questions, decision drafts, planning notes, and
proposal drafts from any tool or device they already use. Notes flow into
Memora's governed review queue automatically. No new app is required.

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

Goal: make Memora the shared governed context layer for a human operator
working across multiple agents and tools simultaneously — the connected
workspace described in CHR-002.

Includes:

- setup packs for Codex, Claude Code, ChatGPT (MCP + OpenAPI paths), Cursor,
  Cline/Roo, Gemini CLI, OpenCode, Windsurf, and Aider where practical
- validated end-to-end path: agent proposes → review inbox (VS Code or
  Obsidian) → operator approves → canonical context updated → all other agents
  see the change on next context retrieval
- deterministic session handoff packages so any agent joining mid-project gets
  the same grounded context without re-explanation
- agent-facing contribution-style and project-state bundles
- MCP and OpenAPI workflow validation across representative clients
- enforcement validation: confirm agents connected via MCP can only call
  propose_artifact, propose_update, record_outcome, and read tools — raw
  filesystem write access is not an available path
- local external workflow guidance updated around imported project workspaces
- agent readiness report v2 that verifies each configured tool can resolve the
  intended Memora project

Outcome: a developer can work from ChatGPT, VS Code, Rider, or Obsidian and
have all active agents operating against the same governed project context.
Proposals from any agent appear in a single review inbox. Canonical truth is
shared, deterministic, and never written by an agent directly.

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

### Band 1 - Foundation (Complete)

Core memory, relationships, context assembly, understanding outputs, workflow
hardening, automation, retrieval, Machina alignment, state view, project
import, repo and GitHub evidence understanding.

- Milestone 1 - Memory Core
- Milestone 2 - Relationship + Traceability Layer
- Milestone 3 - Context Assembly Core
- Milestone 4 - Understanding Outputs
- Milestone 5 - Workflow Hardening
- Milestone 6 - Controlled Automation
- Milestone 7 - Advanced Retrieval Evolution
- Milestone 8 - Machina Alignment
- Milestone 9 - Deterministic Project State View
- Milestone 10 - Project Import And First Run
- Milestone 12 - Existing Repo And GitHub Evidence Understanding MVP

### Band 2 - Connected Workspace MVP (Now)

Get the governed human+agent workflow actually working end-to-end across the
tools the operator already uses. This band is the direct expression of CHR-002.

Priority order:

- Milestone 11 - Human Review And Trust UI (close PBR-01, wire VS Code
  end-to-end, ship Obsidian review inbox plugin)
- Milestone 13 - Capture Bridge MVP (folder-watch ingest, Obsidian capture
  plugin, tool-agnostic pattern documented)
- Milestone 15 - Agentic Workflow Baseline (setup packs, enforcement
  validation, connected workspace end-to-end)

Private beta readiness issues (PBR-01 through PBR-20) run as a parallel
track throughout Band 2. They are not a milestone but are blocking on the
same surfaces.

### Band 3 - Observability And Scale

- Milestone 14 - Observability And Replay Debugging
- Milestone 16 - Remote Conversational Planning

## Guidance

Milestones describe intended product slices and captured roadmap progression.
Use `docs/current-state.md` to distinguish implemented behavior from planned or
sample-captured next work.

Memora must remain filesystem-first, approval-governed, and deterministic at
its core even as integrations and automation expand.
