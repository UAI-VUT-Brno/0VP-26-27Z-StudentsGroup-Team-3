# Implementation: #54 [Benchmark] Add user identification to tasks and "My Tasks" filtering

Status: done
Branch: feat/t3-54-user-task-filtering

## Changed files

| File | Change | Covers |
|---|---|---|
| `src/UAI.ZeroVP.Data/Entities/TodoItem.cs` | modify | R1, R6 |
| `src/UAI.ZeroVP.Data/Abstractions/ITodoRepository.cs` | modify | R4, R5 |
| `src/UAI.ZeroVP.Data/InMemory/InMemoryTodoRepository.cs` | modify | R1, R4, R5 |
| `src/UAI.ZeroVP.Data/Json/JsonFileTodoRepository.cs` | modify | R1, R4, R5, R6 |
| `src/UAI.ZeroVP.DTO/TodoItemDto.cs` | modify | R2 |
| `src/UAI.ZeroVP.DTO/CreateTodoItemRequest.cs` | modify | R3, AC6 |
| `src/UAI.ZeroVP.DTO/UpdateTodoItemRequest.cs` | modify | R3, AC6 |
| `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs` | modify | R2, R3, A2 |
| `src/UAI.ZeroVP.API/Controllers/TodosController.cs` | modify | R4, R5, R6, AC6 |
| `tests/UAI.ZeroVP.Tests/Performance/PerformanceProfilingTests.cs` | modify (compile fix) | R6 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | modify | R8 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | modify | R8 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | add | R8 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | modify | R8 |
| `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs` | modify | R8 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | add | R8 |

## Commits

| Commit | Message |
|---|---|
| `b3cb0cd` | `feat(#54): Add userId to todos and filter GET /api/todos by user` |
| `0168c87` | `test(#54): Cover userId tagging and user filtering` |

## Tests added or changed

- `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs`: `ToEntity_TrimsUserId`, `ToEntity_WhitespaceUserId_SetsNull`, `ToDto_MapsUserId`, `ApplyTo_SetsTrimmedUserId`, `ApplyTo_OmittedUserId_ClearsUserId`
- `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs`: `CreateTodoItemRequest_UserIdOmitted_PassesValidation`, `CreateTodoItemRequest_UserIdAtMaxLength_PassesValidation`, `CreateTodoItemRequest_UserIdTooLong_FailsValidation`, `UpdateTodoItemRequest_UserIdTooLong_FailsValidation`
- `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs`: `Create_WithUserId_ReturnsTrimmedUserId`, `GetAll_WithUserIdFilter_ReturnsOnlyMatchingTodos`, `GetAll_WhitespaceUserId_ReturnsAllTodos`; existing `GetAll_EmptyRepository_ReturnsOkWithEmptyList` gets `userId: null` (compile fix, see Deviations)
- `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs`: `GetByIdAsync_ReturnsStoredUserId`, `GetAllAsync_FiltersByUserId`, `GetAllAsync_FiltersByUserIdAndCompletionStatus`, `GetAllAsync_UserIdFilter_IsCaseSensitive`, `GetAllAsync_UserIdFilter_ExcludesTodosWithoutUserId` (plus private helper `CreateUserItem`)
- `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs`: `UserId_PersistsAcrossRepositoryInstances`, `GetAllAsync_FiltersByUserIdAndCompletionStatus`, `LegacyJsonWithoutUserId_LoadsWithNullUserId` (plus private helper `CreateUserItem`)
- `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs`: `Create_WithUserId_Returns201WithUserId`, `Create_WithoutUserId_Returns201WithNullUserId`, `Create_UserIdTooLong_Returns400`, `GetAll_WithUserId_ReturnsOnlyThatUsersTodos`, `GetAll_WithUserIdAndIsCompleted_ReturnsMatchingSubset`, `GetAll_WithoutUserId_ReturnsTodosWithAndWithoutUserId`, `GetAll_UnknownUserId_ReturnsEmptyList`, `GetAll_UserIdTooLong_Returns400`, `Update_WithUserId_ChangesUserId`
- `tests/UAI.ZeroVP.Tests/Performance/PerformanceProfilingTests.cs`: `GetAll(null, ct)` changed to `GetAll(null, null, ct)` (compile fix from plan A9, no assertion change)

## Deviations from the plan

- `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs`, `GetAll_EmptyRepository_ReturnsOkWithEmptyList`: the plan (A9) states this test compiles unchanged because it uses named arguments. It does not: the new `userId` parameter has no default value, and C# does not allow skipping a non-optional parameter with named arguments (CS7036). Added `userId: null,` to the call. This is a mechanical compile fix; the assertions are unchanged. The alternative (giving `userId` and `cancellationToken` default values in the controller) would have changed the planned production signature.

## Notes

- Build and tests were not run (QA stage responsibility).
- `dotnet format whitespace --folder` was run on the two new test files only; all changed `.cs` files are UTF-8 with BOM and CRLF, and `git diff --cached --check` passed before both commits.
- No Co-Authored-By trailers, per `rules/git-conventions.md`.
- The `GetAll` controller unit tests call the action directly, so `[StringLength]` on the query parameter is only exercised by the integration test `GetAll_UserIdTooLong_Returns400`.
- Documentation (`docs/OpenAPI.md`, `README.md`, `README.cs.md`) is left for stage 5.
