# Contributing to Memora

Memora follows strict engineering discipline to maintain a clean, reliable,
and explainable system for AI-assisted development.

This repository is not a sandbox. Every change must align with the documented
architecture, lifecycle rules, and system boundaries.

---

## Core Principles

- Filesystem is the canonical source of truth
- SQLite is a derived index only
- Agents may propose changes, not write canonical state
- Retrieval must remain deterministic and explainable
- Module boundaries must not be violated

Refer to:
- `AGENTS.md`
- `docs/architecture.md`
- `docs/requirements.md`
- `docs/scope.md`

---

## Contribution Workflow

1. Select or create a GitHub issue
2. Confirm scope is clearly defined
3. Write tests first (where applicable)
4. Implement the smallest valid change
5. Validate against acceptance criteria
6. Submit PR for review

---

## Commit Message Guidelines

Format:

type(scope): short description

Examples:

feat(core): add artifact base schema
fix(storage): handle invalid frontmatter parsing
test(context): add ranking engine validation

Types:

- feat: new feature
- fix: bug fix
- refactor: internal change without feature impact
- test: test-related changes
- docs: documentation changes
- chore: tooling or maintenance

Rules:

- Max 100 characters per line
- Keep commits small and focused
- One logical change per commit

---

## Pull Request Requirements

Each PR must:

- Reference a GitHub issue
- Use a GitHub closing keyword such as `Closes #123` when the PR fully
  satisfies that issue, so GitHub auto-closes it on merge
- Use `References #123` only for related or partial work that should not
  auto-close the issue
- Stay within defined scope
- Include tests where applicable
- Pass build and validation checks
- Not introduce architectural drift

---

## Testing Strategy

- Core domain logic must be strongly tested
- Validation and lifecycle rules are critical
- Integration layers should have minimal but meaningful coverage
- No feature is complete without validation

---

## Module Boundaries

Do not violate separation of concerns:

- `Memora.Core` → domain logic only
- `Memora.Storage` → file parsing/persistence
- `Memora.Index` → SQLite indexing
- `Memora.Context` → retrieval + ranking
- `Memora.Api` → OpenAPI surface
- `Memora.Mcp` → MCP protocol layer
- `Memora.Ui` → UI only

---

## What NOT to Do

- Do not introduce vector databases in core
- Do not bypass lifecycle rules
- Do not write directly to canonical artifacts
- Do not mix Strata responsibilities into Memora
- Do not implement provider-specific logic in core
- Do not over-engineer abstractions prematurely

---

## Decision Making

If a change conflicts with:

- architecture
- requirements
- scope

then:

→ Stop and raise the issue before implementing.

---

## Final Rule

Memora must remain:

- deterministic
- explainable
- structured
- approval-governed

If a change weakens any of these, it should not be merged.
