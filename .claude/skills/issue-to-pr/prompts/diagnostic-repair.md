# Diagnostic repair prompt

Use this method to verify the implementation and repair failures in a bounded loop.

## Terms

- **Run:** one build and test execution.
- **Repair cycle:** one round of diagnosis and code changes, followed by a new run.
- **Initial run:** the first run, before any repair. It measures the first-attempt pass rate and is not a repair cycle.
- **Ceiling:** at most **4 repair cycles**, so at most 5 runs in total. This is the "4 attempts" limit in `SKILL.md`.

## 1. Verification run

Create the folder `<run folder>/qa/` and run both commands from the repository root. Write the full output of each run to a log file named after the run number (`0` for the initial run, then `1` to `4`).

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

The run **passes** when the build succeeds and every test passes. Then go to step 5.

## 2. Classify the failure

Read the logs and classify every problem:

| Class | How to recognize it | Action |
|---|---|---|
| Compiler error | `error CSxxxx` in the build log | Repair |
| Failing new test | A test added in this branch fails (see `implementation.md`) | Repair code or test, see step 3 |
| Regression | An existing test fails | Repair the production code |
| Environment problem | `dotnet` not found, restore or NuGet error, file locked by a running process, SDK missing | **Stop.** Code changes cannot fix it. |

Collect for each problem: the file, the line, the error code or test name, and the message.

## 3. Diagnose before you change anything

For each problem, write down a short diagnosis before editing:

1. **What failed:** the exact error or assertion, with the expected and actual values.
2. **Root cause:** why it failed, based on the code you have read, not on a guess. Read the whole file around the reported line.
3. **Fix:** the smallest change that removes the cause.

Decide which side is wrong:

- **Regression:** the production code is wrong. Never change the existing test.
- **Failing new test:** compare the test with the acceptance criteria in `plan.md`. If the code does not do what the plan requires, fix the code. If the test asserts something the plan does not require, or has a setup mistake, fix the test and keep it as strict as the plan requires.

Common compiler errors:

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

When many errors appear, fix the first ones first: later errors are often consequences of earlier ones.

## 4. Repair and run again

- Change only what your diagnosis requires. Keep the plan's design; do not rewrite working code.
- Follow `rules/` while repairing, the same as the implementer does.
- After repairing, start the next run (step 1).

Stop the loop with `Status: failed` when:

- the ceiling of 4 repair cycles is reached and the run still fails,
- the same failure (same error code or test name and the same message) remains after two consecutive repair cycles, because further attempts are unlikely to help,
- an environment problem occurs,
- a repair would require breaking a rule, changing files outside `src/` and `tests/`, or weakening a test.

## 5. Final check

When the run passes:

- run `git status --porcelain` and list every file you changed or created; these are the files stage 6 will commit,
- run `git diff` and confirm that every change belongs to a repair you recorded,
- confirm that no file outside `src/` and `tests/` was changed by you.

Build warnings do not fail the run. List warnings that point to files changed on this branch under **Notes**.

## 6. Write the report

Write `qa-report.md` in exactly this format:

```markdown
# QA report: #<issue number> <issue title>

Status: passed | failed
First run passed: yes | no
Repair cycles: <0-4>
Final result: <passed>/<total> tests passed, build <succeeded | failed>

## Runs

| Run | Build | Tests passed / total | Problems found | Files changed by the following repair |
|---|---|---|---|---|
| 0 | succeeded / failed | 162 / 164 | CS1061 in `src/...`, 2 failing tests | `src/...` |

## Diagnoses

### Repair cycle 1

- **Problem:** <error code or test name, file and line, message>
- **Root cause:** <why>
- **Fix:** <what was changed>

## Files changed by repairs

- `src/...`

(None if the initial run passed.)

## Remaining failures

- <failures left when Status is failed, or None>

## Notes

- <warnings in changed files, environment details, or None>

Logs: `<run folder>/qa/`
```
