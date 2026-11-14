# Diagnostic repair prompt

Use this method to verify the implementation and repair failures in a bounded loop.

## Terms

- **Run:** one build and test execution. Runs are numbered `1` to `4`.
- **Initial run:** run 1, before any repair. It measures the first-attempt pass rate and is not a repair cycle.
- **Repair cycle:** one round of diagnosis and code changes, followed by the next run.
- **Ceiling:** at most **4 runs** in total, including the initial run, so at most **3 repair cycles**. This is the "4 attempts" limit in `SKILL.md`.
- **Changed files:** files changed on this branch, that is the output of `git diff --name-only origin/<base>...HEAD` (base branch from `branch.md`) together with the files listed by `git status --porcelain`.
- **New warning:** a build warning reported in a changed file. `rules/code-style.md` forbids new warnings, so it must be repaired.
- **Existing warning:** a build warning reported in a file that is not changed on this branch. It is recorded but does not fail the run.

## 1. Verification run

Create the folder `<run folder>/qa/` and run both commands from the repository root. Write the full output of each run to log files named after the run number (`1` for the initial run, then `2` to `4`).

PowerShell:

```powershell
$env:DOTNET_CLI_UI_LANGUAGE = "en"
dotnet build UAI.ZeroVP.sln --configuration Release *> <run folder>/qa/run-<n>-build.log
dotnet test UAI.ZeroVP.sln --configuration Release --no-build --logger "trx;LogFileName=run-<n>.trx" --results-directory <run folder>/qa *> <run folder>/qa/run-<n>-test.log
```

Bash:

```bash
export DOTNET_CLI_UI_LANGUAGE=en
dotnet build UAI.ZeroVP.sln --configuration Release > <run folder>/qa/run-<n>-build.log 2>&1
dotnet test UAI.ZeroVP.sln --configuration Release --no-build --logger "trx;LogFileName=run-<n>.trx" --results-directory <run folder>/qa > <run folder>/qa/run-<n>-test.log 2>&1
```

- `DOTNET_CLI_UI_LANGUAGE=en` makes the output English on any system language, so diagnostics are always in the same format.
- If the build fails, skip the test command for this run.
- Take test results from the `.trx` file (counts and failed tests with their messages), because it does not depend on the console language.
- Collect every `warning` line from the build log with its file, line, code and message, and sort it into new and existing warnings using the changed files.

The run **passes** when the build succeeds, every test passes and there are no new warnings. Then go to step 5.

## 2. Classify the failure

Read the logs and classify every problem:

| Class | How to recognize it | Action |
|---|---|---|
| Compiler error | `error CSxxxx` in the build log | Repair |
| New warning | `warning` in the build log for a changed file | Repair |
| Failing new test | A test added in this branch fails (see `implementation.md`) | Repair code or test, see step 3 |
| Regression | An existing test fails | Repair the production code |
| Existing warning | `warning` in the build log for a file not changed on this branch | Record only; do not repair |
| Environment problem | `dotnet` not found, restore or NuGet error, file locked by a running process, SDK missing | **Stop.** Code changes cannot fix it. |

Collect for each problem: the file, the line, the error or warning code or test name, and the message.

## 3. Diagnose before you change anything

For each problem, write down a short diagnosis before editing:

1. **What failed:** the exact error, warning or assertion, with the expected and actual values.
2. **Root cause:** why it failed, based on the code you have read, not on a guess. Read the whole file around the reported line.
3. **Fix:** the smallest change that removes the cause.

Decide which side is wrong:

- **Regression:** the production code is wrong. Never change the existing test.
- **Failing new test:** compare the test with the acceptance criteria in `plan.md`. If the code does not do what the plan requires, fix the code. If the test asserts something the plan does not require, or has a setup mistake, fix the test and keep it as strict as the plan requires.
- **New warning:** fix the code that causes it. Never suppress it with `#pragma warning disable`, `<NoWarn>` or a changed project setting.

Common compiler errors and warnings:

| Code | Typical cause in this repository | Typical fix |
|---|---|---|
| CS0246 | Type not found | Add the missing `using`, or correct the type name |
| CS0103 | Name does not exist | Correct the name, or declare the missing member |
| CS1061 | Member does not exist on the type | Use the correct member; check the type definition |
| CS7036 | Missing required argument | Update every call site, including tests and test helpers |
| CS0535 | Interface member not implemented | Implement it in **both** `InMemoryTodoRepository` and `JsonFileTodoRepository` |
| CS9035 | Required member not set | Set every `required` property in object initializers, such as `Id`, `Title`, `CreatedAtUtc` on `TodoItem` |
| CS0029, CS1503 | Type mismatch | Convert explicitly, or correct the type |
| CS1591 | Missing XML comment on a public member (warning) | Add a `/// <summary>` |
| CS8600–CS8625 | Nullable reference warning | Handle `null` explicitly or correct the nullability of the type |

When many errors appear, fix the first ones first: later errors are often consequences of earlier ones. Repair errors and failing tests before new warnings.

## 4. Repair and run again

- Change only what your diagnosis requires. Keep the plan's design; do not rewrite working code.
- Follow `rules/` while repairing, the same as the implementer does.
- After repairing, start the next run (step 1).

Stop the loop with `Status: failed` when:

- run 4 does not pass (the ceiling is reached); this includes new warnings that remain after run 4,
- the same failure (same error, warning code or test name and the same message) remains after two consecutive repair cycles, because further attempts are unlikely to help,
- an environment problem occurs,
- a repair would require breaking a rule, changing files outside `src/` and `tests/`, suppressing a warning or weakening a test.

When you stop early, do not start the remaining runs. Record the reason under **Remaining failures**.

## 5. Final check

When the run passes:

- run `git status --porcelain` and list every file you changed or created; these are the files stage 6 will commit,
- run `git diff` and confirm that every change belongs to a repair you recorded,
- confirm that no file outside `src/` and `tests/` was changed by you,
- confirm that the last build reported no new warnings, and list existing warnings under **Warnings**.

## 6. Write the report

Write `qa-report.md` in exactly this format:

```markdown
# QA report: #<issue number> <issue title>

Status: passed | failed
First run passed: yes | no
Runs: <1-4>
Repair cycles: <0-3>
Final result: <passed>/<total> tests passed, build <succeeded | failed>, <n> new warnings, <n> existing warnings

## Runs

| Run | Build | Tests passed / total | New warnings | Problems found | Files changed by the following repair |
|---|---|---|---|---|---|
| 1 | succeeded / failed | 162 / 164 | 1 | CS1061 in `src/...`, 2 failing tests, CS1591 in `src/...` | `src/...` |

## Diagnoses

### Repair cycle 1

- **Problem:** <error or warning code or test name, file and line, message>
- **Root cause:** <why>
- **Fix:** <what was changed>

## Files changed by repairs

- `src/...`

(None if the initial run passed.)

## Warnings

### New warnings in changed files

- <code, file and line, message, or None>

### Existing warnings in unchanged files

- <code, file and line, message, or None>

## Remaining failures

- <errors, failing tests or new warnings left when Status is failed, the reason for stopping, or None>

## Notes

- <environment details, or None>

Logs: `<run folder>/qa/`
```

`Status: passed` requires a successful build, all tests passing and no new warnings in the last run.