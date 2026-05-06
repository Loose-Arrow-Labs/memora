# src

## Purpose
Contains all Memora product code.

## Layout
- Memora.Core
- Memora.Import
- Memora.Storage
- Memora.Index
- Memora.Context
- Memora.Api
- Memora.Mcp
- Memora.Ui

## Rule
Keep module boundaries strict. Do not duplicate domain rules across projects.

## Suggested Reading Order

1. `Memora.Core`
2. `Memora.Import`
3. `Memora.Storage`
4. `Memora.Index`
5. `Memora.Context`
6. `Memora.Api`
7. `Memora.Mcp`
8. `Memora.Ui`

## Entry Points

- `Memora.Core`: domain rules, lifecycle, validation diagnostics, approval queue and workflow rules, diffs, controlled automation policy and safety models, shared agent contracts, review inbox contract shapes, imported readiness contract shapes, state-view serialization, and the external runtime contract definition
- `Memora.Import`: shared first-run import application services for repository attachment, evidence intake, safety filtering, candidate generation, and readiness reporting
- `Memora.Storage`: parsing, markdown writing, file persistence, and workspace discovery
- `Memora.Index`: SQLite schema, rebuild logic, diagnostics from filesystem truth, relationship indexing, and traceability queries
- `Memora.Context`: deterministic ranking, inclusion reasoning, derived context package caching, bounded relationship traversal, layered context bundle assembly, and optional future retrieval extension boundaries
- `Memora.Api`: minimal HTTP host over the shared agent interaction service, imported project readiness lookup, review inbox and governed review decision endpoints for IDE clients, companion OpenAPI document, guarded file-backed session-summary write prototype, and runtime-facing prototype coverage
- `Memora.Mcp`: thin in-process MCP adapter surface over the same shared contract, including imported project readiness lookup, with compatibility validation against the companion API path
- `Memora.Ui`: styled operator shell, review workflow previews, context viewer, and understanding outputs
