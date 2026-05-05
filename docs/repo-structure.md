# Memora Repo Structure

This document describes the current tracked repository layout and the intended
module boundaries. It is a navigation aid, not a source of canonical project
memory.

## Current Layout

```text
memora/
  .github/
    ISSUE_TEMPLATE/
    workflows/
  artifacts/
    README.md
  build/
    README.md
  docs/
    adr/
    change_orders/
    *.md
  samples/
    workflows/
    workspaces/
      demo-project/
        canonical/
        drafts/
        summaries/
        project.json
    README.md
  src/
    Memora.Api/
    Memora.Context/
    Memora.Core/
    Memora.Index/
    Memora.Mcp/
    Memora.Storage/
    Memora.Ui/
    README.md
  tests/
    Memora.Api.Tests/
    Memora.Context.Tests/
    Memora.Core.Tests/
    Memora.Index.Tests/
    Memora.Mcp.Tests/
    Memora.Storage.Tests/
    Memora.Ui.Tests/
    README.md
  AGENTS.md
  CONTRIBUTING.md
  Directory.Build.props
  Memora.sln
  README.md
```

## Top-Level Folder Intent

- `artifacts/`: generated build outputs, packaged deliverables, and temporary validation artifacts. Do not store canonical Memora project memory here.
- `build/`: repository-level build helpers, validation scripts, and bootstrap support.
- `docs/`: planning, architecture, scope, current-state, integration, and operational documentation.
- `samples/`: demo workspaces, fixture artifacts, and local workflow scripts.
- `src/`: product code split by responsibility.
- `tests/`: automated tests grouped by module.
- `.github/`: issue templates and CI workflows.

## Product Projects

### `src/Memora.Core`

Purpose: define Memora's domain rules and shared contracts.

Current responsibilities:

- artifact schemas, enums, links, and typed documents
- lifecycle validation and transition rules
- validation diagnostics
- approval queue and approval workflow logic
- draft editing and revision diff behavior
- planning intake models and validation
- controlled automation policy, trigger, catalog, and safety models
- shared agent interaction and external runtime contracts

Must not contain:

- file I/O
- SQLite logic
- API, MCP, or UI behavior
- provider-specific runtime code

### `src/Memora.Storage`

Purpose: parse and persist artifact files.

Current responsibilities:

- Markdown plus strict frontmatter parsing
- markdown section extraction
- artifact markdown writing
- canonical, draft, and summary persistence
- workspace discovery through `project.json`

Must not contain:

- ranking
- API or MCP logic
- canonical-truth decisions beyond invoking shared validation behavior

### `src/Memora.Index`

Purpose: maintain the derived SQLite index.

Current responsibilities:

- SQLite schema creation
- rebuild-from-files indexing
- rebuild diagnostics
- relationship persistence and lookup
- direct, dependency, and impact traceability queries

Must not contain:

- canonical truth decisions
- provider-specific integrations
- lifecycle bypasses

### `src/Memora.Context`

Purpose: build deterministic context packages.

Current responsibilities:

- layered context bundle models
- deterministic ranking
- inclusion reasoning
- bounded relationship traversal for focus proximity
- derived context package caching
- disabled-by-default optional retrieval extension boundaries

Must not contain:

- storage or file parsing
- API controllers
- semantic or vector retrieval in core v1

### `src/Memora.Api`

Purpose: expose a thin local OpenAPI-compatible companion service.

Current responsibilities:

- `GET /api/projects/{projectId}`
- `POST /api/context`
- `POST /api/artifacts/proposals`
- `POST /api/artifacts/updates`
- `POST /api/outcomes`
- OpenAPI document at `/openapi.json`
- file-backed agent interaction service when a workspace root is configured
- guarded non-canonical session-summary write prototype behind policy checks

Must not contain:

- duplicated lifecycle rules
- duplicated ranking logic
- production deployment assumptions
- direct canonical write paths for agents

### `src/Memora.Mcp`

Purpose: expose the primary provider-facing MCP adapter surface.

Current responsibilities:

- tool definitions for `get_context`, `propose_artifact`, `propose_update`, and `record_outcome`
- project resource template `memora://projects/{projectId}`
- request, response, and error contract metadata
- forwarding to the shared `IAgentInteractionService` boundary

Must not contain:

- business logic beyond protocol adaptation
- hosted transport claims that are not implemented

### `src/Memora.Ui`

Purpose: provide the local operator interface.

Current responsibilities:

- project selection
- artifact browsing
- draft editing
- approval queue and review preview
- context viewer route at `/context-viewer`
- understanding output route at `/understanding`
- seeded writable sample root when no workspace root is configured

Must not contain:

- lifecycle rules duplicated from core
- indexing logic
- approval/rejection persistence claims beyond what is implemented

## Tests

The solution currently includes these test projects:

- `tests/Memora.Core.Tests`
- `tests/Memora.Storage.Tests`
- `tests/Memora.Index.Tests`
- `tests/Memora.Context.Tests`
- `tests/Memora.Api.Tests`
- `tests/Memora.Mcp.Tests`
- `tests/Memora.Ui.Tests`

Use `tests/README.md` for the current validation map.

## Workspace Note

Actual Memora-managed project workspaces should live outside the product source
repo by default.

Use `samples/workspaces/` only for:

- example data
- fixture artifacts
- parser, index, and rebuild testing
- demos
- captured sample project memory that illustrates current or planned workflows

## Rule

Keep module boundaries strict. If a behavior belongs to core lifecycle,
validation, retrieval, storage, protocol adaptation, or UI rendering, keep it
inside the matching project and reference it from other layers instead of
duplicating it.
