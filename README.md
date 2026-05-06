# Memora

Memora helps teams use AI agents on legacy codebases without turning tribal
knowledge, stale docs, and runtime guesses into unreviewed truth.

The wedge is simple: point Memora at an existing local or GitHub repository,
import bounded project evidence, generate reviewable project memory, and give
agents governed context they can cite instead of whatever they happen to infer
from the current prompt.

Memora is local-first structured memory and governance for AI-assisted software
development. It is not a chat-history store, vector database, generic knowledge
base, or agent execution runtime.

## Why Try It

Legacy codebases are hard for agents because the useful context usually lives
between commits, PRs, issues, conventions, old decisions, test scars, and
half-remembered constraints. Generic agent memory tends to blur those signals
together.

Memora keeps the boundary sharper:

- evidence is imported with provenance
- meaning is generated as baseline or reviewable candidate memory
- approved artifacts remain canonical project truth
- agents can retrieve grounded context and submit proposals
- lifecycle and approval rules stay below every UI, MCP, and OpenAPI surface

The goal is not to make agents "remember more." The goal is to make project
understanding durable, inspectable, and governed enough that agent work becomes
easier to trust.

## First-Run Promise

The M10 first-run path is aimed at the first ten minutes with an existing repo,
using the local .NET app path rather than requiring Docker:

1. create or select an app-managed Memora workspace
2. attach one local Git or GitHub repository as a source
3. choose an import mode before promotion behavior matters
4. import bounded evidence with secret/privacy filtering
5. generate candidate memory and an agent readiness report
6. inspect baseline evidence, baseline memory, and review-needed candidates
7. expose project identity, readiness, and grounded context through MCP/OpenAPI

This checkout contains the narrow M10 implementation slices for that path. The
deeper legacy understanding story remains roadmap work: large-scale imports,
approval persistence in the UI, provider setup packs, hosted transports,
observability/replay, and richer candidate conversion still belong to later
milestones. Use [docs/current-state.md](docs/current-state.md) for the exact
implemented state and [docs/milestones.md](docs/milestones.md) for roadmap
intent.

## Retrieval In Plain English

Memora uses hybrid retrieval at the discovery boundary and governed assembly at
the agent-context boundary.

Broad discovery can find candidates from imported evidence, repository scans,
GitHub signals, or future advisory providers. Those candidates can be useful,
but they are not automatically truth.

Grounded agent context remains deterministic, lifecycle-aware, explainable, and
assembled from approved or explicitly allowed artifacts. Advisory discovery may
suggest what to review next; it cannot bypass import modes, provenance, safety
filtering, approval, or governed context assembly.

## Non-Negotiable Rules

- filesystem is the canonical source of truth
- SQLite is derived and rebuildable
- grounded context assembly is deterministic and explainable
- retrieval is hybrid at the discovery boundary
- agents may propose changes in v1, but they do not directly write canonical truth
- lifecycle and approval rules are enforced in core

## What This Checkout Contains

This checkout includes working slices across:

- .NET 10 solution and project structure for Core, Storage, Index, Context, API, MCP, and UI
- core artifact schemas, lifecycle rules, validation, editing, approval queue, and diffs
- validation diagnostics that surface code and path context for operators and integrations
- filesystem parsing and persistence for canonical, draft, and summary artifacts
- SQLite rebuild-from-files indexing, relationship indexing, traceability queries, and filesystem-first rebuild diagnostics
- first-run import modes, app-managed workspace placement, and local/GitHub repository attachment metadata
- local Git and GitHub evidence import with stable provenance and idempotent filesystem storage
- secret and privacy filtering before imported evidence persistence
- candidate memory and readiness report generation from imported evidence, with evidence-derived, inferred, and advisory/future-advisory source separation
- deterministic context ranking, cached context packages, bounded relationship traversal, inclusion reasoning, layered context assembly, and a hybrid retrieval boundary for advisory candidate discovery
- serialized project-state views through the shared `GetContextResponse.bundle` contract
- a provider-agnostic external runtime contract reused by MCP and OpenAPI
- a minimal local HTTP API for project lookup, imported readiness, context assembly, proposals, updates, and outcomes
- a thin MCP surface over the shared agent interaction contract, including imported readiness on project resolution
- Machina-to-Memora interaction guidance that keeps runtime execution outside Memora
- Codex and ChatGPT-oriented local workflow samples over the current companion API path
- runtime-facing prototype and compatibility validation for context, proposal, update, and outcome flows across MCP and OpenAPI
- controlled automation policy models, safe trigger evaluation, and a guarded session-summary direct-write prototype
- a styled local operator UI with approval review navigation, first-run import status, bounded GitHub import execution, clearer revision diffs, a context viewer route, and an understanding-output route
- operator workflow guidance for review, draft editing, diff inspection, and rebuild recovery
- sample workspace artifacts that capture the next IDE review boundary work as draft project memory

