# Private Beta Readiness Evaluation

## Purpose

This document captures an end-to-end product evaluation of Memora performed
against the demo workspace on 2026-05-13. It is the source material that
informs the Private Beta Readiness milestone and the issues filed against it.

It is a snapshot of one walkthrough. Not all paths were exercised. Specifically,
the first-run import flow against a real repository, the Codex external
workflow sample, the IDE review extension, and large-workspace performance
were not covered.

## What Was Exercised

- Booted `Memora.Api` and `Memora.Ui` against a writable copy of
  `samples/workspaces/demo-project`.
- Resolved local-access token authentication for both services.
- `GET /api/projects/demo-project` — project lookup with readiness state.
- `POST /api/context` — context bundle for the task description "add caching
  strategy for context packages".
- `POST /api/artifacts/proposals` — artifact proposal submission for a new
  decision `ADR-099`.
- `POST /api/artifacts/updates` — revision submission for the same artifact.
- UI routes: project list, project detail, queue, proposals, review,
  trust dashboard, first-run import, understanding, context viewer.
- Approve flow on an existing draft (`ADR-007`) — observed canonical write
  on the filesystem.

## What Worked Well

- **Filesystem truth.** Every artifact created through the API landed as a
  human-readable `.md` file with strict frontmatter. No hidden state.
- **Lifecycle enforcement.** Strict ID format, frontmatter validation, and
  rejected illegal status transitions all behaved as documented.
- **Markdown plus YAML as the interop format.** Diffable in git, readable
  by humans, parseable by LLMs without a custom client.
- **UI conceptual surface.** Queue, review-with-diff, evidence provenance,
  trust summary, and first-run import status all expose the right concepts.
- **Rebuildable SQLite index.** Architecturally sound; nothing tried during
  this evaluation depended on hidden index state.
- **Provider-agnostic shared contract.** MCP and OpenAPI mirror the same
  service surface, which keeps integrations honest.

## What Did Not Work, Or Felt Wrong

### 1. The proposed-to-draft path is missing

This is the most significant finding.

An agent's proposal lands with `status: proposed`. The operator UI's review
page for a proposed artifact shows the **Approve** button as disabled (per
the documented lifecycle: `proposed -> approved` is not allowed). The only
active control is **Reject**.

There is no UI control to promote a proposed artifact to draft. There is no
API endpoint to do so. `propose_update` against a proposed artifact creates
another `proposed` revision, not a `draft`.

The practical consequence: an operator cannot approve agent work through the
governed flow. They must hand-edit the YAML frontmatter
(`status: proposed` to `status: draft`) and then use the UI to approve. This
breaks the central agent loop the product is designed around.

### 2. Context inclusion reasons are misleading

For the task "add caching strategy for context packages", the context bundle
returned the project charter and three approved decisions about Phase 2
milestone planning and vendor contracts. None of the returned Layer 2
artifacts were about caching.

Each Layer 2 result carried the inclusion reason `direct-task-match`. The
documented ranking is deterministic and explainable, which is correct in
spirit, but the *labels* attached to results do not match the actual
selection logic. `direct-task-match` is currently applied to results that
were not, in fact, selected because they matched the task.

This is honest-by-architecture and dishonest-by-label, and the label is what
agents and operators read.

### 3. The artifact schema is heavy for the value it returns today

The schema enforces:

- eight artifact types (charter, plan, decision, constraint, question,
  outcome, repo_structure, session_summary)
- type-specific required body sections (for example, Context / Decision /
  Alternatives Considered / Consequences for an ADR)
- strict prefix-format IDs (`ADR-NNN`, `CNS-NNN`, etc.)
- required `provenance` and `reason` fields on every proposal

In the evaluation, the first proposal attempt was rejected for using
`ADR-EVAL-1` (does not match the ADR prefix format). For the proposal to
succeed, the caller had to know the body section names, the ID format, and
the type-specific frontmatter fields (for example, `decision_date` for an
ADR) in advance.

For a regulated organization that has decided to standardize, the schema is
appropriate. For the audience the product can plausibly reach today (solo
developers and small teams trying to keep their agents grounded), the schema
is friction that arrives before any value has been demonstrated.

### 4. Setup friction is unforced

The evaluation lost about twenty minutes to local-access token issues caused
by three independent sources:

- The token file is written with a UTF-8 BOM. A naive shell `cat` of the
  file produces a value that fails comparison until the BOM is stripped.
- `Memora.Api` and `Memora.Ui` default to *different* workspaces and *different*
  token stores when no configuration is provided.
