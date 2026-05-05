# Memora.Ui

## Purpose
Provides the local operator interface for Memora.

## Responsibilities
- project selection
- artifact browsing
- draft editing
- approval queue
- approval review navigation
- context inspection
- understanding output inspection

## Does NOT contain
- lifecycle rules
- indexing logic

## Current Shell
- runs as a styled local ASP.NET Core shell
- reads workspace files through shared core and storage services
- uses a writable local copy of `samples/workspaces` unless `MemoraUi__WorkspacesRoot` or `MEMORA_WORKSPACES_ROOT` is set
- supports project selection, artifact browsing, draft editing, approval review previews, queue navigation, decision-readiness context, a context viewer route, and an understanding-output route

## Design Guidance
- future UI work should follow the shared component-system guidance in `../../docs/ui-design-system.md`
- reusable controls, panels, navigation, tables, badges, and workflow sections should be treated as composable UI parts rather than one-off page styling
- responsive behavior should be designed into reusable components before they are used across routes

## Honest Scope
- draft edits write new draft revisions to the selected workspace root
- approval review surfaces are wired to current core diff and queue behavior
- revision diffs show deterministic areas, display labels, raw paths, and before/after values
- approval and rejection persistence are intentionally not claimed in this slice
- understanding outputs remain read-only and are grounded in the shared context builder plus rebuildable traceability queries
- no IDE review surface is implemented here; the current IDE review boundary only exists as draft/sample planning state

## Key Areas

- `Program.cs`: host wiring, default workspace root behavior, and route registration
- `Operator/LocalOperatorWorkspaceService.cs`: project, artifact, queue, edit, and review models for the operator shell
- `Rendering/OperatorShellPageRenderer.cs`: styled operator shell HTML rendering
- `ContextViewer/FileSystemContextViewerService.cs`: shared context-builder-backed viewer at `/context-viewer`
- `Understanding/FileSystemUnderstandingOutputService.cs`: read-only understanding page at `/understanding`

## Local Development
- the default development launch profile listens on `http://127.0.0.1:5080`
- run it alongside `Memora.Api` on `http://127.0.0.1:5081` to avoid local port collisions
- point `MemoraUi__WorkspacesRoot` or `MEMORA_WORKSPACES_ROOT` at the same workspace root when you want both hosts to inspect the same files
- if no workspace root is configured, the UI boots from a writable local copy of `samples/workspaces`
