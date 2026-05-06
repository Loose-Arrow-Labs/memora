# Current State

This document describes the implemented behavior in the current checkout.
It is intentionally separate from roadmap and milestone planning docs.

## Implemented Now

### Foundation

- .NET 10 solution with product projects for Core, Import, Storage, Index, Context, API, MCP, and UI
- typed artifact models, enums, and lifecycle rules in `Memora.Core`
- artifact validation, diagnostic formatting, planning intake, draft editing, approval queue building, and revision diffs
- revision diffs include deterministic change areas, display labels, and raw paths
- markdown plus frontmatter parsing in `Memora.Storage`
- filesystem persistence for canonical, draft, and summary artifacts
- workspace discovery through `project.json`
- first-run import foundation with provider-agnostic import modes and
  repository attachment metadata
- shared repository attachment service for local Git and GitHub source
  references that keeps Memora workspaces app-managed by default
- local Git evidence import for attached repositories, including commits,
  branches, tags, changed-file summaries, changelog/release signals,
  stable evidence ids, idempotent JSON persistence under `evidence/`, and
  bounded diagnostics
- GitHub evidence normalization and import for attached GitHub repositories,
  including issues, pull requests, reviews, review comments, commits, releases,
  optional discussion metadata from client snapshots, stable provenance, partial
  import diagnostics, and idempotent JSON persistence under `evidence/`
- import intake safety filtering for local Git and GitHub evidence, with
  redaction diagnostics for common tokens and blocking diagnostics for unsafe
  private key material before evidence persistence
- first-run candidate memory generation for the deterministic evidence-derived
  side of Memora's hybrid retrieval model: repo structure, build/test commands,
  constraints, outcomes, contribution style, risks, and open questions, with
  evidence ids, confidence, ambiguity, extraction reason, and baseline/review
  disposition
- agent readiness report generation for missing context, missing tests, risky
  modules, and next review steps, with optional JSON persistence under
  `summaries/first-run-readiness.json`
- SQLite schema plus rebuild-from-files indexing in `Memora.Index`
- typed relationship indexing and direct, dependency, and impact traceability queries over approved artifacts
- rebuild diagnostics distinguish filesystem truth from derived SQLite index state

### Context Assembly

- deterministic context bundle models
- deterministic ranking with stable ordering
- derived context package caching keyed by request shape and loaded artifact fingerprints
- bounded typed relationship traversal for focus proximity
- explicit inclusion reasoning for selected artifacts, including traversed relationship paths
- layered bundle assembly in `Memora.Context`
- a normalized runtime-facing state view exposed as `GetContextResponse.bundle`
- context viewer UI route at `/context-viewer` rendering the shared
  `GetContextResponse.bundle` deterministic state-view contract
- optional retrieval extension contracts exist for future advisory candidate discovery, but they are disabled by default and do not execute semantic retrieval in core v1
- hybrid retrieval is represented as a boundary: advisory discovery may produce
  reviewable candidates later, while final context assembly remains
  deterministic, explainable, and lifecycle-aware

### Integration Surfaces

- local HTTP endpoints in `Memora.Api` for:
  - project lookup with repository attachment and imported readiness state
  - context assembly
  - artifact proposals
  - artifact updates
  - outcome recording
- a thin MCP adapter in `Memora.Mcp` over the shared agent interaction contract
- MCP tools for `get_context`, `propose_artifact`, `propose_update`, and `record_outcome`
- MCP resource template `memora://projects/{projectId}` exposing project
  metadata, repository attachments, and imported readiness state
- a provider-agnostic external runtime contract definition shared by MCP and OpenAPI
- byte-equal compatibility validation for the serialized state view across MCP and OpenAPI paths
- protocol parity validation for imported project readiness state across MCP and
  OpenAPI project lookup
- a documented Machina-to-Memora interaction model that keeps runtime execution outside Memora
- runtime-facing prototype coverage for deterministic context retrieval, proposal submission, update proposal submission, and outcome recording through the current OpenAPI path
- Codex external workflow sample that performs project lookup, context retrieval, proposal submission, and outcome recording
- ChatGPT-oriented sample that validates the current read-only state-view path
- shared compatibility validation across the current MCP and OpenAPI runtime-facing surfaces

### Controlled Automation

- low-risk automation candidates are defined explicitly in `Memora.Core`
- controlled automation policies declare allowed actions, artifact classes, storage scope, and guardrails
- safe trigger evaluation requires explicit operator-requested triggers before policy-governed writes become eligible
- policy-governed write safety validation blocks invalid policy, trigger, project, artifact, and storage-scope cases before persistence
- a guarded file-backed prototype can write `session_summary` artifacts to summary storage only

### Operator UI

- styled local operator shell in `Memora.Ui`
- project selection from discovered workspaces
- artifact browsing and draft editing
- approval queue navigation, proposal review listing, revision review previews,
  and decision-readiness context
- first-run import status page showing attached repository identity, selected
  import mode, progress, evidence counts, warnings, candidate source and
  disposition, readiness report details, and next actions
- context viewer page backed by the shared deterministic state-view contract
- understanding output page with context, traceability, and component views

### Operator Guidance

- workflow-focused operator guide in `docs/operator-workflows.md`
- operations doc that points operators to current review and recovery workflows
- remote conversational planning gap analysis that distinguishes the current local external workflow proof from future remote planning-write work
- sample draft artifacts that capture the next IDE review boundary for VS Code and Cursor

