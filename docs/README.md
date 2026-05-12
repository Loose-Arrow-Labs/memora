# docs

## Purpose
Contains the planning, design, scope, and operational documentation for Memora.

## Rule
Docs must describe current implementation honestly and keep roadmap work clearly separate from shipped behavior.

## Read This First

- `current-state.md` for implemented behavior in the current checkout
- `architecture.md` for module boundaries and system rules
- `integration-strategy.md` for provider-facing MCP and OpenAPI guidance
- `import-and-workspace-strategy.md` for planned first-run import modes and
  multi-project workspace placement
- `external-runtime-contract.md` for the shared runtime-facing contract boundary
- `machina-interaction-model.md` for the Machina-to-Memora execution boundary
- `project-state-view.md` for the serialized deterministic state view returned by `get_context`
- `agent-project-state-interpretation.md` for how external agents should read that state view
- `project-state-boundary-rules.md` for what the state view is and is not allowed to become
- `retrieval-evolution.md` for shipped retrieval behavior and deferred retrieval boundaries
- `operator-workflows.md` for current local operator review and recovery workflows
- `controlled-automation.md` for bounded Milestone 6 automation policy, trigger, prototype, and safety rules
- `understanding-outputs.md` for the current human-readable output strategy
- `use-cases.md` for concrete workflows the sample project and product should demonstrate
- `ui-design-system.md` for frontend Atomic Design guidance and reusable UI expectations
- `Atomic Design.md` for the Memora-specific distillation behind the UI guidance
- `openwebui-openapi.md` for the current Open WebUI companion API path
- `claude-integration.md` for the current Claude-oriented setup guidance
- `codex-integration.md` for the current Codex-oriented setup guidance
- `chatgpt-integration.md` for the current ChatGPT-oriented read-only guidance
- `remote-conversational-planning-gap-analysis.md` for the gap between the current external workflow proof and the desired remote planning-write workflow
- `mobile-contribution-packet.md` for the planned Milestone 13 portable packet shape that mobile capture produces and desktop Memora ingests as non-canonical input
- `delivery-process.md` for milestone closeout and stacked PR cleanup guidance
- `milestones.md` for roadmap sequencing
- `repo-intake-boundary.md` for the existing-repo understanding scan boundary (included/excluded paths, determinism rules)
- `repo-structure.md` for folder-level navigation

## Guidance

- use current-state docs for "what exists now"
- use milestone and strategy docs for "what comes next"
- when current code and docs drift, update the docs instead of relying on stale assumptions
