# Operator Workflows

This guide describes the operator workflows supported by the current Memora
implementation. It is intentionally limited to behavior that exists now.

## Ground Rules

- Filesystem artifacts are canonical truth.
- SQLite is a derived index and can be rebuilt from files.
- Drafts and proposals are review inputs, not authoritative project truth.
- The local UI helps operators inspect and edit pending artifacts, but it does
  not persist approval or rejection decisions yet.
- Rebuild diagnostics help operators fix filesystem issues; they do not repair
  files automatically.

## Start The Local UI

Run `src/Memora.Ui` to open the local operator shell. By default it uses a
writable local copy of `samples/workspaces`. Set `MEMORA_WORKSPACES_ROOT` or
`MemoraUi__WorkspacesRoot` to point the UI at another workspace root.

The first screen lists discovered projects. Open a project to see:

- all discovered canonical, draft, and summary artifacts
- the current approval queue from `ApprovalQueueBuilder`
- the first-run import status page for attached repository identity, evidence
  counts, candidate source and disposition, readiness warnings, and next actions
- links into artifact detail and review pages

## Inspect First-Run Import Status

Open `/projects/{projectId}/first-run-import` to inspect the current first-run
import state. The page reads from workspace metadata, stored evidence, and the
first-run readiness report.

The page shows:

- selected import mode
- repository attachment identity and source path or URL
- progress and completion state from stored workspace files
- baseline evidence, canonical evidence, and reviewable evidence counts
- baseline memory, review-needed candidates, and advisory or future-advisory gaps
- candidate provenance, confidence, ambiguity, extraction reason, and disposition
- next actions for review, agent setup, re-import, or later advisory discovery

Changing the visible import-mode selector does not promote artifacts by itself.
Promotion still depends on the governed import, lifecycle, safety, provenance,
and approval rules.

## Review Pending Artifacts

Open a project queue from `/projects/{projectId}/queue`. Pending artifacts are
ordered by the shared core queue rules:

- proposed artifacts before drafts
- older pending timestamps before newer ones
- stable artifact id ordering when timestamps match

Open a review item to inspect:

- queue position and previous/next navigation
- pending revision metadata
- current approved baseline when one exists
- revision diff details when the pending artifact has an approved baseline
- evidence provenance and decision-readiness context for the current pending item

Approval and rejection controls route through the governed core workflow before
filesystem-backed state changes. Approval writes an approved canonical revision
only after lifecycle validation succeeds. Rejection marks the pending draft or
proposal as deprecated in draft storage so it no longer appears as an active
review item.

## Edit Drafts

Open a draft artifact detail page from the artifact browser. The edit form uses
the shared core draft editor and storage writer.

When a draft edit is valid:

- Memora writes a new draft revision under the workspace draft root.
- The existing canonical artifact remains unchanged.
- The saved draft can be inspected again through the project browser and queue.

When a draft edit is invalid:

- no file is written
- validation errors include the code and path for each failure
- the operator should fix the indicated field or body section and retry

## Inspect Revision Diffs

Revision diffs are read-only. They compare a pending candidate against the
latest approved artifact with the same project, id, and type.

Diff rows show:

- the area affected, such as metadata, sections, links, or type-specific fields
- a reviewer-friendly field label
- the raw deterministic field path
- before and after values

No canonical state changes during diff generation or display.

## Handle Rebuild Diagnostics

The SQLite index is rebuilt from filesystem truth. If a rebuild fails, the
result reports:

- how many filesystem projects and artifact files were scanned
- which file and path produced each diagnostic
- the diagnostic code and message
- that SQLite is a derived index, not canonical truth

When diagnostics are present, derived index rows are cleared and not repopulated.
Fix the filesystem artifact or relationship issue, then rebuild again. Do not
treat stale or missing SQLite rows as truth.

Common examples:

- invalid frontmatter or missing required fields in an artifact file
- artifact `project_id` that does not match the workspace project
- duplicate artifact revision files
- approved relationships that point to missing approved target artifacts

## Use Understanding Outputs

The `/understanding` route builds read-only context, traceability, and component
views from current project files.

If traceability output cannot be built because rebuild diagnostics exist, the
page reports the rebuild summary and first diagnostic. Resolve the filesystem
issue first, then rebuild or refresh the understanding output.

## Current Non-Goals

The current workflows do not include:

- direct UI approval persistence
- UI execution of repository import jobs
- automatic canonical writes by agents
- auto-repair of invalid artifacts or indexes
- semantic or vector retrieval in core
- Strata-style broad search

Future automation work must preserve lifecycle, approval, and filesystem-first
authority rules.
