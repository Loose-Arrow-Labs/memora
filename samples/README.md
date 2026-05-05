# samples

## Purpose
Contains sample workspaces, fixture artifacts, and demo data for Memora.

## Rule
Use samples to validate parsing, indexing, rebuild behavior, and example workflows.

## Demo Workflow

The `demo-project` workspace is not just parser fixture data. It should also
demonstrate how Memora separates:

- approved project truth
- deferred future-track questions
- non-canonical execution or discussion summaries

In the current sample, the retrieval discussion artifacts model a realistic
workflow where future ideas are preserved without being promoted into current
core scope.

## Workflow Samples

The `workflows/` folder contains local PowerShell samples for current external
runtime validation:

- `codex-external-workflow.ps1`: project lookup, deterministic context retrieval, proposal submission, and outcome recording through the companion API
- `chatgpt-read-only-context.ps1`: project lookup and read-only state-view retrieval through the companion API

## Captured Planning State

The demo workspace also includes draft artifacts for the next IDE review
boundary work. Those files document VS Code and Cursor review intent, but they
do not mean an IDE extension or IDE approval bridge is implemented.
