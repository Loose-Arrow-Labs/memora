# Architecture

## Overview

Memora is a local-first, structured memory and governance system for
AI-assisted software development.

Core tenets:

- Filesystem = canonical truth
- SQLite = derived index (rebuildable)
- Deterministic retrieval in v1
- Approval-gated lifecycle
- MCP-first integration with an OpenAPI companion

## High-Level Components

Clients and runtimes (Claude, ChatGPT, Codex, UI)
-> `Memora.Api` / `Memora.Mcp`
-> application services that orchestrate use cases
-> core domain rules and schemas
-> storage and index layers
-> project workspaces and artifacts

## Layers And Responsibilities

### 1. Core Domain (`Memora.Core`)

Purpose: define the rules of truth.

Responsibilities:

- artifact schemas, types, and enums
- lifecycle model and transitions
- validation primitives
- link and relationship definitions
- approval queue and approval workflow rules
- planning intake and proposal content contracts
- controlled automation policy, trigger, and safety models
- shared external runtime contract definitions

Constraints:

- no I/O
- no API concerns
- deterministic and side-effect free behavior

### 2. Storage (`Memora.Storage`)

Purpose: persist and read canonical data.

Responsibilities:

- Markdown plus frontmatter parsing
- file layout for canonical, draft, and summary content
- revision handling
- filesystem abstractions

Constraints:

- no business rules beyond validation hooks
- no ranking or retrieval logic

### 3. Index (`Memora.Index`)

Purpose: provide fast lookup over canonical data.

Responsibilities:

- SQLite schema for projects, artifacts, links, and revisions
- indexing metadata and relationships
- rebuild-from-files processing
- direct, dependency, and impact traceability queries over approved artifacts

Constraints:

- must be fully rebuildable from the filesystem
- must not make source-of-truth decisions

### 4. Context (`Memora.Context`)

Purpose: build deterministic context packages.

Responsibilities:

- layered context assembly
- deterministic ranking
- inclusion reasoning
- bounded relationship traversal
- derived context package caching
- context assembly for agents and UI

Constraints:

- no persistence
- no API controllers

### 5. API (`Memora.Api`)

Purpose: expose Memora capabilities locally over HTTP.

Responsibilities:

- project lookup endpoint
- context endpoint
- proposal endpoint
- update proposal endpoint
- outcome recording endpoint
- OpenAPI surface

Constraints:

- delegate to services
- do not duplicate core rules
- do not persist approval or rejection decisions outside governed workflows

### 6. MCP (`Memora.Mcp`)

Purpose: expose Memora through MCP as the primary integration layer.

Responsibilities:

- tools for context retrieval, artifact proposals, update proposals, and outcome recording
- resource template for project lookup
- protocol metadata for request, response, and error contract shapes

Constraints:

- protocol adaptation only
- no business logic
- currently thin in-process adapter, not a hosted transport runtime

### 7. UI (`Memora.Ui`)

Purpose: provide a local operator interface.

Responsibilities:

- project selection
- artifact browsing and editing
- approval queue and review preview
- context inspection at `/context-viewer`
- read-only understanding output at `/understanding`

Constraints:

- no duplication of domain logic
- current decision controls are intentionally inactive until UI persistence is implemented end to end

## Workspace Model

A workspace represents a project.

```text
<workspace-root>/
  canonical/
    charters/
    decisions/
    plans/
    constraints/
    questions/
    outcomes/
    repo/
  drafts/
  summaries/
  project.json
```

Rules:

- only approved artifacts live in `canonical/`
- canonical artifact revisions are append-only markdown files
- drafts and proposals live in `drafts/`
- summaries are supporting and non-canonical
- workspaces typically live outside the product repo
- `samples/` may contain demo workspaces

## Artifact Model

- artifacts are stored as Markdown with strict frontmatter
- frontmatter is authoritative
- body content is structured but human-readable

Lifecycle:

`proposed -> draft -> approved -> superseded/deprecated`

Rules:

- agents may only propose in v1
- canonical changes require approval
- revisions are append-only, not silent overwrite

## Retrieval Model (v1)

### Layered Context

Layer 1:

- charter, active plan, and repo snapshot anchors when present

Layer 2:

- approved or explicitly allowed supporting artifacts selected by deterministic ranking and focus context

Layer 3:

- optional supporting history, including session summaries and inactive plans, when requested

### Deterministic Ranking

Factors:

- artifact type priority
- canonical status
- tag/task relevance
- relationship proximity
- recency
- direct match strength

No semantic or vector retrieval belongs in core v1.

## Integration Model

### MCP (Primary)

- tools for read and proposal operations
- project resource lookup

### OpenAPI (Companion)

- exposes the current local HTTP companion routes:
  - `GET /api/projects/{projectId}`
  - `POST /api/context`
  - `POST /api/artifacts/proposals`
  - `POST /api/artifacts/updates`
  - `POST /api/outcomes`

Rule:

Integration layers never bypass lifecycle or approval.

## Separation Of Concerns

- Memora: canonical project memory and governance
- Strata: external and broad retrieval, separate from core

Rule:

External retrieval is never truth unless it is promoted and approved in Memora.

## Non-Goals (v1)

- semantic or vector search
- probabilistic ranking
- direct agent writes to canonical state
- tight coupling to any provider SDK

## Operational Guarantees

- filesystem is always the ground truth
- SQLite can be rebuilt at any time
- validation rejects invalid artifacts
- lifecycle transitions are enforced
- context assembly is explainable

## Extension Points

- graph and relationship enhancements that preserve deterministic retrieval
- external advisory retrieval integration behind a clear boundary
- controlled automation policies
- runtime integrations such as Machina

## Summary

Memora enforces structured, approved, and durable project cognition. It
separates truth from retrieval, proposal from approval, and domain rules from
integration layers.
