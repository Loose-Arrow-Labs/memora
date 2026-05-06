# Integration Strategy

## Purpose

This document explains how Memora's provider-facing integration surfaces fit
together in the current implementation.

It is intentionally about interface strategy, not product roadmap. Use
`current-state.md` for what is implemented now and `milestones.md` for what is
planned next.

## Core Rules

- MCP remains Memora's primary provider-facing integration layer
- OpenAPI remains a local companion surface
- filesystem-backed artifacts remain the canonical source of truth
- SQLite remains a derived index and must stay rebuildable
- agent writes remain proposal-only in v1
- retrieval remains deterministic and explainable in v1
- provider-specific guidance must not move business logic into protocol layers

## Current Surfaces

### MCP

Current MCP exposure lives in `src/Memora.Mcp/Server/MemoraMcpServer.cs`.

The current implementation is a thin adapter over `IAgentInteractionService`
and currently exposes:

- tool: `get_context`
- tool: `propose_artifact`
- tool: `propose_update`
- tool: `record_outcome`
- resource: `memora://projects/{projectId}` with repository attachment and
  imported readiness state

MCP is the preferred path for provider workflows that can use Memora's
provider-facing protocol directly.

### OpenAPI Companion

Current HTTP exposure lives in `src/Memora.Api/Program.cs`.

The current implementation registers these routes:

- `GET /api/projects/{projectId}`
- `POST /api/context`
- `POST /api/artifacts/proposals`
- `POST /api/artifacts/updates`
- `POST /api/outcomes`

The API host is intentionally thin. It is useful for local tooling and
environments that need HTTP access, but it does not replace MCP as Memora's
primary provider-facing surface.

## Shared Contract Boundary

Both provider-facing surfaces reuse the shared agent interaction contract in
`src/Memora.Core/AgentInteraction/AgentInteractionContract.cs`.

The current external runtime boundary is summarized in
`docs/external-runtime-contract.md` and anchored by
`src/Memora.Core/AgentInteraction/ExternalRuntimeContract.cs`.

That contract defines:

- project lookup responses, including imported project readiness summaries
- governed context bundle requests and responses
- proposal requests for new artifacts
- proposal requests for updates
- outcome recording requests
- shared validation and error shapes
- a provider-agnostic external runtime contract definition for MCP and OpenAPI

This shared contract is the boundary that keeps business behavior aligned across
MCP and OpenAPI. Integration layers adapt the contract; they do not redefine
lifecycle rules, ranking logic, or canonical storage behavior.

## Governance And Write Rules

Provider-facing integrations must preserve Memora's governance model.

- approved artifacts remain canonical truth
- draft and proposed artifacts are non-canonical
- provider-facing write operations create reviewable proposals
- current surfaces do not grant direct canonical writes
- approval and lifecycle rules remain enforced below the protocol boundary

This means provider guidance must never describe MCP or OpenAPI flows as if
agents can silently overwrite approved project memory.

## Retrieval And Context Rules

Provider-facing integrations must also preserve Memora's retrieval model.

- context comes from deterministic context assembly
- inclusion reasoning must remain explainable
- no semantic or vector retrieval belongs in core v1
- protocol layers must not inject provider-specific ranking behavior
- imported readiness may expose hybrid discovery status, including
  evidence-derived, inferred, advisory, and future-advisory candidate counts,
  but it does not promote those candidates into grounded context

Human-readable understanding outputs can build on these grounded context and
relationship foundations, but they must remain downstream of shared Memora
logic rather than becoming provider-specific report generators.

## Recommended Usage Pattern

### Prefer MCP When

- the caller can consume a provider-facing tool and resource surface
- the workflow benefits from Memora's primary integration layer
- the environment does not require HTTP as the transport boundary

### Use OpenAPI When

- the environment needs a local HTTP interface
- the workflow is better served by explicit request and response payloads
- a local operator tool is integrating alongside, not instead of, MCP

### Keep Provider Guidance Separate

Provider-specific setup notes should live in dedicated docs and should:

- map back to the shared MCP or OpenAPI surfaces
- explain configuration, shell usage, and limits honestly
- avoid claiming custom provider adapters unless they actually exist

The current Machina-facing guidance is documented separately in
`docs/machina-interaction-model.md` so the runtime interaction story stays
explicit without moving runtime responsibilities into Memora itself.

This separation keeps provider guidance practical without letting it redefine
Memora's architecture.

## Current Limitations

The current provider-facing implementation is intentionally thin.

- MCP is not yet documented as a complete hosted transport runtime
- OpenAPI is a local companion surface, not a production deployment story
- context assembly exists, but human-readable understanding outputs remain a
  separate concern
- traceability-oriented understanding remains bounded by the currently
  implemented relationship and retrieval capabilities

These limits should be stated plainly in provider-facing docs.

## Validation Expectations

When provider-facing contracts change:

- update this strategy doc if the architectural story changes
- keep MCP and OpenAPI behavior aligned through shared contract reuse
- cover important request, response, and error paths with focused integration
  validation
- keep documentation wording consistent with actual code and tests

## Summary

Memora's integration strategy is MCP-first, OpenAPI-companion, and
approval-governed. Provider-facing guidance should help external tools connect
to shared Memora capabilities without moving truth, retrieval, or lifecycle
rules out of the core product.
