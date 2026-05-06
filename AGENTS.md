# Memora Execution Rules

---

## 1. What Memora Is

Memora is a:

- local-first
- structured memory system
- governance layer for AI-assisted software development

Memora provides:

- durable project state
- structured artifacts
- lifecycle-controlled updates
- deterministic context retrieval

---

## 2. What Memora Is NOT

Memora is NOT:

- a chat history store
- a vector database
- a generic knowledge base
- an execution runtime
- a replacement for Strata

---

## 3. Locked Architecture

- Language: C#
- Filesystem = canonical source of truth
- SQLite = derived local index (rebuildable)
- MCP = primary integration layer
- OpenAPI = companion integration layer
- Strata = external retrieval system (separate concern)

---

## 4. Core System Rules

### 4.1 Truth Model

- Only approved artifacts are canonical truth
- Drafts and proposals are not authoritative
- Filesystem is always the final source of truth

### 4.2 Lifecycle Enforcement

All artifacts must follow lifecycle:

proposed → draft → approved → superseded/deprecated

- No system component may bypass lifecycle rules

### 4.3 Write Constraints

- Agents may only propose changes in v1
- No direct writes to canonical artifacts
- All canonical updates require explicit approval

### 4.4 Retrieval Constraints

- Retrieval must be deterministic and explainable
- No semantic/vector retrieval in core v1
- No probabilistic ranking in v1

### 4.5 Boundary Rules

- Memora = structured project memory and understanding
- Strata = broad retrieval/search

Do not mix these responsibilities.

---

## 5. Artifact Rules

Artifacts must:

- be strongly typed
- be validated before persistence
- include required metadata
- support versioning/revision tracking

Do not:

- store unstructured or ambiguous state
- rely on implicit meaning

---

## 6. Coding Rules

- Keep modules small and focused
- Avoid premature abstraction
- Prefer explicit over implicit behavior
- Do not duplicate logic across layers
- Keep provider-specific logic out of domain models

---

## 7. Integration Rules

- MCP exposes tools/resources — not business logic
- OpenAPI mirrors core capabilities where needed
- All integration flows must respect lifecycle + approval

---

## 8. Delivery Rules

- One issue = one reviewable change
- Each change must have clear acceptance criteria
- PRs that fully satisfy an issue must use a GitHub closing keyword such as
  `Closes #123`; use `References #123` only for related or partial work that
  should not auto-close the issue.
- Do not implement beyond current milestone scope
- Do not assume future features exist

---

## 9. Validation Expectations

- Code must compile successfully
- API changes must not break contract shape
- Storage must remain filesystem-first
- SQLite index must be rebuildable from files

---

## 10. Anti-Patterns (DO NOT DO)

- Do not bypass approval for speed
- Do not introduce vector DB logic into core
- Do not treat retrieval results as truth
- Do not mix planning logic into storage logic
- Do not invent missing requirements

---

## 11. Guiding Principle

Memora preserves what the system knows, not what the system guesses.

---

## 12. Testing Policy

- Core domain rule changes should include tests that define and protect those rules
- Integration and UI changes must include the smallest meaningful validation needed to prove behavior
- Every feature must leave behind appropriate tests or validation before completion
- Strict test-first development is not required for all features
- Test-first is preferred for:
  - lifecycle rules
  - validation
  - parsing
  - context assembly
  - rebuild behavior
  - project isolation

---

## 13. Reasoning Context

Each issue execution must be treated as a fresh reasoning task.

You MUST build understanding from:

- `AGENTS.md`
- relevant repo docs
- the current GitHub issue
- the current branch and working tree state
- relevant repository files

You MUST NOT rely on stale assumptions when current repo state contradicts them.

If required context is missing and the gap is material:

- STOP execution
- report the missing information or conflict
- do not guess

Reasonable implementation-level assumptions are allowed when:

- they stay within issue scope
- they do not violate documented architecture
- they do not invent new product behavior

Goal:

Ensure deterministic, reproducible execution grounded in current repo state.

---

## 14. Workflow Modes

Memora supports two execution modes:

1. Default Workflow
2. Unattended Stacked Milestone Mode

Unless the user explicitly requests unattended stacked milestone mode, use Default Workflow.

---

## 15. Default Workflow

Use this for normal repo work.

### 15.1 Starting State

Before starting issue work:

- confirm current branch
- confirm `origin` remote
- confirm working tree status
- confirm the local checkout is safe to use
- start from updated `main`
- pull `origin/main` immediately before creating the issue branch
- prefer reusing the main checkout in place
- do not create a sibling worktree when the current checkout is already clean and safe to use

If the current checkout is dirty with unrelated changes:

- use a clean sibling worktree from updated `main`
- do not disturb the existing checkout
- record the sibling worktree path in the working notes so it can be removed during cleanup

### 15.2 Branching

Create one branch per issue named:

`feature/<issue-number>-<short-name>`

### 15.3 Execution

For each issue:

1. Confirm the issue is narrowly scoped
2. Review scope, acceptance criteria, dependencies, and relevant docs
3. Implement only that issue
4. Avoid unrelated changes
5. Run the smallest meaningful validation
6. Open a draft PR targeting `main`; if the PR fully satisfies the issue, use
   `Closes #<issue-number>` in the PR body so GitHub auto-closes the issue on
   merge
