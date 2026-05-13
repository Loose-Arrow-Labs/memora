# Codex Handoff — Trusted-Circle Beta

## Purpose

This document is the scoped handoff that picks up where the previous session
left off. It tells the next Codex run exactly what is already done, what is in
scope for the Trusted-Circle Beta milestone, and what is intentionally out of
scope.

## Operating Contract

Before doing anything, read:

- `AGENTS.md`, including the new section 18 "Using Memora During Agent Work"
- `README.md`
- `docs/current-state.md`
- `docs/private-beta-readiness-eval.md`
- `docs/operator-workflows.md`
- `docs/installer-package.md`
- `src/Memora.Ui/README.md`

Skim only as needed:

- `docs/claude-integration.md`
- `docs/codex-integration.md`
- `docs/external-runtime-contract.md`

Follow `AGENTS.md` exactly. Section 18 is binding: use Memora as governed
project memory, not as chat history or an execution runtime. Specifically:

- Read approved Memora artifacts before relying on drafts, proposals,
  imported evidence, summaries, or prior conversation.
- Treat approved artifacts as canonical truth.
- Treat drafts, proposals, evidence, summaries, and retrieval results as
  review inputs, not truth.
- Never write canonical Memora artifacts directly.
- If durable knowledge is discovered, capture it as a reviewable proposal,
  update, or outcome through governed Memora surfaces.
- Keep handoff notes explicit: what came from approved memory, what came
  from code, what changed, what remains uncertain, and what should be
  proposed for approval.

## What Already Landed (Do Not Redo)

Five PRs have already merged to `main`. Do not re-implement any of the
following:

- **PR #376** `feature/get-started-and-installer-package` — `/get-started`
  GET and `/get-started/project` POST routes that create a workspace
  skeleton and optionally attach a local Git repository through the
  existing `RepositoryAttachmentService`. Home hero rebuilt around "Local
  project memory" with a Get started CTA. Windows portable package
  (`build/package-windows.ps1`, `docs/installer-package.md`) with
  installer, uninstaller, and start scripts. AGENTS.md section 18 added.
  This handoff doc lives here.
