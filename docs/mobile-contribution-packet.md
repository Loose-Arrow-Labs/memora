# Mobile Contribution Packet

## Purpose

This document defines the portable packet that mobile capture produces and
desktop Memora ingests as non-canonical input.

It is planning material for Milestone 13 - Mobile Contribution MVP. The packet
shape is a draft and is expected to change as M10 import and M11 review flows
settle.

## Scope

The packet must support the four mobile contribution types from the milestone:

- questions
- decision drafts
- planning notes
- proposal drafts

The packet must keep canonical truth untouched. Desktop ingest treats every
mobile packet as non-canonical planning or proposal input until a human
reviewer takes it through the existing approval flow.

This document does not define:

- mobile UI behavior
- transport (clipboard, file export, shared folder, sync)
- desktop import code paths
- approval UI or review queue ordering

Those belong to the other Milestone 13 issues.

## Format Decision

The packet is a single UTF-8 text file:

- `.md` file with YAML frontmatter
- frontmatter carries the structured envelope
- body carries the human-readable contribution

Rationale:

- Markdown plus frontmatter is the existing Memora artifact shape. Reusing it
  keeps mobile output legible to humans and parseable by the existing
  `Memora.Storage` markdown parser.
- A pure JSON packet would force mobile authors to escape and re-encode prose.
  That works against the "fast capture" goal of the mobile MVP.
- A dual-format packet (JSON envelope plus separate Markdown body) was
  considered and rejected for v1. Two files multiply transport friction
  (clipboard, share sheet, shared folder) without adding structure that
  frontmatter cannot already carry.

A future revision may introduce a compressed multi-packet container if
batching becomes necessary. v1 stays single-file.

## File Conventions

- file extension: `.md`
- encoding: UTF-8, no BOM
- line endings: LF
- recommended file name: `<created_at_compact>-<intent>-<short_slug>.md`
  - example: `20260512T1841Z-question-cache-eviction.md`
- file name is a transport convenience only, not the packet identity

## Envelope Schema

Frontmatter is authoritative. All envelope fields live in frontmatter.

Required fields:

- `packet_version: 1`
- `packet_id: string` (mobile-generated, stable per packet, recommended UUID v4)
- `created_at: string` (ISO-8601 UTC, with `Z` suffix)
- `source: mobile` (must equal the literal `mobile` in v1)
- `intent: question | decision_draft | planning_note | proposal_draft`
- `lifecycle_target: planning_input | proposal_draft`
  - `planning_input` for questions and planning notes
  - `proposal_draft` for decision drafts and proposal drafts
- `canonical: false` (must equal the literal `false`)

Optional fields:

- `title: string` (short human-readable label; falls back to first body heading
  if omitted)
- `device_label: string` (free-text capture device or app version hint, no PII
  requirement)
- `target_project_hint: string` (operator-supplied workspace name or repo slug;
  mobile may not be able to resolve a real `project_id`)
- `tags: [string]`
- `proposed_artifact_type: charter | plan | decision | constraint | question | outcome | repo_structure | session_summary`
  - only meaningful when `intent` is `decision_draft` or `proposal_draft`
  - acts as a hint for desktop intake; the human reviewer still decides the
    final artifact type during review

Reserved field names that mobile packets MUST NOT set:

- `status`
- `revision`
- `project_id`
- `id`
- `approved_at`, `approved_by`, or any approval marker
- any artifact-specific frontmatter from `docs/artifact-schema.md`

These belong to canonical artifacts. A mobile packet that carries them is
rejected by desktop intake with a clear diagnostic. This is the structural
guarantee that prevents a mobile packet from being mistaken for a canonical
artifact during import.

## Body Schema Per Intent

The body is Markdown. Each intent has a recommended structure. The structure
is a recommendation, not a hard schema, because mobile capture is
small-screen and the v1 desktop import path treats the body as planning input
prose rather than canonical artifact content.

### question

Recommended sections:

```markdown
## Question

## Context

## Possible Directions
```

Maps loosely to the canonical Open Question artifact body in
`docs/artifact-schema.md`. Desktop intake should not require a `## Resolution`
section from mobile, because mobile is the question source, not the answer.

### decision_draft

Recommended sections:

```markdown
## Context

## Decision Direction

## Alternatives Considered

## Open Concerns
```

Mirrors the canonical Architecture Decision body loosely. Desktop intake treats
this as a draft proposal until reviewed; it is not a real ADR until a human
reviewer promotes it through the approval flow.

### planning_note

Recommended sections:

```markdown
## Note

## Why This Matters
```

Free-form. Intended for short captures the operator wants to retain as
planning input without committing to a decision or proposal shape.

### proposal_draft

Recommended sections:

```markdown
## Summary

## Proposed Content

## Notes For Review
```

When `proposed_artifact_type` is set, `## Proposed Content` should sketch the
required body sections for that artifact type from `docs/artifact-schema.md`
in plain prose. Desktop review is responsible for shaping the sketch into a
real proposed artifact.

## Non-Canonical Semantics

The packet shape preserves Memora's truth model by design:

- `canonical: false` is required and validated on intake
- canonical-style fields are forbidden in mobile packets
- `lifecycle_target` is restricted to non-canonical values
- desktop intake maps mobile packets onto the existing non-canonical planning
  or proposal review path; it does not write canonical artifacts
- promotion to a canonical artifact requires the existing review and approval
  flow, not a mobile signal

A mobile packet is never approved. Approval only happens on the desktop, on
the artifact that the reviewer derives from the packet.

## Validation Expectations

Desktop intake should validate at least:

- packet is a UTF-8 markdown file with parseable YAML frontmatter
- all required envelope fields are present
- `packet_version` equals a value the current desktop intake understands
- `source` equals `mobile`
- `canonical` equals `false`
- `intent` and `lifecycle_target` are paired correctly
- no reserved canonical-artifact fields are present
- body is non-empty
- `proposed_artifact_type`, if present, is a known artifact type and is only
  set when `intent` is `decision_draft` or `proposal_draft`

Invalid packets must fail intake with explicit diagnostics. Partial intake of
an invalid packet is not allowed. Concrete intake behavior and diagnostic
codes belong to M13-04.

## Example Packet

```markdown
---
packet_version: 1
packet_id: 5f0e1a3a-9c1c-4f6d-8b8e-cb6f6d1d51a1
created_at: 2026-05-12T18:41:00Z
source: mobile
intent: question
lifecycle_target: planning_input
canonical: false
title: Cache eviction policy for context packages
device_label: pixel-9-memora-capture-0.1
target_project_hint: memora
tags:
  - context-cache
  - retrieval
---

## Question

How should derived context package cache entries be evicted when the
underlying approved artifacts change revision?

## Context

The current cache is keyed by request shape and loaded artifact fingerprints.
Revision churn during review may invalidate cached packages more often than
we expect.

## Possible Directions

- evict on any approved-revision change in the loaded fingerprint set
- evict only when the changed artifact participated in the cached layer
- expose a manual cache-flush action in the operator UI
```

## Open Questions

These are flagged for revision after M10 and M11 settle:

- whether `target_project_hint` should be replaced by a stronger workspace
  identity once Memora supports remote project resolution
- whether `proposed_artifact_type` should be required for `proposal_draft` or
  remain optional
- whether v1 should support attaching plain-text excerpts as fenced blocks
  inside the body (recommended) versus separate attachment fields (deferred)
- whether the packet should later carry a signed integrity field once a real
  trust boundary between mobile and desktop exists

## Boundary Reminder

This packet is non-canonical planning or proposal input. It is not project
truth. Desktop import and human review remain the only paths into canonical
Memora state.
