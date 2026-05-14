Implement Milestone 1 — Artifact Schema and Validation for the Memora project.

Project context

Memora is a local-first structured memory and governance system for AI-assisted software development.

Locked architecture

- Language: C#
- Filesystem = canonical source of truth
- SQLite = derived local index
- MCP = primary integration layer
- OpenAPI = companion integration layer
- Strata is a separate system and must not be mixed into Memora core responsibilities

Core rules

- Only approved artifacts are canonical truth
- Agents may only propose changes in v1
- Retrieval must remain deterministic and explainable in v1
- No semantic/vector retrieval in core v1
- Do not invent behavior outside documented scope

Current task

Build the artifact schema model, validation layer, lifecycle validation, and markdown/frontmatter parsing foundation for Memora.

This task is limited to the schema and validation foundation. Do not build API endpoints, MCP integration, UI, or SQLite indexing yet unless strictly required to support schema tests.

---

Locked artifact format

Artifacts are stored as Markdown files with strict frontmatter.

Frontmatter is authoritative

Body content is human-readable, but schema validation must enforce frontmatter correctness.

Shared base schema

All artifacts must support:

- "id: string"
- "project_id: string"
- "type: charter | plan | decision | constraint | question | outcome | repo_structure | session_summary"
- "status: proposed | draft | approved | superseded | deprecated"
- "title: string"
- "created_at: string" (ISO-8601 UTC)
- "updated_at: string" (ISO-8601 UTC)
- "revision: integer"
- "tags: []"
- "provenance: string"
- "reason: string"
- "links:"
  - "depends_on: []"
  - "affects: []"
  - "derived_from: []"
  - "supersedes: []"

Base validation rules

- "id" must be non-empty
- "project_id" must be non-empty
- "type" must be one of the allowed enums
- "status" must be one of the allowed enums
- "title" must be non-empty
- "revision >= 1"
- timestamps must parse as valid ISO-8601 UTC values
- "links" may only contain the allowed relationship keys
- linked values must be artifact IDs, not titles

---

Artifact-specific schemas

1. Project Charter

Body sections required:

- "## Problem Statement"
- "## Primary Users / Stakeholders"
- "## Current Pain"
- "## Desired Outcome"
- "## Definition of Success"

2. Plan / Milestone

Additional frontmatter:

- "priority: low | normal | high"
- "active: true | false"

Body sections required:

- "## Goal"
- "## Scope"
- "## Acceptance Criteria"
- "## Notes"

At least one acceptance criterion must exist.

3. Architecture Decision

Additional frontmatter:

- "decision_date: string"

Body sections required:

- "## Context"
- "## Decision"
- "## Alternatives Considered"
- "## Consequences"

4. Constraint

Additional frontmatter:

- "constraint_kind: technical | product | workflow | operational | integration"
- "severity: low | normal | high | critical"

Body sections required:

- "## Constraint"
- "## Why It Exists"
- "## Implications"

5. Open Question

Additional frontmatter:

- "question_status: open | resolved | deferred"
- "priority: low | normal | high"

Body sections required:

- "## Question"
- "## Context"
- "## Possible Directions"
- "## Resolution"

"Resolution" may be empty while question is open.

6. Outcome / Lesson

Additional frontmatter:

- "outcome: success | failure | mixed"

Body sections required:

- "## What Happened"
- "## Why"
- "## Impact"
- "## Follow-up"

7. Repo Structure Snapshot

Additional frontmatter:

- "snapshot_source: manual | generated"

Body sections required:

- "## Root"
- "## Key Directories"
- "## Key Files"
- "## Notes"

8. Session Summary

Additional frontmatter:

- "session_type: planning | review | execution | retrospective"
- "canonical: false"

Body sections required:

- "## Summary"
- "## Artifacts Created"
- "## Artifacts Updated"
- "## Open Threads"

Session summaries are supporting artifacts only and must not be treated as canonical truth by themselves.

9. Note

Notes are the low-ceremony catch-all type. Use this when a thought,
observation, or pending finding does not yet fit one of the eight typed
artifacts above. The typed schemas remain opt-in for users who want their
structure; the `note` type is for the 80% case where the discipline of a
typed schema would be friction before any value has been demonstrated.

Additional frontmatter:

- (none)

Body sections required:

- (none) — any non-empty body is acceptable.

Notes still participate in the standard lifecycle (`proposed -> draft ->
approved -> superseded/deprecated`) and the standard relationship model
(`depends_on`, `affects`, `derived_from`, `supersedes`). They are saved to
`canonical/notes/` once approved and to `drafts/note/` while pending review.
The deterministic context ranker treats notes as the lowest-priority type
so any typed approved artifact ranks above a note when both are eligible.

The id prefix for notes is `NTE` (`NTE-001`, `NTE-002`, etc.).

---

Lifecycle rules

Allowed transitions:

- "proposed -> draft"
- "draft -> approved"
- "draft -> deprecated"
- "approved -> superseded"
- "approved -> deprecated"

Not allowed:

- "proposed -> approved"
- "approved -> draft"
- direct overwrite of approved artifact without creating a new revision

---

ID strategy

Use stable human-readable IDs with type prefixes:

- "CHR-001"
- "PLN-001"
- "ADR-001"
- "CNS-001"
- "QST-001"
- "OUT-001"
- "REP-001"
- "SUM-001"

Do not implement full ID generation service unless required; support validation of IDs and leave generation simple if needed.

---

File naming rule

Filename should include the ID, but filename is not the authoritative identity.

Example:

- "ADR-001-use-sqlite-as-derived-index.md"

The artifact ID in frontmatter is the real identifier.

---

Implementation expectations

Create the schema/validation foundation in a clean, modular way.

Suggested areas:

- domain models for artifacts
- enums for types/statuses/relationship keys
- frontmatter parsing
- markdown body section validation
- lifecycle transition validator
- artifact validation result model

Do not over-abstract.

Prefer:

- explicit validators
- strongly typed models
- small focused classes

Avoid:

- speculative plugin systems
- persistence layer work beyond what is needed to parse/validate files
- API/controller code
- MCP or OpenAPI implementation
- SQLite work in this task

---

Testing requirements

Use a test-aware approach.

Tests are required for:

- base schema validation
- enum validation
- timestamp validation
- lifecycle transition validation
- artifact-specific required-section validation
- invalid frontmatter rejection
- invalid relationship-key rejection

Good test cases:

- valid charter parses and validates
- plan missing acceptance criteria fails
- invalid status enum fails
- invalid timestamp fails
- "proposed -> approved" transition fails
- session summary with "canonical: true" fails

---

Acceptance criteria

- [ ] Markdown + frontmatter parsing exists for Memora artifacts
- [ ] Shared base schema is implemented and validated
- [ ] Artifact-specific validation exists for all 8 artifact types
- [ ] Lifecycle transition validation exists and enforces allowed transitions
- [ ] Invalid frontmatter and invalid enum values are rejected cleanly
- [ ] Required body sections are validated per artifact type
- [ ] Tests exist for the main schema and lifecycle rules
- [ ] Implementation does not include unrelated API, MCP, UI, or SQLite work

---

Output expectations

Return:

1. A short summary of the implementation
2. The files created or changed
3. Any assumptions made
4. Any follow-up issues that should be created next

Keep the implementation honest and narrow.