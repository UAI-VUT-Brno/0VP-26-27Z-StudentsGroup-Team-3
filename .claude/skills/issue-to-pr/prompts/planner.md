# Planner prompt

Use this method to turn a GitHub issue into an implementation plan. Work through the steps in order.

## 1. Understand the issue

From `issue.json`, extract:

- **Goal:** one sentence describing the outcome.
- **Requirements:** every concrete behavior the issue asks for, numbered `R1`, `R2`, …
- **Acceptance criteria:** every verifiable condition, numbered `AC1`, `AC2`, … If the issue has a "Done when" or checklist section, take each item from it. If it has none, derive the criteria from the requirements.
- **Explicit constraints:** anything the issue forbids or limits, such as "no new packages" or "read-only".

Do not add requirements the issue does not ask for.

## 2. Locate the affected code

For each requirement:

1. Find the existing code it touches. Start from the "Where code belongs" table in `rules/architecture.md`.
2. Read the relevant files completely, not only the matching lines.
3. Find an existing example of the same pattern and reference it, for example an existing endpoint, DTO, repository method or test.
4. Decide which layers change: DTO, Data, API, tests, documentation.

Check these cross-cutting rules explicitly:

| Check | Rule |
|---|---|
| Does `ITodoRepository` change? | Both `InMemoryTodoRepository` and `JsonFileTodoRepository` must change, with tests for both. |
| Does an operation update an existing todo? | It must use `UpdateAsync` and needs a 409 test. Check the version table in `rules/domain.md`. |
| Does a public contract change (endpoint, DTO, MCP tool, configuration)? | List the documentation to update, from `rules/domain.md`. |
| Does `README.md` change? | `README.cs.md` must change too. |
| Is a new NuGet package needed? | Avoid it. If unavoidable, record it as a risk. |

## 3. Design the tests

For every acceptance criterion, plan at least one test. For every new behavior, plan at least one success test and one failure test. Name tests `Method_Scenario_ExpectedResult` and place them according to the test table in `rules/code-style.md`.

## 4. Resolve ambiguity

You cannot ask the user. When the issue is ambiguous:

- choose the interpretation that changes the least existing behavior and keeps the public API backward compatible,
- follow the closest existing pattern in the repository,
- record the decision under **Assumptions**.

Set `Status: blocked` only if the issue cannot be implemented safely, for example when it contradicts itself, requires a breaking change it does not explicitly ask for, or asks for something outside this repository. Explain the reason. A blocked plan stops the workflow.

## 5. Write the plan

Write `plan.md` in exactly this format. Keep every section; write `None` where nothing applies.

```markdown
# Plan: #<issue number> <issue title>

Status: ready | blocked
Task ID: OVP-T3-<nn>
Branch: <branch from branch.md>

## Goal

<one sentence>

## Requirements

- R1: <requirement>

## Acceptance criteria

- AC1: <criterion> (verified by: <test name or manual check>)

## Assumptions

- <decision made because the issue was ambiguous, or None>

## Affected files

| File | Layer | Change | Reason | Covers |
|---|---|---|---|---|
| `src/...` | DTO / Data / API | add / modify | <what and why> | R1 |

## Tests

| Test file | Test name | Type | Covers |
|---|---|---|---|
| `tests/UAI.ZeroVP.Tests/...` | `Method_Scenario_ExpectedResult` | success / failure | AC1 |

## Public contract changes

<endpoints, DTOs, MCP tools or configuration that change, or None>

## Documentation impact

| File | Change |
|---|---|
| `docs/...` | <what to update> |

## Implementation order

1. <step, for example: add DTO, then repository method, then controller action, then tests>

## Risks

- <what could go wrong or break, or None>

## Out of scope

- <related things the issue does not ask for and that must not be changed>
```

## Quality checklist

Before you write the file, check that:

- every requirement appears in at least one row of **Affected files**,
- every acceptance criterion is covered by at least one test or a named manual check,
- every file path exists, or is clearly marked as `add`,
- no file outside `src/` and `tests/` is listed in **Affected files**; documentation goes to **Documentation impact**,
- the plan contains only what the issue asks for.
