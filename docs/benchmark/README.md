# Benchmark: `/issue-to-pr` vs. GitHub Copilot in VS Code

This benchmark compares the autonomous `/issue-to-pr` workflow (Approach B) with interactive development using GitHub Copilot in VS Code (Approach A) on the same two issues.

## Benchmark issues

| ID | Issue | Type | Base branch | Reference solution |
|---|---|---|---|---|
| B1 | #54, replica of OVP-T3-10 *Add user identification to tasks and "My Tasks" filtering* | Historical | `benchmark/base-t3-10` | PR #30 (18 files, 499 insertions) |
| B2 | #55 *Change todo priority with PATCH /api/todos/{id}/priority* | Synthetic | `benchmark/base-current` | None |

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

All runs took place on 16 November 2026, by Martin239412. Approach A used Copilot Chat in VS Code in *Interactive* mode with *Manual permissions*; Approach B used Claude Code 2.1.325 in `auto` permission mode.

| Metric | B1 Copilot | B1 Agent | B2 Copilot | B2 Agent |
|---|---|---|---|---|
| Total time to PR | 15 min 0 s ¹ | 14 min 20 s | ~10 min ² | 10 min 35 s |
| Human interventions | 1 prompt, 3 approvals ¹ | 0 | 1 prompt, 1 approval, 1 manual PR fix | 0 |
| First-attempt pass | No | No | Not recorded | Yes |
| Repair cycles | 2 | 1 | Not recorded | 0 |
| Code quality (met / applicable) | 4 / 6 | 7 / 7 | 5 / 6 | 6 / 6 |
| Files changed | 18 | 19 | 4 | 10 |
| New test cases | 3 | 29 | 6 | 20 |
| Final test result | 89 / 89 | 115 / 115 | 161 / 161 ³ | 175 / 175 |
| Pull request | #56 | #57 | #58 | #59 |

¹ Attempt 1 only. An earlier attempt 0 (4 min 40 s, 1 prompt, 10 approvals) was aborted because only the issue number was given to Copilot, without the issue text; Copilot then implemented an unrelated priority filter. See `evidence/b1-copilot.md`.
² Copilot finished the code in 1 min 37 s; the rest is committing, pushing and creating the pull request, including a manual correction of its base branch.
³ As reported by Copilot; individual test runs were not recorded.

For reference, PR #30 (the original human solution of B1) changed 18 files and added 17 test methods.

### Code quality

| # | Criterion | B1 Copilot | B1 Agent | B2 Copilot | B2 Agent |
|---|---|---|---|---|---|
| Q1 | Layering follows `rules/architecture.md` | 1 | 1 | 0 ⁴ | 1 |
| Q2 | Repository changes in both implementations | 1 | 1 | n/a | n/a |
| Q3 | ProblemDetails and `[ProducesResponseType]` for errors | n/a ⁵ | 1 | 1 | 1 |
| Q4 | Success and failure test for every new behavior | 0 ⁶ | 1 | 1 | 1 |
| Q5 | Documentation updated | 1 | 1 | 1 | 1 |
| Q6 | `.editorconfig` respected | 0 ⁷ | 1 | 1 | 1 |
| Q7 | No unrelated changes | 1 | 1 | 1 | 1 |

⁴ The controller sets `Priority` and `UpdatedAtUtc` directly instead of adding a `TodoMapper` method. Copilot was not given the repository rules; the agent reads them on every run.
⁵ No new error responses: Copilot added no validation for `userId`. The reference solution and the agent limit it to 100 characters and return 400.
⁶ All three new tests cover successful filtering; there is no failure test.
⁷ The new file `UserTodoApiTests.cs` is UTF-8 without BOM. `git diff --check` passed in all four runs.

### Requirement coverage

- **B1 Agent** did not add `userId` to `POST /api/todos/repeat` (`CreateRecurringTodoRequest`). The issue asks for `userId` in all creation contracts; the reference solution and Copilot include it.
- All other runs cover every acceptance criterion of their issue.

### Observations

- **Autonomy:** both agent runs finished without any human intervention. Copilot needed the issue text to be supplied by a person; with only the issue number it guessed the task and implemented a different feature (B1 attempt 0).
- **Speed:** total times were similar (B1: 14 min 20 s vs. 15 min; B2: 10 min 35 s vs. ~10 min). Copilot generated the B2 code in only 1 min 37 s, but the person then had to commit, push and create the pull request by hand. The agent spends most of its time on planning, verification and documentation.
- **Tests:** the agent wrote considerably more tests (29 vs. 3 on B1, 20 vs. 6 on B2), including validation, 404 and 409 cases, and on B1 more than the reference solution (17 methods).
- **Self-healing:** on B1 the QA agent diagnosed a failing new test (wrong expected content type) and corrected the test instead of changing the shared `[Produces]` attribute of the controller, with a documented reasoning. On B2 the first run passed.
- **Documentation:** the agent updated `docs/OpenAPI.md` and both READMEs; Copilot updated only `docs/OpenAPI.md`.
- **Deviations of the agent:** a missed requirement on B1 (see above), and pull request descriptions ending with a "Generated with Claude Code" line that is not part of the template in `prompts/pr-generator.md`.
- **Measurement limits:** Copilot runs were timed and logged by hand. In B2 the intermediate Copilot steps were not recorded, so its first-attempt result is unknown.

## Evidence

| Run | Location |
|---|---|
| B1 Copilot | [`evidence/b1-copilot.md`](evidence/b1-copilot.md) |
| B1 Agent | [`evidence/b1-agent/`](evidence/b1-agent/): stage outputs, QA logs and `transcript.txt` (unattended run) |
| B2 Copilot | [`evidence/b2-copilot.md`](evidence/b2-copilot.md) |
| B2 Agent | [`evidence/b2-agent/`](evidence/b2-agent/): stage outputs, QA logs and `transcript.txt` (unattended run) |

The evidence files are kept verbatim; `evidence/.gitattributes` excludes them from whitespace checks.
