# Memora.Mcp

## Purpose
Exposes Memora through MCP as the primary provider-facing protocol.

## Responsibilities
- MCP tools
- MCP resources
- MCP prompts
- mapping protocol calls to Memora services

## Does NOT contain
- business logic beyond protocol adaptation

## Key Areas

- `Server/MemoraMcpServer.cs`: tool and resource definitions plus contract forwarding

## Current Scope

- the current implementation is a thin adapter over `IAgentInteractionService`
- it exposes context assembly, proposal, update, and outcome operations
- it exposes a project lookup resource at `memora://projects/{projectId}`
  including repository attachment and imported readiness state
- tool and resource definitions carry explicit request, response, and error contract metadata
- project resource reads validate the published URI template before forwarding to shared services
- imported readiness state includes evidence-derived, inferred, advisory, and
  future-advisory counts without bypassing governed context assembly
- compatibility validation keeps the MCP surface aligned with the companion OpenAPI path through the shared runtime contract
- transport hosting and broader MCP ergonomics are intentionally still thin