- On Windows, the .NET `Path.GetFullPath("/tmp/...")` resolves to
  `C:\tmp\...` (drive-rooted), while a Git Bash `/tmp` resolves to the user
  temp directory. The two services and the shell can end up looking at
  different filesystem locations for the same logical path.

These are individually small. Together they are the kind of paper cut that
makes a new user assume the product is unfinished and walk away.

### 5. Three names exist for the workspace root configuration

- environment variable `MEMORA_WORKSPACES_ROOT`
- configuration key `Memora:WorkspacesRootPath`
- UI-specific configuration key `MemoraUi:WorkspacesRoot`

The right name to use depends on which service is being configured and how
the service is launched. The README and operator docs do not currently call
this out as a single canonical entry point.

### 6. The marginal value over hand-edited markdown is unclear at small scale

The proposal submitted via `propose_artifact` produced a markdown file under
`drafts/decision/` with strict frontmatter. The same file could have been
produced by `vim drafts/decision/ADR-099.md` plus `git commit`. For a solo
developer with a well-maintained `AGENTS.md`, the cost of running the full
service stack to get a governed `.md` file is high relative to the value
added today.

Memora's value grows when:

- multiple agents and human reviewers are working on the same project memory
- a structured trust dashboard is needed for stale drafts, broken
  relationships, or missing context
- imported repository evidence converts into reviewable candidate memory
  faster than manual reading allows
- the same context bundle must be served to many clients deterministically

The first-run import flow and the trust dashboard are the strongest pitches
for that incremental value. Neither was exercised end-to-end in this
evaluation.

## Verdict

The core thesis is right. Durable, governed, filesystem-first project memory
that AI agents and human reviewers share is a real and growing need. The
filesystem-plus-rebuildable-index plus MCP architecture is a defensible
foundation.

The current product surface is over-built relative to its value delivery
today. Specifically:

- The lifecycle and schema layer is built for a regulated organization but
  ships with a broken proposed-to-draft path that would block solo and
  small-team adoption.
- The context-assembly layer's honest framing is undermined by labels that
  imply more selection intelligence than exists.
- The integration layer (MCP, OpenAPI, IDE review) is the closest path to
  real-world value, and it is currently gated by the lifecycle gap.

For a solo developer evaluating Memora today, a well-maintained `AGENTS.md`
plus a handful of markdown files under `docs/` is competitive on
time-to-value with the full service stack. That competition is closer than
it should be given the engineering effort invested.

The fix is not to lower the bar on rigor. It is to keep the rigor as an
opt-in tier and ship a lower-ceremony default that closes the agent loop
end-to-end.

## Recommended Direction Before Beta

Tracked as issues under the Private Beta Readiness milestone. In order of
impact per hour of work:

1. **Close the agent loop.** Add a `proposed -> draft` promotion in the
   review UI and through a corresponding API endpoint. Without this, the
   review workflow has a hole in the middle.
2. **Ship a low-ceremony artifact type.** A `note` or `memo` type with no
   required body sections, no ID prefix format, and no type-specific
   frontmatter. Keep the eight typed schemas as an opt-in for users who
   want the discipline.
3. **Stop validating IDs against a fixed prefix format on intake.** Either
   assign IDs server-side or accept any string that follows a stable
   filename rule.
4. **Make context inclusion labels honest.** Either implement keyword and
   tag matching properly, or remove the `direct-task-match` label and ship
   the default ordering as `approved-default` only.
5. **Unify configuration and authentication.** One workspace root setting,
   one token store, one bootstrap path shared between `Memora.Api` and
   `Memora.Ui`.
6. **Polish the ten-minute first-run import demo.** Attach a real public
   repository, scan it, produce candidate memory, show the trust dashboard
   reflecting real state. This is the strongest existing pitch for adoption
   and needs to be reproducible by a new user without instructions beyond a
   short walkthrough.

## What This Document Does Not Claim

- That Memora's engineering quality is the problem. It is not.
- That the lifecycle model is wrong. The model is right; the missing
  transition path is the problem.
- That the integration surface (MCP, OpenAPI) is wrong. The shape is right;
  it is currently gated by the lifecycle gap.
- That the import or remote workflow is broken. Those paths were not
  exercised in this evaluation and the evaluation has nothing to say about
  them.

## Boundary

This evaluation reflects one walkthrough on one machine on one day. It is
deliberately a point-in-time check before opening Memora to private beta
users. It should be re-run, in full, after the Private Beta Readiness
milestone closes.
