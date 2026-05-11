# Remote Conversational Planning Gap Analysis

## Purpose

This document captures the gap between the current Milestone 10 outcome and a
more important product goal:

- hold a planning conversation in ChatGPT or a similar external client
- let that conversation create or update reviewable Memora artifacts
- make the workflow usable when the operator is away from the local Memora
  machine

It is a roadmap and planning document, not a claim of current capability.

## Target Goal

The desired operator experience is:

1. start a planning conversation from a remote client such as ChatGPT
2. ground that conversation in current Memora project state
3. let the conversation produce reviewable planning artifacts or updates
4. persist those artifacts back into Memora without direct canonical writes
5. review and approve later through normal Memora governance

This is different from a local demo of the shared contract. It is a product
workflow goal with remote-access, write-path, and review implications.

## What Milestone 10 Proved

Milestone 10 proved several important narrow things:

- Memora's shared runtime-facing contract can carry deterministic context,
  proposal submission, and outcome recording
- Codex can exercise one real external workflow through that shared contract
- ChatGPT can consume the shared read model as a read-only client
- proposal and outcome writes can stay non-canonical and approval-governed

Those are useful foundations.

## What Milestone 10 Did Not Prove

Milestone 10 did not prove the operator goal above.

Specifically, it did not prove:

- a remotely reachable Memora instance
- a ChatGPT-accessible write path for proposals or updates
- a conversation-to-artifact planning workflow that feels natural inside
  ChatGPT
- remote review and approval usability
- a secure identity and access model for off-machine clients

The milestone validated the shared contract, not the full product workflow.

## Current Planning Module Status

`Memora.Core.Planning` is present as roadmap-oriented domain groundwork. It
defines structured planning intake, validation, and draft artifact generation
for local tests and future workflow design. It is not wired into the current
API, MCP, UI, IDE extension, or service registration paths, and it is not a
current remote planning-write capability.

Current write-capable integration surfaces use the shared agent interaction
proposal/update contracts. Wiring the planning module into a real operator or
remote-client workflow belongs to Milestone 16, after the use case, review
points, authentication, reachability, and stale-context behavior are specified.

## Gap Summary

### 1. Reachability Gap

Current behavior is local-machine scoped:

- Memora API runs on `127.0.0.1`
- Memora UI runs on `127.0.0.1`
- canonical truth remains on the local filesystem

Result:

- the workflow works only while the local machine is running and reachable
- being "away from the computer" is not solved by the current implementation

### 2. Client Attachment Gap

Current behavior:

- Codex uses the local companion OpenAPI host as the practical operational path
- ChatGPT guidance is intentionally read-only

Result:

- the main desired client, ChatGPT planning conversation, cannot yet create or
  update reviewable Memora planning artifacts as part of the documented
  workflow

### 3. Use-Case Definition Gap

Current M10 work proves protocol behavior, but the exact product use case is
still underspecified.

Questions that need clearer planning:

- what exact planning conversations should create new artifacts versus suggest
  edits to existing ones
- what artifact types should remote conversations be allowed to create first
- whether the remote client should submit a full proposed artifact, a partial
  draft, or a structured planning packet
- what operator review expectation exists after a remote write

Without this use-case definition, implementation risks solving the wrong
problem cleanly.

### 4. Review Workflow Gap

Current behavior:

- artifacts can be proposed
- review UX remains intentionally thin

Result:

- even if a remote client could write proposals, the review and acceptance flow
  for those proposals is not yet optimized for the away-from-computer scenario

### 5. Security And Trust Gap

Any remote planning-write workflow needs explicit answers for:

- who is allowed to write proposals
- how the remote client is authenticated
- how project scope is selected safely
- how approval and audit boundaries remain clear

Current M10 work intentionally does not answer these.

### 6. Freshness And Conflict Gap

A remote planning conversation can easily drift from current project state.

Future workflow design needs to decide:

- how the client refreshes context before submitting updates
- how revision mismatch or stale context should be surfaced
- whether remote planning should prefer new proposal artifacts over direct
  update proposals when state freshness is uncertain

## Why This Matters

The local contract-first M10 result is still valuable, but it is not enough to
validate the real operator outcome.

If Memora is meant to support planning conversations from ChatGPT while the
operator is away from the local machine, the next planning step should optimize
for:

- actual user journeys
- remote reachability assumptions
- write-path safety
- governance fit

rather than for another narrow transport demo.

## Proposed Follow-On Milestone

Suggested milestone theme:

- `Remote Conversational Planning`

Suggested milestone goal:

- define and validate the smallest real workflow in which a remote planning
  conversation can create reviewable Memora artifacts without weakening
  filesystem-first truth or approval governance

### First Issue

Suggested first issue:

- `Use Case Planning For Remote Conversational Writes`

That issue should define:

- primary user journeys
- supported clients and environments
- read versus write expectations per client
- proposal versus update rules
- required review points
- away-from-computer constraints and assumptions

### Why This Should Be First

This issue should precede transport or plugin work because:

- the product goal is broader than the current protocol proof
- remote write paths have governance implications
- client attachment choices should follow the use case, not lead it

### Candidate Follow-On Issues

After use-case planning, likely next issues include:

- remote reachability model selection
- remote client authentication and project scoping
- conversation-to-proposal artifact mapping
- remote review and approval workflow design
- first real ChatGPT write-path prototype

## Recommended Planning Rule

Do not assume that "plugin support" is the core missing piece.

The bigger question is:

- what exact remote planning workflow should Memora support end to end

Only after that is explicit should the repo choose whether the next step is:

- hosted MCP transport
- remote OpenAPI exposure
- plugin packaging
- another intermediate integration shape

## Summary

Milestone 10 proved the shared contract and a local external workflow. It did
not yet prove the remote ChatGPT planning workflow that motivated the work.

The right next milestone should begin with use-case planning so future
implementation is driven by the real operator workflow rather than by transport
shape alone.
