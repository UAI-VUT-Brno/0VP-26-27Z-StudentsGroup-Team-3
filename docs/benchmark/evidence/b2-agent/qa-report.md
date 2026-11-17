# QA report: #55 [Benchmark] Change todo priority with PATCH /api/todos/{id}/priority

Status: passed
First run passed: yes
Runs: 1
Repair cycles: 0
Final result: 175/175 tests passed, build succeeded, 0 new warnings, 0 existing warnings

## Runs

| Run | Build | Tests passed / total | New warnings | Problems found | Files changed by the following repair |
|---|---|---|---|---|---|
| 1 | succeeded | 175 / 175 | 0 | None | None (no repair needed) |

New tests: 20 of the 175 (test cases counted in `run-1.trx`, theory rows counted on their own):

- `DTO/UpdateTodoPriorityRequestTests.cs`: 8 (`Validate_ValidRequest_PassesValidation` x3, `Validate_VersionBelowOne_FailsValidation` x2, `Validate_MissingVersion_FailsValidation`, `Validate_InvalidPriority_FailsValidation`, `Validate_MissingPriority_FailsValidation`)
- `Mapping/TodoMapperTests.cs`: 1 (`ApplyTo_PriorityRequest_ChangesOnlyPriorityAndUpdatedAtUtc`)
- `Controllers/TodosControllerTests.cs`: 3 (`UpdatePriority_ValidRequest_ReturnsUpdatedTodoWithIncrementedVersion`, `UpdatePriority_UnknownId_ReturnsNotFound`, `UpdatePriority_StaleVersion_ReturnsConflictAndPreservesData`)
- `Integration/TodoPriorityApiTests.cs`: 8 (`PatchPriority_ValidRequest_ChangesOnlyPriorityAndIncrementsVersion`, `PatchPriority_UnknownId_Returns404ProblemDetailsAndPreservesStorage`, `PatchPriority_StaleVersion_Returns409ProblemDetailsAndPreservesStorage`, `PatchPriority_InvalidBody_Returns400ValidationProblemAndPreservesStorage` x5)

Every test named in the plan's **Tests** table ran and passed.

## Diagnoses

None. The initial run passed.

## Files changed by repairs

None. The initial run passed.

## Warnings

### New warnings in changed files

- None

### Existing warnings in unchanged files

- None

## Remaining failures

- None

## Notes

- Commands: `dotnet build UAI.ZeroVP.sln --configuration Release` and `dotnet test UAI.ZeroVP.sln --configuration Release --no-build` (trx logger), run with `DOTNET_CLI_UI_LANGUAGE=en` in PowerShell on Windows 11.
- An incremental build can skip compilation and hide compiler warnings. To rule that out, a full rebuild (`dotnet build UAI.ZeroVP.sln --configuration Release --no-incremental`, log `run-1-rebuild.log`) was also run. It succeeded with 0 warnings and 0 errors.
- `git diff --check` passed both for the branch commits (`origin/benchmark/base-current...HEAD`) and for the working tree.
- Files changed on this branch (`git diff --name-only origin/benchmark/base-current...HEAD`): `src/UAI.ZeroVP.API/Controllers/TodosController.cs`, `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs`, `src/UAI.ZeroVP.DTO/UpdateTodoPriorityRequest.cs`, `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs`, `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs`, `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs`, `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs`. `git status --porcelain` is empty: QA left no changes in the working tree, and the run folder is ignored by git.
- Documentation (`docs/OpenAPI.md`, `README.md`, `README.cs.md`) is still to be done in stage 5. It is outside QA scope.

Logs: `.issue-to-pr/55/qa/`