## Still Intentionally Thin

- UI review is preview-oriented and does not persist approval or rejection decisions
- API is a minimal HTTP surface, not a fully documented production service
- MCP is currently an in-process adapter surface, not a complete hosted server transport
- runtime alignment is grounded in the shared contract, but hosted transport, remote reachability, authentication, and provider-specific attachments still remain follow-up work
- context assembly is deterministic and explainable, while the broader product
  retrieval strategy is hybrid: advisory discovery can suggest candidates, but
  core v1 remains non-semantic and non-vector
- cached context packages are derived convenience and never replace filesystem truth
- rebuild diagnostics identify filesystem issues, but they do not auto-repair artifacts or indexes
- controlled automation does not provide a general direct-write path and does not write canonical artifacts
- IDE review is captured as draft/sample planning state, not implemented product behavior
- first-run import UI is status and inspection only; it does not execute imports,
  persist approval decisions, or promote candidate memory to canonical truth

## Where To Look In Code

### Core Domain

- `src/Memora.Core/Artifacts/ArtifactDocuments.cs`
- `src/Memora.Core/Validation/`
- `src/Memora.Core/Approval/`
- `src/Memora.Core/Automation/`
- `src/Memora.Core/Revisions/`
- `src/Memora.Core/AgentInteraction/AgentInteractionContract.cs`
- `src/Memora.Core/AgentInteraction/ExternalRuntimeContract.cs`
- `src/Memora.Core/AgentInteraction/ProjectStateViewSerializer.cs`
- `src/Memora.Core/Import/`

### Import

- `src/Memora.Import/Attachment/RepositoryAttachmentService.cs`
- `src/Memora.Import/Evidence/FileBackedImportedEvidenceStore.cs`
- `src/Memora.Import/GitHub/GitHubEvidenceImporter.cs`
- `src/Memora.Import/GitHub/GitHubCliEvidenceClient.cs`
- `src/Memora.Import/Git/LocalGitEvidenceImporter.cs`
- `src/Memora.Import/Git/ProcessGitRepositoryInspector.cs`
- `src/Memora.Import/Readiness/FirstRunMemoryGenerator.cs`
- `src/Memora.Import/Readiness/FileBackedFirstRunReportStore.cs`
- `src/Memora.Import/Safety/ImportContentSafetyFilter.cs`

### Storage

- `src/Memora.Storage/Parsing/ArtifactMarkdownParser.cs`
- `src/Memora.Storage/Persistence/ArtifactFileStore.cs`
- `src/Memora.Storage/Workspaces/WorkspaceDiscovery.cs`

### Index

- `src/Memora.Index/Schema/SqliteIndexSchema.cs`
- `src/Memora.Index/Rebuild/SqliteIndexRebuilder.cs`
- `src/Memora.Index/Rebuild/IndexRebuildResult.cs`
- `src/Memora.Index/Relationships/ArtifactRelationshipIndex.cs`
- `src/Memora.Index/Traceability/TraceabilityQueryService.cs`

### Context

- `src/Memora.Context/Models/ContextBundleModels.cs`
- `src/Memora.Context/Ranking/DeterministicContextRankingEngine.cs`
- `src/Memora.Context/Reasoning/ContextInclusionReasoner.cs`
- `src/Memora.Context/Assembly/ContextBundleBuilder.cs`
- `src/Memora.Context/Assembly/ContextPackageCache.cs`
- `src/Memora.Context/Extensions/OptionalRetrievalExtension.cs`

### Retrieval Evolution Docs

- `docs/retrieval-evolution.md`

### API

- `src/Memora.Api/Program.cs`
- `src/Memora.Api/Services/FileSystemAgentInteractionService.cs`
- `src/Memora.Api/AgentInteractionHttpResults.cs`
- `tests/Memora.Api.Tests/RuntimeFacingPrototypeTests.cs`
- `tests/Memora.Api.Tests/RuntimeContractCompatibilityTests.cs`

### Controlled Automation Docs

- `docs/controlled-automation.md`

### MCP

- `src/Memora.Mcp/Server/MemoraMcpServer.cs`

### Runtime Alignment Docs

- `docs/external-runtime-contract.md`
- `docs/machina-interaction-model.md`
- `docs/integration-strategy.md`
- `docs/project-state-view.md`
- `docs/agent-project-state-interpretation.md`
- `docs/remote-conversational-planning-gap-analysis.md`

### UI

- `src/Memora.Ui/Program.cs`
- `src/Memora.Ui/Operator/LocalOperatorWorkspaceService.cs`
- `src/Memora.Ui/Rendering/OperatorShellPageRenderer.cs`
- `src/Memora.Ui/ContextViewer/FileSystemContextViewerService.cs`
- `src/Memora.Ui/Understanding/FileSystemUnderstandingOutputService.cs`

## Local Run Behavior

### UI

- project: `src/Memora.Ui`
- default dev URL: `http://127.0.0.1:5080`
- if no workspace root is configured, it boots from a writable local copy of `samples/workspaces`

### API

- project: `src/Memora.Api`
- default dev URL: `http://127.0.0.1:5081`
- it uses a file-backed agent interaction service only when `MEMORA_WORKSPACES_ROOT` or `Memora:WorkspacesRootPath` is configured

## Guidance

- use `docs/milestones.md` for roadmap intent
- use this file for implemented behavior
- if docs and code disagree, the code wins and the docs should be updated
