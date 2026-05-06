# External Runtime Contract

## Purpose

This document defines the shared Memora contract that external runtimes must
use when they need project context or need to submit reviewable proposals.

It describes the current contract boundary. It does not turn Memora into an
execution runtime, orchestration layer, or provider-specific adapter host.

## Core Boundary

External runtimes interact with Memora through the shared agent interaction
contract in `src/Memora.Core/AgentInteraction/`.

The current published definition lives in:

- `src/Memora.Core/AgentInteraction/AgentInteractionContract.cs`
- `src/Memora.Core/AgentInteraction/ExternalRuntimeContract.cs`

That shared contract is reused by:

- `Memora.Mcp` as the primary provider-facing surface
- `Memora.Api` as the companion HTTP and OpenAPI surface

External runtimes must adapt to that contract. They do not redefine lifecycle
rules, retrieval behavior, approval behavior, or truth semantics.

## Contract Version

- version: `memora.runtime.v1`
- primary surface: MCP
- companion surface: OpenAPI

The version is intentionally about the shared runtime-facing contract boundary,
not about any specific provider integration.

## Supported Operations

### Project Lookup

- purpose: resolve the project identity before runtime-facing calls proceed
- request:
  - MCP resource: `memora://projects/{projectId}`
  - OpenAPI route: `GET /api/projects/{projectId}`
- response contract: `ProjectLookupResponse`
- behavior: read-only

Project lookup also carries imported project readiness when available:

- repository attachments for local Git or GitHub source evidence
- first-run readiness report path
- baseline, canonical, and reviewable evidence counts
- evidence-derived, inferred, advisory, and future-advisory candidate counts
- missing context, missing tests, risky modules, advisory discovery gaps, and
  next review steps
- whether grounded context is ready for agent setup

### Grounded Context Retrieval

- request contract: `GetContextRequest`
- response contract: `GetContextResponse`
- MCP tool: `get_context`
- OpenAPI route: `POST /api/context`
- behavior: read-only governed context assembled deterministically with
  explainable inclusion reasoning

#### Project-State View Fit

The deterministic project-state view defined in
`docs/project-state-view.md` is carried by the existing
`GetContextResponse.bundle` shape.

That means:

- the state view already fits the shared runtime contract
- `get_context` remains the published read operation for project-state output
- no additional top-level `get_project_state` operation is required in M9

The state view is the serialized bundle returned by the current contract, not a
second runtime-facing state model.

### New Artifact Proposal

- request contract: `ProposeArtifactRequest`
- response contract: `ProposalResponse`
- MCP tool: `propose_artifact`
- OpenAPI route: `POST /api/artifacts/proposals`
- behavior: creates a reviewable proposed artifact and does not mutate approved
  canonical truth directly

### Existing Artifact Update Proposal

- request contract: `ProposeUpdateRequest`
- response contract: `ProposalResponse`
- MCP tool: `propose_update`
- OpenAPI route: `POST /api/artifacts/updates`
- behavior: creates a reviewable proposed revision and does not mutate approved
  canonical truth directly

### Outcome Recording

- request contract: `RecordOutcomeRequest`
- response contract: `OutcomeResponse`
- MCP tool: `record_outcome`
- OpenAPI route: `POST /api/outcomes`
- behavior: records a reviewable non-canonical outcome artifact without writing
  approved truth directly

## Governance Constraints

Every external runtime integration must preserve these rules:

- filesystem-backed approved artifacts remain canonical truth
- SQLite remains derived and rebuildable
- external runtimes may create reviewable proposals, but they do not directly
  write canonical artifacts
- approval-governed lifecycle rules remain in force after runtime-facing calls
- imported or advisory discovery can expose candidate status, but grounded
  context retrieval remains deterministic, explainable, lifecycle-aware, and
  non-semantic in core v1
- Memora remains structured memory and governance, not an execution runtime
- provider-specific behavior must stay outside Memora core services

## Explicit Non-Goals

This contract does not mean:

- hosted runtime orchestration now lives inside Memora
- provider-specific adapters belong in `Memora.Core`
- external runtimes can bypass approval for convenience
- Memora now provides semantic, vector, or probabilistic retrieval in core
- Strata responsibilities move into Memora

## How To Use This Contract

When an external runtime needs Memora:

1. Resolve the target project.
2. Request grounded context through the shared contract.
3. Produce a reviewable proposal or outcome through the shared contract when
   needed.
4. Leave canonical truth unchanged until a human approval flow accepts the
   change.

That sequence keeps runtime integrations aligned with current Memora
responsibilities.
