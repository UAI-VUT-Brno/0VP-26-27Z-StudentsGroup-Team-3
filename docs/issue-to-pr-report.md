# Autonomous implementation of GitHub issues with `/issue-to-pr`

Technical and benchmark report, team 3, course 0VP 26/27Z.

## 1. Summary

`/issue-to-pr` is an agentic workflow for the UAI.ZeroVP ToDo REST API (.NET 10). Given a GitHub issue, it creates a feature branch, implements the change across the DTO, Data and API layers, writes xUnit tests, verifies the build and tests with a bounded self-healing loop, updates documentation, and opens a pull request with `gh`. It runs in Claude Code and consists only of Markdown files: one orchestrator skill, four subagents, four stage prompts and four rule files. It contains no executable code.

We compared the workflow with interactive development using GitHub Copilot in VS Code on two issues: a replica of the historical issue OVP-T3-10 and a new synthetic issue. Both unattended runs finished without human intervention and produced verified pull requests. Total time to pull request was similar to Copilot (14 min 20 s vs. 15 min on B1, 10 min 35 s vs. about 10 min on B2), but the agent wrote far more tests (29 vs. 3 and 20 vs. 6 new test cases) and scored higher on our code quality checklist, which measures how the code is written. Functional completeness is a separate result: on B1, the agent interpreted one requirement too narrowly, as discussed in section 6.

The full benchmark protocol, raw data and transcripts are in [`benchmark/README.md`](benchmark/README.md).

## 2. Architecture

### 2.1 Runtime and invocation

The workflow runs in **Claude Code** in `auto` permission mode. Claude Code discovers project skills in `.claude/skills/` and exposes each one as a slash command; subagents are defined in `.claude/agents/`. The skill sets `disable-model-invocation: true`, so it starts only when a user types the command:

```text
/issue-to-pr <#number | number | issue URL> [--base <branch>] [--dry-run]
```

`--dry-run` validates the input, fetches the issue, checks the rules and shows the derived branch name and planned stages without changing anything. `--base` runs the workflow on a branch other than `main`; the benchmark uses it to work on baseline branches.

### 2.2 Components

| Component | Location | Responsibility |
|---|---|---|
| Orchestrator | `.claude/skills/issue-to-pr/SKILL.md` | Input validation, issue ingestion, loading rules, stage order, stage result checks, stop conditions, final report |
| Planner agent | `.claude/agents/issue-to-pr-planner.md` | Analyzes the issue and the repository and writes a plan |
| C# Implementation agent | `.claude/agents/issue-to-pr-implementer.md` | Implements the plan in `src/` and `tests/` and commits |
| QA & Self-Healing agent | `.claude/agents/issue-to-pr-qa.md` | Builds, tests, diagnoses and repairs failures |
| Release & PR agent | `.claude/agents/issue-to-pr-release.md` | Creates the branch, updates documentation, commits the rest, pushes and opens the pull request |
| Prompt library | `.claude/skills/issue-to-pr/prompts/` | Detailed method and output format for each stage |
| Rules | `.claude/skills/issue-to-pr/rules/` | Architecture, domain, code style and git conventions of this repository |

The pack has 17 files with about 1,300 lines in total.

### 2.3 Stages and data flow

The orchestrator never writes code itself. It runs six stages in a fixed order and passes each subagent only the issue number, the run folder and, for the release agent, the stage name.

| # | Stage | Subagent | Output | Required to continue |
|---|---|---|---|---|
| 1 | Prepare: clean working tree, base branch, feature branch | Release | `branch.md` | `Status: done` |
| 2 | Plan | Planner | `plan.md` | `Status: ready` |
| 3 | Implement and commit | Implementer | `implementation.md` | `Status: done` |
| 4 | Verify with self-healing | QA | `qa-report.md` | `Status: passed` |
| 5 | Document | Release | `docs.md` | `Status: done` |
| 6 | Commit the rest, validate, push, create PR | Release | `pr.md` | `Status: done` |

All stages exchange data only through files in `.issue-to-pr/<issue>/`, which git ignores. This has three effects:

- **Isolation:** each subagent starts with a fresh context and receives only what it needs, not the whole conversation.
- **Traceability:** every run leaves a complete record (issue, plan, implementation report, QA logs, documentation report, pull request body), which we used directly as benchmark evidence.
- **Explicit gates:** the orchestrator reads the `Status:` line of each output file and stops on any other value. The existence of a file is never treated as success.

### 2.4 Tool permissions

