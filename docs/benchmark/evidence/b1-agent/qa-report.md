# QA report: #54 [Benchmark] Add user identification to tasks and "My Tasks" filtering

Status: passed
First run passed: no
Runs: 2
Repair cycles: 1
Final result: 115/115 tests passed, build succeeded, 0 new warnings, 0 existing warnings

## Runs

| Run | Build | Tests passed / total | New warnings | Problems found | Files changed by the following repair |
|---|---|---|---|---|---|
| 1 | succeeded | 114 / 115 | 0 | 1 failing new test: `UserTodoFilterApiTests.GetAll_UserIdTooLong_Returns400` (content type assertion) | `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` |
| 2 | succeeded | 115 / 115 | 0 | None | - |

Test totals for the final run: 115 total, 115 passed, 0 failed, 0 skipped. 29 tests are new on this branch: 5 mapping, 4 DTO validation, 3 controller, 5 in-memory repository, 3 JSON repository and 9 integration. All 86 existing tests pass. The only change to an existing test is the compile fix listed in `implementation.md`; no assertion was changed.

`git diff --check` passed after both runs.

## Diagnoses

### Repair cycle 1

- **Problem:** `UAI.ZeroVP.Tests.Integration.UserTodoFilterApiTests.GetAll_UserIdTooLong_Returns400`, `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` line 235: `Assert.Equal() Failure: Strings differ`. Expected `"application/problem+json"`, actual `"application/json"`. The status code assertion (400) passed.
- **Root cause:** `TodosController` has a class-level `[Produces("application/json")]`. That attribute was already present at base `95e4ab9`, so it is not part of this branch. It overrides content negotiation for every result, including the automatic `ValidationProblemDetails` from `[ApiController]`, so every existing 400 on this controller (POST, PUT, repeat) is also served as `application/json`. The response body is a correct `ValidationProblemDetails` with status 400 and errors. The test asserted a media type the existing API contract does not produce. AC6 requires "400 ValidationProblemDetails", not a specific media type. That expectation came from the plan's test description, and the plan is wrong about it. The production code meets the acceptance criterion. Changing the controller's `[Produces]` would change the error content type of every existing endpoint, which is out of scope (`rules/architecture.md`).
- **Fix:** The test side is wrong (a setup mistake in a new test), so I corrected the test. The expected media type is now `application/json`, which is the actual contract, so the test still checks the content type. I also added an assertion that the errors contain the plan's message `"User ID cannot exceed 100 characters."` (A1), so the test now proves the 400 comes from the `userId` validation. The test is at least as strict as the plan requires. No production code changed.

## Files changed by repairs

- `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` (still UTF-8 with BOM and CRLF)

## Warnings

### New warnings in changed files

- None

### Existing warnings in unchanged files

- None

## Remaining failures

- None

## Notes

- `DOTNET_CLI_UI_LANGUAGE=en`; .NET SDK build on Windows 11 (Git Bash).
- The normal builds are incremental, so they can skip compiling an unchanged project and then not report its warnings. To confirm there were no warnings, I also ran a full `dotnet build UAI.ZeroVP.sln --configuration Release --no-incremental` after run 2 as a diagnostic, not counted as a run: 0 warnings, 0 errors (`qa/run-2-rebuild-warning-check.log`).
- For stage 5 (documentation): error responses from `TodosController`, including the new `GET /api/todos?userId=` 400, have the content type `application/json`, not `application/problem+json`, because of the existing controller-level `[Produces("application/json")]`. `docs/OpenAPI.md` should not claim `application/problem+json`.
- For stage 6 (commit): the repaired test file is not committed yet; suggested commit `test(#54): Align userId validation test with the controller content type`.

Logs: `.issue-to-pr/54/qa/`
