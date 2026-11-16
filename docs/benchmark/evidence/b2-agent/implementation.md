# Implementation: #55 [Benchmark] Change todo priority with PATCH /api/todos/{id}/priority

Status: done
Branch: feat/t3-55-todo-priority-patch

## Changed files

| File | Change | Covers |
|---|---|---|
| `src/UAI.ZeroVP.DTO/UpdateTodoPriorityRequest.cs` | add | R2, R7 |
| `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs` | modify | R3 |
| `src/UAI.ZeroVP.API/Controllers/TodosController.cs` | modify | R1, R3, R4, R5, R6, R7 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | add | R7, R8 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | modify | R3, R8 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | modify | R4, R5, R6, R8 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | add | R1-R7, R8 |

## Commits

| Commit | Message |
|---|---|
| `d500a49` | `feat(#55): Add PATCH /api/todos/{id}/priority endpoint` |
| `5f9f8a1` | `test(#55): Cover priority PATCH success, validation, 404 and 409` |

## Tests added or changed

- `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs`: `Validate_ValidRequest_PassesValidation` (theory Low/Normal/High), `Validate_MissingVersion_FailsValidation`, `Validate_VersionBelowOne_FailsValidation` (theory 0, -1), `Validate_InvalidPriority_FailsValidation`, `Validate_MissingPriority_FailsValidation`
- `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs`: `ApplyTo_PriorityRequest_ChangesOnlyPriorityAndUpdatedAtUtc`
- `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs`: `UpdatePriority_ValidRequest_ReturnsUpdatedTodoWithIncrementedVersion`, `UpdatePriority_UnknownId_ReturnsNotFound`, `UpdatePriority_StaleVersion_ReturnsConflictAndPreservesData`
- `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs`: `PatchPriority_ValidRequest_ChangesOnlyPriorityAndIncrementsVersion`, `PatchPriority_UnknownId_Returns404ProblemDetailsAndPreservesStorage`, `PatchPriority_StaleVersion_Returns409ProblemDetailsAndPreservesStorage`, `PatchPriority_InvalidBody_Returns400ValidationProblemAndPreservesStorage` (theory, 5 bodies)

## Deviations from the plan

- None. The success log message is written only after a successful update, so the action uses an explicit `if (!updated)` branch instead of the ternary in `Update`; behavior is identical.
- In `TodoMapperTests.cs` a blank line was added between the existing `ApplyTo_UpdatesTodoEntity` test and the new test (the existing file had no blank line before the following `[Theory]`); no existing test changed.

## Notes

- Not built or tested (per stage rules); QA should run `dotnet build UAI.ZeroVP.sln --configuration Release` and `dotnet test UAI.ZeroVP.sln --configuration Release`.
- New `.cs` files were normalized with `dotnet format whitespace --folder` (UTF-8 with BOM, CRLF). Modified files kept UTF-8 with BOM and CRLF; `git diff --cached --check` passed for both commits.
- The integration theory case `{"priority":99,"version":1}` relies on `JsonStringEnumConverter` accepting integers (default) and `[EnumDataType]` rejecting the undefined value (plan risk 1).
- Documentation (`docs/OpenAPI.md`, `README.md`, `README.cs.md`) is left for stage 5.
- Commits have no trailers, per `rules/git-conventions.md`.
