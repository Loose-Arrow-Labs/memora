# tests

## Purpose
Contains automated tests for Memora modules.

## Testing stance
- core domain rules should be strongly covered
- integration layers must include the smallest meaningful validation
- no feature is complete without appropriate tests or validation

## Test Projects

- `Memora.Core.Tests`: lifecycle, validation, planning, approval queue, and diff behavior
- `Memora.Storage.Tests`: parsing, persistence, and workspace layout behavior
- `Memora.Index.Tests`: SQLite schema, rebuild, relationship, and traceability behavior
- `Memora.Context.Tests`: ranking, inclusion reasoning, and context assembly behavior
- `Memora.Import.Tests`: repository attachment behavior and later focused M10 import validation
- `Memora.Api.Tests`: HTTP contract, file-backed agent interaction, runtime prototype, and contract compatibility behavior
- `Memora.Mcp.Tests`: MCP adapter and shared contract behavior
- `Memora.Ui.Tests`: operator shell, context viewer, and understanding output behavior