- **PR #377** `feature/honest-inclusion-reasons` — replaces the misleading
  `direct-task-match` inclusion reason code with `request-keyword-overlap`
  (any positive score) and a new `request-keyword-strong-match` (score
  crosses an internal title-or-tag-hit threshold). Deterministic ranker
  behavior unchanged. Closes PBR-04 (#359). 39/39 `Memora.Context.Tests`
  pass.
- **PR #378** `feature/promote-proposed-to-draft` — adds
  `ArtifactApprovalWorkflow.Promote(...)` and an
  `ArtifactApprovalDecisionResult.PromotedArtifact` slot, a
  `OperatorReviewDecision.Promote` value, a service handler that rewrites
  the draft file with the promoted artifact, and a "Promote to draft"
  button on the review page when the artifact is proposed. 3 new
  workflow tests + 2 new UI smoke tests. Closes PBR-01 (#356). **The
  agent loop now closes through the UI.**
- **PR #379** `feature/note-artifact-type` — adds a ninth artifact type,
  `note` (`NTE-NNN` prefix, no required body sections, no type-specific
  frontmatter, `TypePriority = 0` in the deterministic ranker). Saved at
  `drafts/note/` while pending and `canonical/notes/` once approved. 3
  new factory tests. Closes PBR-02 (#357).
- **PR #380** `feature/operator-ui-vocab-pass` — operator review-page
  vocab audit: "Non-Canonical Proposal" -> "Pending review",
  "Decision Readiness" -> "What this needs before approval",
  "Non-canonical" status cell -> "Pending review", and the
  "Current UI boundary" + "Current workflow scope" footer panels removed
  from every page (`RenderScopeNote` is now a no-op stub). Test
  assertions updated to forbid the removed strings. Partial close on
  PBR-11 (#366); the issue stays open in case dogfooding finds more
  strings.

Base your work on updated `main` (all five PRs above are merged).

## In-Scope: Trusted-Circle Beta Milestone

GitHub milestone: **Trusted-Circle Beta** (number 20). After the five PRs
above landed PBR-01, PBR-02, PBR-04, and PBR-11, three issues remain on
the milestone:

1. **#372 PBR-17** Finish the in-product attach-a-project flow. The
   local-repo half is done (PR #376). The GitHub side is not. For
   trusted-circle beta, GitHub CLI guidance plus a "paste a personal
   access token" form is acceptable; full OAuth is explicitly
   post-beta.
2. **#371 PBR-16** Filesystem-style hierarchical navigation
   (`Memora > Project > {Agent resources, Artifacts, Project root}`).
   The biggest piece left and the one that reshapes how the rest of
   the UI reads. Worth doing before PBR-06 because the walkthrough
   should show the new nav.
3. **#361 PBR-06** Polish the ten-minute first-run walkthrough end to
   end against a real public repository. Use this to validate the
   full stack: PR #376 attach -> PR #378 promote -> PR #380 vocab ->
   new nav from #371 -> import flow.

If PBR-11 (#366) is reopened during the walkthrough because the audit
found more strings, treat that as scope creep on this milestone and
either land a follow-up PR or push it to the strangers-beta tier.

Issue order matters because:

- PBR-17 is the most user-felt gap remaining (attach a GitHub repo
  from inside the UI). Land it first so the walkthrough has something
  real to drive.
- PBR-16 reshapes navigation. PBR-06's walkthrough should reflect the
  new nav, so land it before the walkthrough.
- PBR-06 is last because it validates the rest end to end.

## Explicit Out-of-Scope

Do **not** start the following in this session. They are tracked on the
Private Beta Readiness Gate milestone (number 19) and are the strangers-beta
tier:

- PBR-03 relaxed ID validation
- PBR-05 unified workspace/token config
- PBR-07 external integration sweep
- PBR-08 non-self-referential demo workspace
- PBR-09 actionable diagnostic hints in the UI
- PBR-10 first-five-seconds welcome (the home hero partial in #376 is the
  trusted-circle floor; do not redesign further yet)
- PBR-12 information hierarchy audit
- PBR-13 single local app (the portable package is the trusted-circle floor)
- PBR-14 feature-discoverable landing
- PBR-15 cross-platform installer
- PBR-18 real first-run auth UX
- PBR-19 Agent resources section content
- PBR-20 Project root section content

Do not implement GitHub OAuth, hosted MCP transport, automatic import
execution, or direct canonical agent writes. They are explicitly not features
yet.

## Working Mode

- One issue per PR unless the milestone is being run as an approved stacked
  workflow.
- Each PR must build (`dotnet build Memora.sln`) and pass the touched test
  projects.
- Each PR must update `docs/current-state.md` only after behavior actually
  changes.
- Each PR must use a GitHub closing keyword (`Closes #N`) when it fully
  satisfies its issue; `References #N` only for partial work.
- AGENTS.md section 16 (unattended stacked milestone) is available if the
  user explicitly requests it. Otherwise use the default workflow in
  section 15.

## Validation Required Before Closing Each Issue

Build:

```
dotnet build Memora.sln
```

Targeted tests (run those that match the change):

```
dotnet test tests/Memora.Ui.Tests/Memora.Ui.Tests.csproj
dotnet test tests/Memora.Api.Tests/Memora.Api.Tests.csproj
dotnet test tests/Memora.Core.Tests/Memora.Core.Tests.csproj
dotnet test tests/Memora.Context.Tests/Memora.Context.Tests.csproj
dotnet test tests/Memora.Storage.Tests/Memora.Storage.Tests.csproj
dotnet test tests/Memora.Index.Tests/Memora.Index.Tests.csproj
dotnet test tests/Memora.Import.Tests/Memora.Import.Tests.csproj
```

Manual checks against the local UI, for the relevant issue:

- Home: a new user can understand what Memora is and reach `/get-started`.
- Get started: workspace creation and local repo attach produce a clear
  success or a clear validation error.
- Project view: matches a recognizable folder structure (Memora > project >
  agent resources / artifacts / project root once PBR-16 lands).
- Approve flow: proposed -> draft -> approved is reachable through the UI
  without hand-editing YAML frontmatter (once PBR-01 lands).
- GitHub guidance: honest about what works today; no dead ends.

## Memora-As-Memory Dogfooding (Required)

Per AGENTS.md section 18, this work itself should be governed by Memora when
the workspace is available:

- Before each issue, read the relevant approved artifacts in the demo
  workspace via the local API and reference them in the PR body.
- When a durable design decision is made during implementation (for example,
  "we are deferring full GitHub OAuth to post-beta and the form will take a
  personal access token instead"), propose it as an artifact through
  `POST /api/artifacts/proposals` and reference the proposed artifact in the
  PR body.
- If propose-then-approve does not yet work end to end through the UI (which
  is the entire point of PBR-01), record the limitation in the PR body and
  do not silently hand-edit YAML frontmatter to fake an approval.

## Constraints (Architecture Boundaries)

Per AGENTS.md sections 3 through 10:

- Filesystem is source of truth.
- SQLite is derived and rebuildable.
- Approved artifacts are canonical truth.
- Agents only propose in v1.
- No direct canonical writes without governed approval.
- No vector or semantic retrieval in core v1.
- MCP remains the primary integration layer; OpenAPI mirrors it.
- Do not implement beyond current milestone or beta-readiness scope.
- Do not claim future features exist.

## Deliverable per Issue

For each completed issue:

- A focused PR that targets `main` (or the previous stack branch under an
  approved stacked workflow).
- A PR body that records:
  - what changed
  - which PBR issue is closed and how each acceptance criterion is satisfied
  - which approved Memora artifacts were read during the work
  - any proposed artifacts that should be promoted before the next session
  - what remains intentionally unsupported, with a one-line reason

## Stop Conditions

Stop and report (do not guess past) if any of the following occurs:

- The issue requires architecture changes not yet documented.
- A dependency on an unfinished PR blocks completion.
- The proposed-to-draft path (PBR-01) is needed before PBR-04, 11, or 17
  can be honestly closed and PBR-01 is not yet merged.
- A required test cannot be made to pass within issue scope.

When stopping, hand off back with:

- The issue number that blocked progress.
- The specific blocker.
- A recommended next action.

## If You Have Time for Only One Thing

Land PBR-17 (#372 — GitHub side of attach-a-project). The agent loop
already closes thanks to PR #378. The next-largest user-felt gap is
"I want to use Memora on my GitHub repo and the UI does not help me
get there." A personal-access-token paste form is sufficient for
trusted-circle beta; OAuth is post-beta.
