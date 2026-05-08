# Memora

Governed project memory for AI-assisted legacy codebases.

`local-first` · `filesystem truth` · `MCP + OpenAPI` · `.NET 10` · `no vector DB in core`

Memora helps teams use AI agents on existing software without turning tribal
knowledge, stale docs, and runtime guesses into unreviewed truth.

Point Memora at a local or GitHub repository, import bounded project evidence,
generate reviewable project memory, and give agents governed context they can
cite instead of whatever they happen to infer from the current prompt.

## Quick Start

```powershell
git clone https://github.com/alucero270/memora.git
cd memora
dotnet build Memora.sln
```

Run the operator UI:

```powershell
dotnet run --project src/Memora.Ui
```

Open `http://127.0.0.1:5080`.

The UI also binds to loopback by default and uses the same local token file as
the API when both point at the same workspaces root. Browser sessions can open
`http://127.0.0.1:5080/?localToken=<token>` once to set the local session cookie.
Draft edit submissions also carry an antiforgery token from the rendered form,
so cross-site form posts fail before any draft mutation occurs.

Run the companion API in a second terminal:

```powershell
dotnet run --project src/Memora.Api
```

Open `http://127.0.0.1:5081/openapi.json`.

The API binds to loopback by default and rejects non-loopback URL overrides.
It also requires the `X-Memora-Local-Token` header on every request. On first
run, Memora writes the shared local token to
`<workspaces-root>/.memora/local-access-token`; keep that file local to the
current user.

When no workspace root is configured, the UI uses a writable local copy of
`samples/workspaces` so you can inspect the demo project immediately.

## Why Memora Exists

Legacy codebases are hard for agents because the useful context usually lives
between commits, pull requests, issues, conventions, old decisions, test scars,
and half-remembered constraints. Generic agent memory tends to blur those
signals together.

Memora keeps the boundary sharper:

| Generic agent memory | Memora |
| --- | --- |
| Remembers whatever the agent or chat captured | Preserves structured project state with lifecycle rules |
| Can mix facts, guesses, and preferences | Separates evidence, inferred meaning, and approved truth |
| Often opaque or ranking-driven | Keeps grounded context deterministic and explainable |
| Usually tied to one agent/runtime | Exposes shared MCP and OpenAPI contract surfaces |
| May become stale silently | Keeps provenance, review state, and approval status visible |

The goal is not to make agents "remember more." The goal is to make project
understanding durable, inspectable, and governed enough that agent work becomes
easier to trust.

## First Ten Minutes

The current M10 first-run path is aimed at a narrow but useful legacy-repo
onboarding loop:

1. create or select an app-managed Memora workspace
2. attach one local Git or GitHub repository as a source
3. choose an import mode before promotion behavior matters
4. import bounded evidence with secret and privacy filtering
5. generate candidate memory and an agent readiness report
6. inspect baseline evidence, baseline memory, and review-needed candidates
7. expose project identity, readiness, and grounded context through MCP/OpenAPI

Try the current demo status page:

```text
http://127.0.0.1:5080/projects/demo-project/first-run-import?importMode=fast_baseline
```

The deeper legacy understanding story remains roadmap work: large-scale
imports, approval persistence in the UI, provider setup packs, hosted
transports, observability/replay, and richer candidate conversion belong to
later milestones. Use [docs/current-state.md](docs/current-state.md) for the
exact implemented state and [docs/milestones.md](docs/milestones.md) for
roadmap intent.

## How It Works

Memora is a local app and governance layer, not an agent runtime.

```text
source repo / GitHub evidence
          |
          v
 app-managed Memora workspace
          |
          +-- canonical/   approved artifacts only
          +-- drafts/      proposals and reviewable work
          +-- evidence/    imported source material
          +-- summaries/   derived reports
          +-- indexes/     rebuildable SQLite state
          |
          v
 deterministic context + readiness state
          |
          v
 MCP / OpenAPI / operator UI
```

Core rules:

- filesystem is the canonical source of truth
- SQLite is derived and rebuildable
- approved artifacts are canonical truth
- drafts, proposals, imported evidence, and generated candidates are not
  approved truth
- lifecycle and approval rules are enforced below UI, MCP, and OpenAPI
- agents may propose changes in v1, but they do not directly write canonical
  truth

## Retrieval Model

Memora uses hybrid retrieval at the discovery boundary and governed assembly at
the agent-context boundary.