7. Stop after the draft PR is open unless explicitly asked to continue

### 15.4 Cleanup

After merge:

- delete the completed branch locally
- delete the completed branch on GitHub
- remove any completed local worktree used for that issue
- if a sibling worktree was used, verify the surviving main checkout now contains the merged commit before deleting the worktree

---

## 16. Unattended Stacked Milestone Mode

This is an exception workflow. It is not the default.

Use this mode only when the user explicitly requests unattended milestone execution.

### 16.1 Purpose

This mode allows execution of a full milestone without waiting for PR merges between issues.

### 16.2 Starting State

Before starting unattended milestone execution:

- confirm current branch
- confirm `origin` remote
- confirm working tree status
- confirm the GitHub repo identity
- fetch and prune remotes
- confirm `main` is up to date with `origin/main`
- pull `origin/main` immediately before creating the first issue branch
- identify the target milestone and its open issues
- confirm issue order from milestone definitions, issue dependencies, and issue scope
- prefer the existing checkout when it is clean; only create a sibling worktree when the current checkout cannot be used safely

If the current checkout is dirty with unrelated changes:

- create a clean sibling worktree from updated `main`
- use that clean worktree as the milestone starting point
- record that worktree path so post-merge cleanup can remove it

### 16.3 Branching Model

- Create one branch per issue
- Keep the existing branch naming rule:

`feature/<issue-number>-<short-name>`

- Issue 1 base: updated `main`
- Issue N base: previous issue branch in the stack

### 16.4 PR Model

- Create one draft PR per issue
- Keep the existing draft PR rule
- Issue 1 PR target: `main`
- Issue N PR target: previous issue branch in the stack
- Do not merge PRs during unattended stacked execution
- Leave the full PR stack open for human review after the milestone is complete

Each PR must:

- use `Closes #<issue-number>` when the PR fully satisfies its GitHub issue
- use `References #<issue-number>` only when the PR is related to an issue but
  should not auto-close it
- describe current scope honestly
- distinguish current implementation from roadmap work

### 16.5 Execution Loop

For each issue in the milestone:

1. Review scope, dependencies, acceptance criteria, and relevant docs
2. Confirm the issue is unblocked by earlier stack work
3. Implement only the required change
4. Update docs only if behavior changes within scope
5. Run the smallest meaningful validation for touched projects
6. Confirm:
   - acceptance criteria are satisfied
   - no scope expansion occurred
   - no architectural drift was introduced
   - no unrelated files were modified
7. Commit the issue work
8. Push the issue branch
9. Open the draft PR
10. Continue to the next issue in the stack

### 16.6 Dependency Rule

- Respect explicit dependency order
- Treat issue order as a merge-safety contract, not only an execution order
- Prefer the thinnest real slice when multiple issues appear unblocked
- Do not reorder issues arbitrarily
- Before opening each stacked PR, verify that its base branch is the
  immediately previous issue branch from the declared order, unless that
  previous issue has already been merged to `main` before work resumes
- If an issue is blocked and no clearly unblocked milestone issue can be taken next without violating dependency order, STOP and report the blocker

### 16.7 Failure Rule

Stop execution immediately if any of the following occurs:

- acceptance criteria cannot be satisfied
- issue scope is materially unclear or contradictory
- a required dependency is missing
- repo docs conflict with issue requirements
- architecture violation would be required to proceed
- validation fails and cannot be resolved within issue scope
- stacked branch state becomes inconsistent
- a PR would need to target an out-of-order, deleted, or already-merged stack
  branch in order to continue

When stopping, report:

- failing issue
- failing criteria or conflict
- impacted files
- recommended next action

### 16.8 Completion State

At milestone completion:

- all issue branches exist
- all draft PRs are open
- the PR stack is ordered correctly
- nothing has been merged during unattended execution
- the stack is ready for human review

### 16.9 Interrupted Session Recovery

If an unattended stacked milestone session is interrupted and the operator
merges any submitted PRs before work resumes:

- fetch and prune remotes before continuing
- inspect the merged/open PRs and issue states for the milestone
- recompute the next branch base from actual remote state
- continue from updated `main` only when all earlier issue changes are reachable
  from `origin/main`
- otherwise continue from the immediately previous still-open issue branch in
  the declared issue order
- do not restore deleted stack branches or retarget PRs out of order to make
  progress
- if earlier issue changes are only present on intermediate branches and are
  not reachable from `origin/main` or the previous still-open issue branch,
  STOP and report the recovery state

The goal is that a human can merge the submitted PRs in issue order after any
interruption without causing avoidable conflicts or hidden dependency gaps.

### 16.10 Post-Review Cleanup

After the milestone stack is reviewed and merged, return to the normal cleanup rules:

- delete completed branches locally
- delete completed branches on GitHub
- remove completed worktrees
- verify each merged issue branch has no unique non-merge commits relative to `origin/main` before deleting it
- if any merged stack branch still differs from `origin/main`, stop cleanup and open a narrow recovery branch or PR before deleting anything else

---

## 17. Priority Order

1. Respect issue scope
2. Satisfy acceptance criteria
3. Preserve architecture
4. Preserve workflow integrity
5. Then implement functionality

---

## Final Principle

Correctness > completeness
Structure > speed
Boundaries > features
