# Retrieval Evolution

This document explains how retrieval evolves in Memora without changing the
core truth model.

It is intentionally narrower than the general roadmap. Use
`current-state.md` for implemented behavior in the current checkout and
`milestones.md` for sequencing. This file exists to make the retrieval
boundary explicit so future work does not blur shipped deterministic behavior
with deferred exploration.

## Retrieval Rules That Do Not Change

Memora retrieval remains governed by the same core rules:

- filesystem-backed approved artifacts are canonical truth
- SQLite is a derived index and can be rebuilt from files
- retrieval in core v1 is deterministic and explainable
- agents do not write canonical truth directly
- Memora is not a broad search system or execution runtime

These rules apply even when retrieval becomes faster or can inspect more of the
approved relationship graph.

## Hybrid Retrieval Decision

Memora's retrieval strategy is hybrid, but the word hybrid does not mean
semantic search inside core v1.

Hybrid retrieval means:

- deterministic governed assembly for final agent context
- deterministic evidence scans for directly observed repo and GitHub facts
- optional advisory discovery for candidate material, kept outside core truth
- provenance, confidence, ambiguity, and review state on every generated
  candidate
- lifecycle and approval before candidate meaning becomes canonical memory

The practical model is:

1. discover broadly from evidence and later advisory sources
2. store or present candidates with provenance and review state
3. assemble grounded agent context deterministically from approved or explicitly
   allowed Memora artifacts

This lets Memora benefit from broad discovery without letting broad discovery
become truth.

## Shipped Retrieval Behavior

The current retrieval path in `Memora.Context` includes:

- deterministic ranking with stable ordering
- explicit inclusion reasoning for selected artifacts
- layered context bundle assembly
- derived context package caching keyed by request shape and loaded artifact
  fingerprints
- bounded typed relationship traversal for focus proximity

These improvements deepen retrieval and reduce repeated work, but they do not
change what counts as truth or how inclusion is justified.

### Inclusion Reason Labels

The current set of inclusion reason codes returned by the reasoner:

- `approved-default` — included because approved artifacts are the default
  context grounding in v1.
- `draft-explicitly-allowed` — included because the request opted in to
  drafts or proposals.
- `noncanonical-history` — included as supporting non-canonical history
  rather than canonical truth.
- `layer1-charter-anchor`, `layer1-active-plan-anchor`, `layer1-repo-anchor`,
  `layer3-supporting-history` — layer-specific anchor reasons.
- `explicit-focus-artifact` — included because the request named this
  artifact by id.
- `related-focus-artifact`, `traversed-focus-artifact` — included because
  an explicit stored relationship (direct or bounded traversal) connects to
  a focused artifact.
- `milestone-relevance` — included because the artifact matches the same
  milestone markers as the request.
- `request-keyword-overlap` — included because at least one request keyword
  appears in this artifact's title, tags, sections, or body. This is honest
  about what the deterministic ranker measures: keyword presence weighted
  by where the term appears (title > tags > headings > body). It is not a
  semantic match.
- `request-keyword-strong-match` — included because the keyword overlap is
  strong enough that at least one request term hits a title or tag, not
  only a body sentence. Emitted in addition to `request-keyword-overlap`
  when the ranker's direct-match score crosses an internal threshold.

The previous `direct-task-match` code is replaced by the two keyword-overlap
codes above. The change is purely a relabeling for honesty; the underlying
deterministic ranking behavior is unchanged. The old label overstated the
strength of the connection — a body-only token hit was reported with the
same label as a title hit. The new labels are explicit about what the
ranker actually found.

## What Caching Changes

Cached context packages are a derived convenience only.

- cache keys are fingerprinted from normalized request values and the loaded
  artifact set
- changes to artifact content, metadata, lifecycle state, relationships,
  sections, or relevant request inputs produce a different cache key
- cached bundles preserve the same context shape and reasoning as an uncached
  build

Caching improves efficiency. It does not create a second source of truth.

## What Relationship Traversal Changes

Relationship traversal makes deterministic retrieval richer without becoming
fuzzy.

- traversal follows explicit stored relationships
- traversal stays bounded and lifecycle-aware
- direct and traversed relationship paths are available for inclusion reasoning
- focus proximity is still reproducible for the same inputs

Traversal adds grounded depth. It is not graph exploration for its own sake,
and it does not replace deterministic ranking rules.

## What Stays Out Of Core V1

The following remain out of scope for core Memora retrieval:

- semantic retrieval
- vector storage or vector search
- probabilistic ranking
- treating retrieval results as canonical truth
- Strata-style broad search inside Memora core

This boundary matters more than the specific retrieval technique. Memora must
preserve what the system knows, not what the system guesses.

## Future Exploration Boundary

Future retrieval exploration must stay outside core v1 unless it preserves the
same governance model and the hybrid boundary above.

The optional retrieval extension contract is a boundary for future candidate
discovery providers. It is not active core behavior.

That means any later advisory discovery layer would need to remain:

- non-canonical
- disabled by default
- clearly separate from deterministic core retrieval
- unable to bypass lifecycle or approval rules
- validated back through approved artifacts and normal context assembly before
  it affects grounded Memora output

If semantic or broad retrieval is explored later, it should be treated as an
external advisory concern rather than as a replacement for Memora's core
deterministic retrieval path.

## Practical Reading

For the current implementation and adjacent boundaries, read these together:

- `docs/current-state.md`
- `docs/architecture.md`
- `docs/integration-strategy.md`
- `src/Memora.Context/README.md`

## Summary

Retrieval evolution in Memora means making governed context assembly faster,
richer, and easier to explain while adding hybrid discovery around it. It does
not mean shifting Memora core toward semantic search, vector indexing, or
probabilistic project memory.