| Subagent | Allowed tools | Additional scope set by instructions |
|---|---|---|
| Planner | Read, Grep, Glob, Write | Writes only `plan.md`; no shell |
| Implementer | Read, Grep, Glob, Edit, Write, Bash, PowerShell | Edits only `src/` and `tests/`; local commits; no build, test or push |
| QA | Read, Grep, Glob, Edit, Write, Bash, PowerShell | Edits only `src/` and `tests/`; `dotnet build` and `dotnet test`; no commits |
| Release | Read, Grep, Glob, Edit, Write, Bash, PowerShell | Edits only `docs/` and the READMEs; git and `gh`; never merges or force-pushes |

Claude Code enforces the tool list of each subagent; the planner, for example, cannot run commands at all. Folder and command restrictions are enforced only by the instructions. Stage 6 adds a structural safeguard: it commits only files listed by stages 4 and 5 and stops on any other change.

The implementer is deliberately not allowed to build or test. If it could, it would fix its own errors before handing over, and the benchmark could not measure the first-attempt pass rate.

## 3. Prompt engineering

### 3.1 Grounding in repository rules

Generic instructions such as "write clean code" do not tell an agent where code belongs in this repository. The four rule files therefore describe the project concretely and point to real files:

- **`architecture.md`:** project dependencies, a table of where each kind of change belongs (with an existing example for each), the rule that both `ITodoRepository` implementations must change together, and documented exceptions such as `TodosController.Repeat()`.
- **`domain.md`:** optimistic concurrency, including a table of which operations take a client-supplied `version`, ProblemDetails helpers, validation limits and which documentation to update for each kind of contract change.
- **`code-style.md`:** `.editorconfig` rules (UTF-8 with BOM for C#), C# conventions, test locations and naming, and the verification commands.
- **`git-conventions.md`:** branch names, Conventional Commits with the task ID as scope, pull request template and forbidden operations.

Every subagent must read all four rule files before acting. A missing file stops the workflow.

### 3.2 Role prompts and stage prompts

Each subagent has a short role definition in `.claude/agents/` (boundaries, inputs, outputs) and a longer stage prompt in `prompts/` (method and output format). Claude Code gives a subagent only its own system prompt, not the main conversation, so every agent definition explicitly lists the files to read first.

### 3.3 Structured outputs

Every stage writes a fixed Markdown template with a `Status:` line. The planner, for example, must number requirements (`R1`, …) and acceptance criteria (`AC1`, …) and map each one to an affected file and a test. Later stages refer to these identifiers, and the final pull request body is filled only from values recorded in the run folder, so the agent cannot report results it has not measured.

### 3.4 Diagnose before repair

The QA prompt requires a written diagnosis for every failure before any edit: what failed (with expected and actual values), the root cause based on code that was read, and the smallest fix. A table of common compiler errors (`CS0246`, `CS1061`, `CS7036`, `CS0535`, …) maps each one to its typical cause in this repository.

### 3.5 Rules against gaming

The prompts explicitly forbid the shortcuts a model may take to make a run look successful:

- deleting, skipping or weakening tests, or catching assertion failures,
- suppressing warnings with `#pragma warning disable` or `<NoWarn>`,
- committing unexpected files (`git add -A` is forbidden; files are staged by explicit path),
- claiming that nothing was published after a push without checking the remote state.

When a new test and the code disagree, the plan decides which side is wrong; existing tests always define existing behavior.

### 3.6 Robustness to the environment

Two details came from observing real runs on a Czech Windows system:

- `dotnet test` printed localized output ("Souhrn testu: celkem…"). The QA agent therefore sets `DOTNET_CLI_UI_LANGUAGE=en` and reads results from TRX files, which do not depend on the console language.
- On Windows, Claude Code uses a PowerShell tool instead of Bash, so the prompts give both variants of each command.

## 4. Failure handling

| Situation | Behavior |
|---|---|
| Invalid input, closed issue, unknown `--base` branch | Stop before any change, with a usage message |
| Missing rule file | Stop before stage 1 |
| Dirty working tree or existing branch | Stage 1 fails; nothing is created |
| Ambiguous issue | Planner chooses the least invasive, backward-compatible interpretation and records it under **Assumptions** |
| Unsafe or contradictory issue | Planner returns `Status: blocked`; the workflow stops |
| Compiler error, failing test, new warning | QA repair loop: at most 4 runs in total, so at most 3 repair cycles |
| Same failure after two consecutive repairs | QA stops early, since further attempts are unlikely to help |
| Environment problem (missing SDK, NuGet error, locked file) | QA stops without repairing; code cannot fix it |
| Unexpected file in the working tree before commit | Stage 6 stops without committing it |
| Failure before push | Nothing is published |
| Push or pull request creation fails | Release agent checks the remote state and records whether the branch exists and whether the pull request was created |

The workflow never merges a pull request, pushes to the base branch, force-pushes or deletes branches.

## 5. Benchmark

### 5.1 Setup

| ID | Issue | Base branch | Reference |
|---|---|---|---|
| B1 | #54, replica of OVP-T3-10 *Add user identification to tasks and "My Tasks" filtering* | `benchmark/base-t3-10` (code before PR #30) | PR #30: 18 files, 17 test methods |
| B2 | #55 *Change todo priority with PATCH /api/todos/{id}/priority* (synthetic) | `benchmark/base-current` | None |

For B1, we removed references to user identification and to MCP from the rules on the baseline branch, because they describe the solution or did not exist yet. Copilot runs used Copilot Chat in VS Code in *Interactive* mode with *Manual permissions* and always took place before the agent run on the same issue. All benchmark pull requests target their baseline branch and are labeled `benchmark` and `do not merge`.

### 5.2 Results

| Metric | B1 Copilot | B1 Agent | B2 Copilot | B2 Agent |
|---|---|---|---|---|
| Total time to PR | 15 min 0 s ¹ | 14 min 20 s | ~10 min ² | 10 min 35 s |
| Human interventions | 1 prompt, 3 approvals ¹ | 0 | 1 prompt, 1 approval, 1 manual PR fix | 0 |
| First-attempt pass | No | No | Not recorded ⁴ | Yes |
| Repair cycles | 2 | 1 | Not recorded ⁴ | 0 |
| Code quality checklist (met / applicable) ⁵ | 4 / 6 | 7 / 7 | 5 / 6 | 6 / 6 |
| Issue scope fully implemented ⁵ | Yes | No ⁶ | Yes | Yes |
| Files changed | 18 | 19 | 4 | 10 |
| New test cases | 3 | 29 | 6 | 20 |
| Final test result | 89 / 89 | 115 / 115 | 161 / 161 ³ | 175 / 175 |
| Pull request | #56 | #57 | #58 | #59 |

¹ Attempt 1 only. An earlier attempt 0 (4 min 40 s, 1 prompt, 10 approvals) was aborted because Copilot received only the issue number, not its text.
² Copilot finished the code in 1 min 37 s; the rest is committing, pushing and creating the pull request.
³ As reported by Copilot; individual test runs were not recorded. The CI check of #58 (build and all tests) passed.
⁴ Not recoverable: the steps were too fast to log by hand and the chat history was closed after the run. Comparisons of these two metrics are therefore based on B1 only.
⁵ The code quality checklist measures how the code is written, not whether the issue is complete; functional completeness is the separate row below it.
⁶ `userId` is missing in `POST /api/todos/repeat`; see section 6.1.

Approximate stage durations of the agent runs, from the timestamps of the stage output files:

| Stage | B1 | B2 |
|---|---|---|
| Prepare | < 1 min | < 1 min |
| Plan | ~3 min | ~3 min |
| Implement | ~5 min | ~2 min |
| QA | ~2 min | ~2 min |
| Document | ~2 min | ~1 min |
| Publish | ~2 min | ~1 min |

### 5.3 Code quality

The checklist (Q1–Q7) covers layering, both repository implementations, ProblemDetails and response declarations, success and failure tests, documentation, `.editorconfig` and unrelated changes. Points lost:

- **B1 Copilot:** no failure tests (all three new tests cover successful filtering); a new test file without BOM.
- **B2 Copilot:** the controller sets the priority directly instead of using a `TodoMapper` method. Copilot did not receive our rules, while the agent reads them on every run.

The agent lost no points on the checklist. This does not mean that it fully implemented every issue: the checklist evaluates the code that was written, while requirement coverage is reported separately (table in 5.2, section 6.1). The full table with explanations is in [`benchmark/README.md`](benchmark/README.md#code-quality).

### 5.4 Interpretation

- **Autonomy is the main difference.** The agent needed one command and no further input. Copilot needed a person to supply the issue text, approve edits, commit, push and create the pull request. In B1 attempt 0, given only "Issue #54", Copilot guessed the task and implemented an unrelated priority filter.
- **Speed is similar.** Copilot generates code faster (1 min 37 s on B2), but the agent spends its time on planning, verification and documentation that a developer would otherwise do by hand.
- **Thoroughness favors the agent.** It wrote 29 new test cases on B1, more than the human reference solution (17 test methods), and updated both READMEs. Copilot wrote 3 and 6 new test cases and updated only `docs/OpenAPI.md`.
- **Neither approach was perfect.** The agent interpreted one requirement too narrowly on B1 (section 6); Copilot missed validation and failure tests.

## 6. Edge cases, failure modes and limitations

### 6.1 Observed in the benchmark

- **Narrowed requirement (B1).** The agent did not add `userId` to `POST /api/todos/repeat`, although the issue asks for it in "creation/update contracts" and both the reference solution and Copilot include it. This was not an oversight: the planner recorded it as an explicit assumption (A7), interpreting the contracts as `POST` and `PUT` only, to change the least behavior and to avoid touching the `Repeat()` exception described in `rules/architecture.md`. A rule written to prevent unnecessary refactoring thus pushed the planner towards an interpretation that was too narrow. Implementation and QA then followed the plan, and all tests passed because no test required the missing field. This shows the main limit of the self-healing loop: it verifies the plan, not the issue.
- **Repairing a test instead of code (B1).** A new test expected `application/problem+json`, but the controller's existing `[Produces("application/json")]` returns `application/json` for all errors. The QA agent analyzed this, decided that the test was wrong, and documented why changing the shared attribute would affect every existing endpoint. The decision was correct, but it relies on the model's judgment of which side is wrong.
- **Pull request footer.** Claude Code appended a "Generated with Claude Code" line to both pull request descriptions, although the template in `prompts/pr-generator.md` does not contain it. Built-in behavior of the runtime can override prompt templates.
- **Input dependency of Copilot.** Without the issue text, Copilot produced plausible but wrong code with passing tests. The agent avoids this by fetching the issue itself with `gh issue view`.

### 6.2 Found during development

Trial runs on synthetic tasks and code review by team members revealed several problems before the benchmark:

| Problem | Fix |
|---|---|
| QA repairs and documentation would not be committed; `git diff --check` checked only uncommitted changes | Stage 6 commits the listed files and validates the full branch diff against the base |
| `dotnet format` reformatted untouched lines and changed a file encoding | The implementer formats only newly created files and checks `git diff` |
| New branches tracked `origin/main`, so a plain `git push` would target `main` | Branches are created with `--no-track` |
| The QA ceiling allowed 5 runs instead of 4; new warnings did not fail the run | At most 4 runs including the initial one; new warnings in changed files must be repaired |
| The planner could not express documentation-only issues | `Affected files: None` and manual checks for documentation criteria |
| The orchestrator treated an existing output file as success | Explicit `Status:` checks after every stage |
| Failures after the push were reported as "nothing published" | The release agent records the actual remote state |

These fixes are part of the current version and show that review by humans remains necessary even for a well-specified agent.

### 6.3 Limitations

- **Scope restrictions are not technically enforced.** Claude Code restricts which tools a subagent has, not which folders it may edit or which commands it may run. Hooks could add such checks; this version relies on instructions and on the stage 6 file check.
- **The plan is a single point of failure.** If the planner narrows or misses a requirement, later stages cannot notice it, as B1 shows. A possible improvement is a final check that compares the pull request against the original acceptance criteria, not only against the plan.
- **Possible contamination of B1.** The agent chose the same name (`NormalizeUserId`), the same 100-character limit and the same validation message for `userId` as the original solution, although we removed these details from the rules on the baseline branch. The transcript shows no access to the git history, and the choices follow existing patterns in the code (for example the message for the description limit), but the local repository still contained the original solution in its history, available to both approaches. A fresh clone containing only the baseline branch would rule this out.
- **Small sample.** Two issues and one person per approach do not allow general conclusions. Results also depend on the model version and on the person using Copilot.
- **Manual measurement of Copilot runs.** Copilot runs were timed and logged by hand. In B2 the first-attempt result and the number of repair cycles were not recorded and could not be recovered, so issue #47 is not fully met for that run; see the measurement gap in [`benchmark/README.md`](benchmark/README.md#measurement-gap-b2-copilot).
- **Unequal grounding.** The agent reads repository rules on every run; Copilot received only the issue text. Part of the quality difference comes from the rules rather than from autonomy itself.
- **Model behavior is not deterministic.** The same issue can produce different plans and code in another run. Occasionally the model also states unverified assumptions, for example a guess about a missing newline that the planner recorded without reading the file.

## 7. Conclusion

A workflow built only from prompts, rules and role definitions can implement realistic issues in a layered .NET application end to end without human intervention, at a speed comparable to an experienced developer with Copilot and with more thorough tests. The decisive design choices were the separation into stages with file-based handoffs and explicit status gates, repository-specific rules, a QA loop that diagnoses before repairing and may not weaken tests, and a release stage that commits only expected files. The main remaining weakness is requirement coverage: the agent verifies its own plan, so a requirement narrowed or missed during planning reaches the pull request unnoticed. Human review of agent pull requests therefore remains essential.
