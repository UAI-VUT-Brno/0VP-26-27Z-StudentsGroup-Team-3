# Benchmark: `/issue-to-pr` vs. GitHub Copilot in VS Code

This benchmark compares the autonomous `/issue-to-pr` workflow (Approach B) with interactive development using GitHub Copilot in VS Code (Approach A) on the same two issues.

## Benchmark issues

| ID | Issue | Type | Base branch | Reference solution |
|---|---|---|---|---|
| B1 | Replica of OVP-T3-10 *Add user identification to tasks and "My Tasks" filtering* | Historical | `benchmark/base-t3-10` | PR #30 (18 files, 499 insertions) |
| B2 | *Change todo priority with PATCH /api/todos/{id}/priority* | Synthetic | `benchmark/base-current` | None |

- **B1** reuses the original issue text of #23. The original issue is closed and already implemented in `main`, so the benchmark runs on a baseline branch created from commit `3478a2a`, the last commit of `main` before PR #30.
- **B2** is a new issue written for the benchmark. Its text is in [`issues/b2-change-priority.md`](issues/b2-change-priority.md).
- Both benchmark issues are titled with the prefix `[Benchmark]` and labeled `benchmark`.

## Baseline branches

| Branch | Created from | Added on top |
|---|---|---|
| `benchmark/base-t3-10` | `3478a2a` (before PR #30) | The agentic pack (`.claude/`) and the `.issue-to-pr/` entry in `.gitignore` |
| `benchmark/base-current` | The `--base` commit of OVP-T3-22 | Nothing |

On `benchmark/base-t3-10`, the rule files are adjusted to the state of the code at that commit:

- References to user identification (`NormalizeUserId`, the 100-character user ID limit) are removed, because they describe the solution of B1.
- References to MCP (`TodoTools.cs`, `docs/MCP.md`, `McpException`) are removed, because MCP was added later (OVP-T3-14).

All other rules, prompts and agent definitions are identical to `main`.

## Preventing accidental merges

- Every benchmark pull request targets its **baseline branch**, never `main`.
- Approach B creates its pull request with `/issue-to-pr <issue> --base <baseline branch>`; its title is the issue title, which starts with `[Benchmark]`.
- Approach A pull requests are titled `[Benchmark][Copilot] <issue title>`.
- All benchmark pull requests get the labels `benchmark` and `do not merge`.
- Benchmark pull requests are closed without merging after the results are recorded.

## Procedure

### Order

Run Approach A (Copilot) for an issue **before** Approach B (`/issue-to-pr`) for the same issue, so the person using Copilot has not seen the agent's solution. The agent's result does not depend on the order.

### Approach A: GitHub Copilot in VS Code

1. Check out the baseline branch and create `benchmark/copilot-b<n>` from it.
2. Open the issue on GitHub and **start the timer**.
3. Solve the issue interactively in VS Code with Copilot (inline completions and Copilot Chat with `@workspace`). Write code, tests and documentation as needed.
4. Record every interaction in the run log (template below).
5. Run `dotnet test --configuration Release` whenever you consider the change complete. The first such run is the **first attempt**.
6. Commit, push, create the pull request against the baseline branch and **stop the timer**.

### Approach B: `/issue-to-pr`

1. Check out the baseline branch, make sure the working tree is clean and start Claude Code (`claude`) in the repository root, in `auto` permission mode.
2. **Start the timer** and run:

   ```text
   /issue-to-pr #<issue> --base <baseline branch>
   ```

3. Do not interact with Claude Code until the workflow finishes. Any message, approval or manual change counts as a human intervention.
4. **Stop the timer** when the pull request is created.
5. Export the transcript with `/export` and copy the run folder `.issue-to-pr/<issue>/` to `docs/benchmark/evidence/b<n>-agent/`.

## Metrics

| Metric | Approach A (Copilot) | Approach B (`/issue-to-pr`) |
|---|---|---|
| **Total time to PR** | From opening the issue to creating the pull request | From starting the command to creating the pull request |
| **Human interventions** | Prompts sent to Copilot Chat plus manual code edits made outside a Copilot suggestion; accepted inline completions are counted separately | Any message, approval or manual change after starting the command (target: 0) |
| **First-attempt pass** | Whether the first `dotnet test --configuration Release` passed | `First run passed` in `qa-report.md` |
| **Repair cycles** | Number of further test runs needed after a failing run | `Repair cycles` in `qa-report.md` |
| **Code quality** | Checklist below | Checklist below |

### Code quality checklist

Score each item 1 (met) or 0 (not met). Mark items that do not apply as `n/a`.

| # | Criterion | How to check |
|---|---|---|
| Q1 | Layering follows `rules/architecture.md` | Controllers use `ITodoRepository` only; mapping in `TodoMapper` |
| Q2 | Repository changes are made in both implementations | `InMemoryTodoRepository` and `JsonFileTodoRepository` |
| Q3 | Errors use ProblemDetails and are declared with `[ProducesResponseType]` | Review the controller |
| Q4 | Every new behavior has a success and a failure test | Review the tests |
| Q5 | Documentation is updated (`docs/OpenAPI.md`; both READMEs if they change) | Review `docs/` |
| Q6 | `.editorconfig` is respected | `git diff --check`; new `.cs` files are UTF-8 with BOM |
| Q7 | No unrelated changes | Review the diff |

For B1, also compare the changed files and the number of new tests with the reference solution (PR #30).

## Run log template for Approach A

Copy this to `docs/benchmark/evidence/b<n>-copilot.md` and fill it in during the run.

```markdown
# B<n> Copilot run

Person: <GitHub user>
Start: <hh:mm:ss>
End: <hh:mm:ss>

| Time | Type | Description |
|---|---|---|
| 00:02 | Chat prompt | Asked @workspace where todos are filtered |
| 00:05 | Inline completions | Accepted 3 completions in TodoItem.cs |
| 00:09 | Manual edit | Fixed the namespace in the new test file |
| 00:12 | Test run | 1 failing test (attempt 1) |

Chat prompts: <n>
Manual edits: <n>
Accepted inline completions: <n>
Test runs: <n> (first passed: yes | no)
Pull request: <URL>
```

## Results

Fill in after all runs.

| Metric | B1 Copilot | B1 Agent | B2 Copilot | B2 Agent |
|---|---|---|---|---|
| Total time to PR | | | | |
| Human interventions | | | | |
| First-attempt pass | | | | |
| Repair cycles | | | | |
| Code quality (Q1–Q7) | | | | |
| Files changed | | | | |
| New tests | | | | |
| Pull request | | | | |

## Evidence

| Run | Location |
|---|---|
| B1 Copilot | `evidence/b1-copilot.md` |
| B1 Agent | `evidence/b1-agent/` (run folder and transcript) |
| B2 Copilot | `evidence/b2-copilot.md` |
| B2 Agent | `evidence/b2-agent/` (run folder and transcript) |
