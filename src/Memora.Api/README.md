# Memora.Api

## Purpose
Exposes Memora capabilities through a local OpenAPI-compatible service.

## Responsibilities
- project lookup endpoint
- deterministic context endpoint
- artifact proposal endpoint
- update proposal endpoint
- outcome recording endpoint

## Does NOT contain
- duplicated core rules
- duplicated ranking logic

## Key Areas

- `Program.cs`: route registration and service wiring
- `Services/FileSystemAgentInteractionService.cs`: file-backed project, cached context, proposal, outcome, and guarded session-summary write flow
- `Services/UnavailableAgentInteractionService.cs`: guarded fallback when no workspace root is configured
- `AgentInteractionHttpResults.cs`: maps shared contract results to HTTP responses

## Current Scope

- endpoints are minimal and focused on the shared agent interaction contract
- current routes are project lookup, context retrieval, artifact proposal, update proposal, and outcome recording
- the host publishes a companion OpenAPI document at `/openapi.json`
- the file-backed path is only active when a workspace root is configured
- context cache reuse is derived from freshly loaded filesystem artifacts and never replaces filesystem truth
- runtime-facing proposal payloads normalize JSON-bound type-specific values before shared validation so companion-surface requests stay compatible with the shared contract
- validation errors preserve structured code/path fields and use diagnostic messages from core validation
- the guarded direct-write prototype is limited to non-canonical session summaries in summary storage
- this host is intentionally thin and does not claim a full production API surface
- runtime-facing prototype and compatibility validation currently live in `tests/Memora.Api.Tests`

## Local Development
- the default development launch profile listens on `http://127.0.0.1:5081`
- verify the current OpenAPI document at `http://127.0.0.1:5081/openapi.json`
- set `MEMORA_WORKSPACES_ROOT` or `Memora:WorkspacesRootPath` to a workspace root before exercising file-backed endpoints
- this host is intended to run alongside `Memora.Ui`, which uses its own development port
