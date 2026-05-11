# Data Model

## Overview

This document defines how Memora represents, relates, and uses data at the
system level.

It complements the artifact schema by describing:

- how artifacts behave
- how they relate to each other
- how they participate in lifecycle, retrieval, and indexing

## Core Concepts

### 1. Project

A project is the top-level container for all Memora state.

A project contains:

- canonical artifacts
- draft artifacts
- session summaries
- project metadata

Each project is isolated.

### 2. Artifact

Artifacts are the atomic units of structured project memory.

Properties:

- strongly typed
- versioned
- lifecycle-managed
- linked to other artifacts

Artifacts represent:

- intent and plans
- decisions
- constraints
- questions
- outcomes
- structure

### 3. Artifact States

Artifacts exist in one of the following states:

- `proposed`
- `draft`
- `approved`
- `superseded`
- `deprecated`

Behavior:

- only approved artifacts are canonical
- drafts are editable
- proposed artifacts originate from agents or ingestion
- proposed artifacts must be explicitly accepted into draft review before
  approval
- superseded artifacts remain for traceability

### 4. Artifact Relationships

Artifacts are connected through typed relationships:

- `depends_on`
- `affects`
- `derived_from`
- `supersedes`

Rules:

- relationships are directional
- relationships must reference valid artifact IDs
- relationships influence retrieval and ranking

## Artifact Graph

Memora forms a directed graph of artifacts.

- nodes = artifacts
- edges = typed relationships

This graph is:

- human-readable through links
- machine-usable through the index

## Lifecycle Model

Flow:

`proposed -> draft -> approved -> superseded/deprecated`

Behavior rules:

- proposed artifacts are not used in canonical retrieval
- proposed artifacts transition to draft through an explicit human review
  decision before approval
- draft artifacts may be inspected but not treated as truth
- approved artifacts form the canonical project state
- superseded artifacts remain accessible but are deprioritized

## Workspace Model

Filesystem layout:

```text
<workspace>/
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

- `canonical/` contains only approved artifacts
- `drafts/` contains proposed and editable artifacts
- `summaries/` contains supporting session-level information
- persisted artifact revisions are append-only markdown files
- the filesystem is the source of truth

## Index Model

SQLite provides a derived representation of the filesystem.

Indexed entities:

- projects
- artifacts
- relationships
- revisions

Rules:

- the index must be rebuildable from the filesystem
- the index cannot introduce new truth
- the index is used for query efficiency only

## Retrieval Model

Memora uses layered retrieval.

### Layer 1 - Core State

Always included when available:

- Project Charter
- active plan
- repo structure snapshot

### Layer 2 - Relevant Context

Filtered artifacts:

- decisions
- constraints
- questions
- outcomes

Selection is based on deterministic ranking.

### Layer 3 - Extended Context

Optional:

- historical artifacts
- deeper relationship traversal

## Ranking Model (v1)

Ranking is deterministic.

Factors:

- artifact type priority
- lifecycle state
- milestone relevance
- relationship proximity
- recency
- direct match to task

Rules:

- identical inputs must produce identical ordering
- ranking must be explainable

## Context Package Model

Context is returned as a structured package.

It contains:

- Layer 1 core artifacts
- Layer 2 ranked artifacts
- metadata explaining selection

Rules:

- context must not include drafts unless explicitly allowed
- context must not invent data

## Proposal Model

Agents interact through proposals.

Proposal types:

- new artifact
- update to an existing artifact
- outcome recording

Rules:

- proposals enter the `proposed` state
- proposals must pass schema validation
- proposals must not overwrite canonical artifacts

## Revision Model

Artifacts are versioned.

Rules:

- each approved change creates a new revision
- previous revisions remain traceable
- superseded artifacts must reference their successor

## Event Model

Key events:

- `planning_imported`
- `artifact_proposed`
- `artifact_approved`
- `artifact_rejected`
- `artifact_superseded`
- `context_requested`
- `outcome_recorded`
- `index_rebuilt`

Events drive:

- workflows
- future automation

## Separation Of Concerns

Memora:

- structured memory
- lifecycle governance
- deterministic retrieval

Strata:

- external and broad retrieval

Rule:

External data must be promoted and approved before becoming canonical.

## System Guarantees

- filesystem is authoritative
- the index is rebuildable
- lifecycle is enforced
- retrieval is deterministic
- context is explainable

## Summary

Memora treats project knowledge as structured, versioned, linked, and
lifecycle-controlled. The data model ensures that memory is not just stored,
but behaves correctly within planning, execution, and agent interaction
workflows.