Broad discovery can find candidates from imported evidence, repository scans,
GitHub signals, or future advisory providers. Those candidates can be useful,
but they are not automatically truth.

Grounded agent context remains deterministic, lifecycle-aware, explainable, and
assembled from approved or explicitly allowed artifacts. Advisory discovery may
suggest what to review next; it cannot bypass import modes, provenance, safety
filtering, approval, or governed context assembly.

## What This Checkout Contains

Implemented slices include:

- Core, Import, Storage, Index, Context, API, MCP, and UI projects
- strongly typed artifact schemas, lifecycle rules, validation, approval queue,
  revision diffs, and diagnostics
- filesystem parsing and persistence for canonical, draft, summary, and
  evidence artifacts
- SQLite rebuild-from-files indexing, relationship indexing, and traceability
  queries
- first-run import modes, app-managed workspace placement, and local/GitHub
  repository attachment metadata
- local Git and GitHub evidence import with stable provenance and idempotent
  filesystem storage
- secret and privacy filtering before imported evidence persistence
- candidate memory and readiness report generation from imported evidence, with
  evidence-derived, inferred, and advisory/future-advisory source separation
- deterministic context ranking, inclusion reasoning, bounded relationship
  traversal, layered context assembly, and cached derived context packages
- runtime-facing project-state serialization through the shared
  `GetContextResponse.bundle` contract
- local HTTP endpoints for project lookup, imported readiness, context
  assembly, proposals, updates, and outcomes
- a thin MCP adapter over the shared agent interaction contract, including
  imported readiness on project resolution
- a styled local operator UI with review previews, first-run import status,
  context viewer, and understanding-output routes
- Codex and ChatGPT-oriented local workflow samples over the current companion
  API path

## Intentional Limits

These are current boundaries, not accidents:

- canonical truth remains filesystem-first and approval-governed
- no semantic or vector retrieval executes in core v1
- hybrid retrieval means advisory discovery plus deterministic governed
  assembly, not probabilistic core truth
- the first-run UI is status and inspection only; it does not execute imports
  or promote candidates on `main`
- the UI shows review previews and inactive approval decision controls, but it
  does not persist approval or rejection decisions yet
- the MCP layer is currently a thin in-process adapter surface, not a
  production transport host
- provider-facing runtime alignment is shared-contract based; hosted transport,
  remote reachability, authentication, and provider-specific attachment work
  remain follow-up scope
- controlled automation is limited to explicit policy checks and
  non-canonical session-summary writes

## Project Map

| Path | Purpose |
| --- | --- |
| [docs/](docs/README.md) | Architecture, scope, roadmap, and current-state docs |
| [src/](src/README.md) | Product code by module boundary |
| [tests/](tests/README.md) | Automated validation by module |
| [samples/](samples/README.md) | Demo workspaces and fixtures |

Start with:

1. [docs/current-state.md](docs/current-state.md)
2. [docs/architecture.md](docs/architecture.md)
3. [docs/milestones.md](docs/milestones.md)
4. [docs/import-and-workspace-strategy.md](docs/import-and-workspace-strategy.md)
5. [docs/retrieval-evolution.md](docs/retrieval-evolution.md)
6. [docs/operator-workflows.md](docs/operator-workflows.md)
7. [docs/external-runtime-contract.md](docs/external-runtime-contract.md)
8. [docs/project-state-view.md](docs/project-state-view.md)
9. [docs/agent-project-state-interpretation.md](docs/agent-project-state-interpretation.md)

## Validation

Build everything:

```powershell
dotnet build Memora.sln
```

Run focused test projects:

```powershell
dotnet test tests/Memora.Core.Tests/Memora.Core.Tests.csproj
dotnet test tests/Memora.Storage.Tests/Memora.Storage.Tests.csproj
dotnet test tests/Memora.Index.Tests/Memora.Index.Tests.csproj
dotnet test tests/Memora.Context.Tests/Memora.Context.Tests.csproj
dotnet test tests/Memora.Import.Tests/Memora.Import.Tests.csproj
dotnet test tests/Memora.Api.Tests/Memora.Api.Tests.csproj
dotnet test tests/Memora.Mcp.Tests/Memora.Mcp.Tests.csproj
dotnet test tests/Memora.Ui.Tests/Memora.Ui.Tests.csproj
```

## Status

Memora is an early product slice with real foundational, integration, import,
and UI behavior, but some surfaces are intentionally thin and documented as
such.

Use [docs/current-state.md](docs/current-state.md) for the most accurate
summary of implemented behavior in this checkout.