Important limits still apply:

- canonical truth remains filesystem-first and approval-governed
- controlled automation is limited to explicit policy checks and non-canonical session-summary writes
- no semantic or vector retrieval executes in core v1; hybrid retrieval means advisory discovery plus deterministic governed assembly, not probabilistic core truth
- the UI shows review previews and inactive approval decision controls, but it does not persist approval or rejection decisions
- the first-run UI can execute bounded GitHub evidence import through local `gh` authentication, but it does not promote candidates
- the MCP layer is currently a thin in-process adapter surface, not a production transport host
- provider-facing runtime alignment is shared-contract based; hosted transport, remote reachability, authentication, and provider-specific attachment work remain follow-up scope

## Start Here

If you are orienting yourself in the repo, this order works well:

1. [docs/current-state.md](docs/current-state.md)
2. [docs/architecture.md](docs/architecture.md)
3. [docs/milestones.md](docs/milestones.md)
4. [docs/import-and-workspace-strategy.md](docs/import-and-workspace-strategy.md)
5. [docs/retrieval-evolution.md](docs/retrieval-evolution.md)
6. [docs/operator-workflows.md](docs/operator-workflows.md)
7. [docs/external-runtime-contract.md](docs/external-runtime-contract.md)
8. [src/README.md](src/README.md)
9. [tests/README.md](tests/README.md)

For runtime-facing state view details, read
[docs/project-state-view.md](docs/project-state-view.md) and
[docs/agent-project-state-interpretation.md](docs/agent-project-state-interpretation.md).

## Local Run

Build everything:

- `dotnet build Memora.sln`

Run the UI:

- startup project: `src/Memora.Ui`
- default dev URL: `http://127.0.0.1:5080`
- when no workspace root is configured, the UI uses a writable local copy of `samples/workspaces`

Run the API:

- startup project: `src/Memora.Api`
- default dev URL: `http://127.0.0.1:5081`
- set `MEMORA_WORKSPACES_ROOT` or `Memora:WorkspacesRootPath` to use the file-backed service

Smallest useful validation:

- `dotnet test tests/Memora.Core.Tests/Memora.Core.Tests.csproj`
- `dotnet test tests/Memora.Storage.Tests/Memora.Storage.Tests.csproj`
- `dotnet test tests/Memora.Index.Tests/Memora.Index.Tests.csproj`
- `dotnet test tests/Memora.Context.Tests/Memora.Context.Tests.csproj`
- `dotnet test tests/Memora.Api.Tests/Memora.Api.Tests.csproj`
- `dotnet test tests/Memora.Mcp.Tests/Memora.Mcp.Tests.csproj`
- `dotnet test tests/Memora.Ui.Tests/Memora.Ui.Tests.csproj`

## Repo Map

- [docs/](docs/README.md): architecture, scope, roadmap, and current-state docs
- [samples/](samples/README.md): demo workspaces and fixtures
- [src/](src/README.md): product code by module boundary
- [tests/](tests/README.md): automated validation by module

## Status

Memora is an early product slice with real foundational, integration, and UI
behavior, but some surfaces are intentionally thin and documented as such.

Use [docs/current-state.md](docs/current-state.md) for the most accurate
summary of implemented behavior in this checkout.
