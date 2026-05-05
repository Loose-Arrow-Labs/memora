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
- `Revisions/`: field-level revision diffs with deterministic areas and display labels
- `AgentInteraction/`: shared contracts used by API and MCP, including the provider-agnostic external runtime boundary

## Current Runtime Alignment Scope

- the shared agent interaction contract remains the single boundary reused by API and MCP
- `AgentInteraction/ExternalRuntimeContract.cs` defines the published runtime-facing operations and governance constraints
- `AgentInteraction/ProjectStateViewSerializer.cs` normalizes the deterministic state view shared by runtime-facing surfaces
- core remains provider-agnostic and does not take on runtime-host responsibilities
