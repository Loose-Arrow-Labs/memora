# Memora.Core

## Purpose
Defines Memora's core domain model and rules.

## Responsibilities
- artifact schemas
- lifecycle rules
- validation primitives
- validation diagnostic messages
- controlled automation policy, trigger, and safety models

## Does NOT contain
- storage logic
- API logic
- MCP logic
- indexing
- UI logic

## Key Areas

- `Artifacts/`: typed artifact models and enums
- `Validation/`: frontmatter, body, id, timestamp, and lifecycle validation
- `Approval/`: approval queue and workflow rules
- `Automation/`: bounded low-risk artifact classes, controlled automation policies, safe triggers, and write safety validation
- `Editing/`: draft-edit behavior
- `Planning/`: internal planning intake validation and draft artifact generation
- `Revisions/`: field-level revision diffs with deterministic areas and display labels
- `AgentInteraction/`: shared contracts used by API and MCP, including the provider-agnostic external runtime boundary

## Current Runtime Alignment Scope

- the shared agent interaction contract remains the single boundary reused by API and MCP
- `AgentInteraction/ExternalRuntimeContract.cs` defines the published runtime-facing operations and governance constraints
- `AgentInteraction/ProjectStateViewSerializer.cs` normalizes the deterministic state view shared by runtime-facing surfaces
- core remains provider-agnostic and does not take on runtime-host responsibilities

## Planning Module Status

`Planning/` is implemented and tested as an internal domain foundation. It can
validate structured planning intake and generate draft artifact documents, but
it is not currently wired into API, MCP, or UI write paths. Current external
write paths use the shared agent interaction proposal/update contracts instead.

Remote conversational planning remains roadmap work tracked in Milestone 16.
See `../../docs/remote-conversational-planning-gap-analysis.md` for the current
gap analysis and expected follow-on shape.
