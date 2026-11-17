# Plan: #54 [Benchmark] Add user identification to tasks and "My Tasks" filtering

Status: ready
Task ID: none in the issue title; fallback per `rules/git-conventions.md`: `t3-54` in branch names, commit scope `#54` (for example `feat(#54): Add userId to todos and GET /api/todos filter`)
Branch: feat/t3-54-user-task-filtering (base: benchmark/base-t3-10)

## Goal

Todos can carry an optional string `userId`, set through `POST /api/todos` and `PUT /api/todos/{id}`, and `GET /api/todos` can filter by `userId`, alone or together with the existing `isCompleted` filter, without breaking clients or stored data that do not use `userId`.

## Requirements

- R1: Add an optional `UserId` (string) to the `TodoItem` entity and persist it in both repositories (in-memory and JSON file).
- R2: Expose `userId` in the response DTO `TodoItemDto`.
- R3: Accept an optional `userId` in the create contract (`CreateTodoItemRequest`, `POST /api/todos`) and the update contract (`UpdateTodoItemRequest`, `PUT /api/todos/{id}`).
- R4: `GET /api/todos` accepts an optional `userId` query parameter that returns only todos belonging to that user.
- R5: The `userId` filter can be combined with the existing `isCompleted` filter (logical AND).
- R6: Backward compatibility: requests without `userId`, todos without `userId`, and existing JSON storage files without a `userId` property keep working. Omitting the `userId` query parameter returns the same result as before.
- R7: Update `docs/OpenAPI.md` with the new query parameter and fields.
- R8: Add unit and integration tests for user tagging and filtered queries.

## Acceptance criteria

- AC1: A todo can be created with a `userId`, and the value is returned in the response and stored (verified by: `Create_WithUserId_Returns201WithUserId` (integration), `Create_WithUserId_ReturnsTrimmedUserId` (controller), `ToEntity_TrimsUserId`, `GetByIdAsync_ReturnsStoredUserId`, `UserId_PersistsAcrossRepositoryInstances`).
- AC2: A todo can be (re)tagged with a `userId` through `PUT` (verified by: `Update_WithUserId_ChangesUserId` (integration), `ApplyTo_SetsTrimmedUserId`).
- AC3: `GET /api/todos?userId={userId}` returns only todos of that user (verified by: `GetAll_WithUserId_ReturnsOnlyThatUsersTodos` (integration), `GetAll_WithUserIdFilter_ReturnsOnlyMatchingTodos` (controller), `GetAllAsync_FiltersByUserId` (in-memory), `GetAll_UnknownUserId_ReturnsEmptyList`).
- AC4: Combining `userId` and `isCompleted` works correctly (verified by: `GetAll_WithUserIdAndIsCompleted_ReturnsMatchingSubset` (integration), `GetAllAsync_FiltersByUserIdAndCompletionStatus` in both repository test classes).
- AC5: Backward compatibility for todos without `userId` (verified by: `Create_WithoutUserId_Returns201WithNullUserId`, `GetAll_WithoutUserId_ReturnsTodosWithAndWithoutUserId`, `GetAll_WhitespaceUserId_ReturnsAllTodos`, `GetAllAsync_UserIdFilter_ExcludesTodosWithoutUserId`, `LegacyJsonWithoutUserId_LoadsWithNullUserId`, `ToEntity_WhitespaceUserId_SetsNull`, all existing tests still pass unchanged except the one compile fix listed under Affected files).
- AC6: Invalid input is rejected with 400 ValidationProblemDetails (verified by: `Create_UserIdTooLong_Returns400`, `GetAll_UserIdTooLong_Returns400`, `CreateTodoItemRequest_UserIdTooLong_FailsValidation`, `UpdateTodoItemRequest_UserIdTooLong_FailsValidation`).
- AC7: `docs/OpenAPI.md` documents the `userId` query parameter and the `userId` field (verified by: Manual check: `docs/OpenAPI.md` lists `GET /api/todos` with optional `isCompleted` and `userId` filters and status 400, and contains a section "User Identification and \"My Tasks\" Filtering" describing the field, validation limit, trimming, exact case-sensitive matching, the combination with `isCompleted`, PUT full-update behavior and backward compatibility).
- AC8: Automated tests cover the new field and filtering (verified by: all tests in the **Tests** table exist and pass with `dotnet test UAI.ZeroVP.sln --configuration Release`).

## Assumptions

- A1 (validation limit): `userId` is optional, maximum 100 characters, no character-set restriction. Error message: `"User ID cannot exceed 100 characters."`. The same `[StringLength(100, ...)]` is applied to `CreateTodoItemRequest.UserId`, `UpdateTodoItemRequest.UserId` and the `userId` query parameter of `GetAll`. The issue gives no limit; 100 follows the existing pattern of bounded text fields (title 200, description 1000).
- A2 (normalization): `userId` is trimmed; empty or whitespace-only input becomes `null` (rule in `rules/domain.md`: "Trim text input in the mapper. Convert empty or whitespace-only optional text to `null`."). A single helper `TodoMapper.NormalizeUserId(string? userId)` is used by `ToEntity`, `ApplyTo` and the controller's `GetAll`, so normalization stays in `TodoMapper`.
- A3 (filter semantics): the filter matches exactly, ordinal and case-sensitive (`string.Equals(item.UserId, userId, StringComparison.Ordinal)`), after the query value is trimmed. `userId` is an opaque identifier, so `alice` and `Alice` are different users. An empty or whitespace-only `?userId=` is treated as "no filter" (same as omitting it). Todos without `userId` never match a non-null `userId` filter. Filters combine with AND. Sorting (priority desc, then `CreatedAtUtc`) is unchanged.
- A4 (PUT without `userId`): `PUT /api/todos/{id}` is a documented full update ("omitted properties will be overwritten with default values" in `UpdateTodoItemRequest`). Omitting `userId` therefore sets it to `null`, exactly like `description`. This keeps the existing contract consistent; it is documented in `docs/OpenAPI.md`.
- A5 (JSON compatibility): existing `todos.json` files have no `userId` property. `System.Text.Json` leaves a missing property at its default (`null`) for a nullable `string?` without `required`, so no migration or converter is needed. New files will contain `"UserId": null` or the value (the repository's `JsonOptions` uses default PascalCase property names). Covered by `LegacyJsonWithoutUserId_LoadsWithNullUserId`.
- A6 (response shape): `TodoItemDto.UserId` is serialized as `"userId": null` for untagged todos (no `DefaultIgnoreCondition` is configured in `Program.cs`, same as `dueAtUtc`). Adding a property is a backward-compatible change.
- A7 (recurring todos): the issue names "creation/update contracts"; this plan interprets them as `POST /api/todos` and `PUT /api/todos/{id}`. `POST /api/todos/repeat` (`CreateRecurringTodoRequest`) is not changed; its occurrences get `UserId = null`. This changes the least behavior and avoids touching the `Repeat()` exception described in `rules/architecture.md`.
- A8 (repository signature): `ITodoRepository.GetAllAsync` gets a new optional parameter inserted before the token: `GetAllAsync(bool? isCompleted = null, string? userId = null, CancellationToken cancellationToken = default)`. All existing callers (`GetAllAsync()`, `GetAllAsync(true)`, `GetAllAsync(false)` in tests) still compile; the only positional `(isCompleted, cancellationToken)` caller is `TodosController.GetAll`, which is updated. The repository expects an already normalized value and does not trim.
- A9 (controller signature): `GetAll` becomes `GetAll([FromQuery] bool? isCompleted, [FromQuery] [StringLength(100, ErrorMessage = "User ID cannot exceed 100 characters.")] string? userId, CancellationToken cancellationToken)`. `[ApiController]` validates DataAnnotations on action parameters and returns 400 `ValidationProblemDetails` automatically. The positional call `GetAll(null, ct)` in `PerformanceProfilingTests` no longer compiles and must be changed to `GetAll(null, null, ct)`; this is a mechanical compile fix that does not weaken the test. `TodosControllerTests.GetAll_EmptyRepository_ReturnsOkWithEmptyList` uses named arguments and compiles unchanged.
- A10 (no "unassigned" filter): there is no way to query only todos without `userId`; the issue does not ask for it.
- No rule is overridden by the issue.

## Affected files

| File | Layer | Change | Reason | Covers |
|---|---|---|---|---|
| `src/UAI.ZeroVP.Data/Entities/TodoItem.cs` | Data | modify | Add `public string? UserId { get; set; }` with XML doc ("Optional identifier of the user the todo belongs to; `null` = not assigned."); copy `UserId` in `Clone()` (otherwise the in-memory repository and JSON rollback lose it). Not `required`, so legacy JSON loads. | R1, R6 |
| `src/UAI.ZeroVP.Data/Abstractions/ITodoRepository.cs` | Data | modify | `GetAllAsync(bool? isCompleted = null, string? userId = null, CancellationToken cancellationToken = default)`; update XML doc: `<param name="userId">` "Exact, case-sensitive user identifier filter; `null` = no filter." and summary "optionally filtered by completion status and user". | R4, R5 |
| `src/UAI.ZeroVP.Data/InMemory/InMemoryTodoRepository.cs` | Data | modify | Implement new signature; add `.Where(item => userId is null \|\| string.Equals(item.UserId, userId, StringComparison.Ordinal))` next to the existing `isCompleted` filter. Seed data unchanged. | R1, R4, R5 |
| `src/UAI.ZeroVP.Data/Json/JsonFileTodoRepository.cs` | Data | modify | Same signature and identical filter in `GetAllAsync`. No serializer change needed (A5). Seed data unchanged. | R1, R4, R5, R6 |
| `src/UAI.ZeroVP.DTO/TodoItemDto.cs` | DTO | modify | Add `public string? UserId { get; init; }` with XML doc ("Optional identifier of the user the todo belongs to, or `null`."). | R2 |
| `src/UAI.ZeroVP.DTO/CreateTodoItemRequest.cs` | DTO | modify | Add `[StringLength(100, ErrorMessage = "User ID cannot exceed 100 characters.")] public string? UserId { get; init; }` with XML doc ("Optional user identifier, maximum of 100 characters. Trimmed; empty means not assigned."). | R3, AC6 |
| `src/UAI.ZeroVP.DTO/UpdateTodoItemRequest.cs` | DTO | modify | Same property and attribute as in `CreateTodoItemRequest`; XML doc states that omitting it clears the value (full update, A4). Do not touch `Version`. | R3, AC6 |
| `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs` | API | modify | Add `public static string? NormalizeUserId(string? userId) => string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();` with XML doc; map `UserId = entity.UserId` in `ToDto`; `UserId = NormalizeUserId(request.UserId)` in `ToEntity`; `entity.UserId = NormalizeUserId(request.UserId);` in `ApplyTo`. | R2, R3, A2 |
| `src/UAI.ZeroVP.API/Controllers/TodosController.cs` | API | modify | `GetAll`: add `[FromQuery] [StringLength(100, ErrorMessage = "User ID cannot exceed 100 characters.")] string? userId` between `isCompleted` and `cancellationToken` (one argument per line); call `_repository.GetAllAsync(isCompleted, TodoMapper.NormalizeUserId(userId), cancellationToken)`; update XML: summary "optionally filtered by completion status and user", `<param name="userId">`, `<response code="400">Query parameter validation failed.</response>`; add `[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]`. Needs `using System.ComponentModel.DataAnnotations;`. `Create`, `Update`, `Repeat` need no code change (mapping handles `userId`). | R4, R5, R6, AC6 |
| `tests/UAI.ZeroVP.Tests/Performance/PerformanceProfilingTests.cs` | Tests | modify | Compile fix only: `_todosController.GetAll(null, ct)` becomes `_todosController.GetAll(null, null, ct)` (A9). No assertion changes. | R6 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | Tests | modify | Add controller unit tests (see Tests). | R8 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | Tests | modify | Add mapping tests (see Tests). | R8 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | Tests | add | DTO validation tests for `UserId` on create and update requests; use `Validator.TryValidateObject(..., validateAllProperties: true)` like `CreateRecurringTodoRequestTests`. No repository. | R8 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | Tests | modify | Add in-memory filter tests using the existing `CreateEmptyRepository()` helper. | R8 |
| `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs` | Tests | modify | Add JSON tests using the existing isolated temp folder (`_tempFilePath`, `SeedSampleData = false`). | R8 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | Tests | add | HTTP integration tests with `WebApplicationFactory<Program>`; replace `ITodoRepository` with `InMemoryTodoRepository { SeedSampleData = false }` via `ConfigureTestServices`, exactly as `RecurringTodoApiTests.CreateFactory()`, so no developer data is touched. Use a fresh factory per test. | R8 |

## Tests

| Test file | Test name | Type | Covers |
|---|---|---|---|
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ToEntity_TrimsUserId` | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ToEntity_WhitespaceUserId_SetsNull` | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ToDto_MapsUserId` | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ApplyTo_SetsTrimmedUserId` | success | AC2 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ApplyTo_OmittedUserId_ClearsUserId` | success | AC2, A4 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | `CreateTodoItemRequest_UserIdOmitted_PassesValidation` | success | AC5 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | `CreateTodoItemRequest_UserIdAtMaxLength_PassesValidation` (100 chars) | success | AC6 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | `CreateTodoItemRequest_UserIdTooLong_FailsValidation` (101 chars) | failure | AC6 |
| `tests/UAI.ZeroVP.Tests/DTO/TodoUserIdValidationTests.cs` | `UpdateTodoItemRequest_UserIdTooLong_FailsValidation` (101 chars, valid title and `Version = 1`) | failure | AC6 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `Create_WithUserId_ReturnsTrimmedUserId` | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `GetAll_WithUserIdFilter_ReturnsOnlyMatchingTodos` | success | AC3 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `GetAll_WhitespaceUserId_ReturnsAllTodos` | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | `GetByIdAsync_ReturnsStoredUserId` | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | `GetAllAsync_FiltersByUserId` | success | AC3 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | `GetAllAsync_FiltersByUserIdAndCompletionStatus` | success | AC4 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | `GetAllAsync_UserIdFilter_IsCaseSensitive` (`"Alice"` does not match `"alice"`) | failure | AC3, A3 |
| `tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs` | `GetAllAsync_UserIdFilter_ExcludesTodosWithoutUserId` (and `GetAllAsync()` still returns them) | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs` | `UserId_PersistsAcrossRepositoryInstances` | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs` | `GetAllAsync_FiltersByUserIdAndCompletionStatus` | success | AC3, AC4 |
| `tests/UAI.ZeroVP.Tests/Data/JsonFileTodoRepositoryTests.cs` | `LegacyJsonWithoutUserId_LoadsWithNullUserId` (write a JSON array with one item without a `UserId` property to `_tempFilePath` before creating the repository; assert it loads with `UserId == null`, appears in `GetAllAsync()`, is excluded by `GetAllAsync(userId: "alice")`, and no `.corrupt-*.bak` file is created) | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `Create_WithUserId_Returns201WithUserId` (send `"  alice  "`, expect `"alice"`) | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `Create_WithoutUserId_Returns201WithNullUserId` | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `Create_UserIdTooLong_Returns400` (assert no todo was stored) | failure | AC6 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `GetAll_WithUserId_ReturnsOnlyThatUsersTodos` | success | AC3 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `GetAll_WithUserIdAndIsCompleted_ReturnsMatchingSubset` (alice open, alice completed via `POST /complete`, bob completed, untagged completed; `?userId=alice&isCompleted=true` returns only alice's completed todo) | success | AC4 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `GetAll_WithoutUserId_ReturnsTodosWithAndWithoutUserId` | success | AC5 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `GetAll_UnknownUserId_ReturnsEmptyList` | failure | AC3 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `GetAll_UserIdTooLong_Returns400` (101-char query value, `application/problem+json`) | failure | AC6 |
| `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` | `Update_WithUserId_ChangesUserId` (PUT with current version; then `?userId=` new value finds it and the old value does not) | success | AC2 |

## Public contract changes

- `GET /api/todos`: new optional query parameter `userId` (string, max 100 characters, trimmed, exact case-sensitive match, combinable with `isCompleted`). New possible response `400 Bad Request` (`ValidationProblemDetails`) when `userId` is too long. Without `userId`, behavior is unchanged.
- `POST /api/todos` (`CreateTodoItemRequest`): new optional body field `userId` (max 100 characters).
- `PUT /api/todos/{id}` (`UpdateTodoItemRequest`): new optional body field `userId` (max 100 characters); omitted or blank clears it (full update).
- `TodoItemDto` (all todo responses, including `POST /api/todos/repeat` and `POST /api/todos/{id}/complete`): new field `userId` (`string` or `null`).
- `ITodoRepository.GetAllAsync`: new optional `userId` parameter (internal contract; both implementations updated).
- No MCP tools or configuration options change.

## Documentation impact

| File | Change |
|---|---|
| `docs/OpenAPI.md` | In "TodosController": change the `GET /api/todos` line to "200 OK (task list with optional `isCompleted` and `userId` filters) / 400 Bad Request (ValidationProblemDetails)", and mention the optional `userId` body field on `POST /api/todos` and `PUT /api/todos/{id}`. Add a section `## User Identification and "My Tasks" Filtering` with: query parameter table (`isCompleted`, `userId`), example `GET /api/todos?userId=alice&isCompleted=false`, request body example for `POST /api/todos` with `userId`, response example with `"userId": "alice"`, validation (optional, max 100 characters, trimmed, blank = not assigned / no filter), matching semantics (exact, case-sensitive, AND with `isCompleted`, untagged todos excluded when filtering), PUT full-update behavior (omitting `userId` clears it), note that `userId` is not authentication, and backward compatibility (requests without `userId`, existing todos and existing JSON files without `userId` keep working; untagged todos return `"userId": null`). Do not rewrite the existing recurring section. |
| `README.md` | Required by `rules/domain.md` (usage change): in the "API endpoints" table, change the `GET /api/todos` description to "List todos; optional `?isCompleted=true` or `false` and `?userId=<id>` filters". Optionally mention that `POST`/`PUT` accept an optional `userId`. |
| `README.cs.md` | Same change in Czech, required by `rules/code-style.md` (bilingual README): "Seznam úkolů; volitelné filtry `?isCompleted=true` nebo `false` a `?userId=<id>`". |
| `docs/multi-instance.md` | None. Storage format gains an optional property but multi-instance behavior is unchanged. |

## Implementation order

1. Data: add `UserId` to `TodoItem` (property and `Clone()`).
2. DTO: add `UserId` to `TodoItemDto`, `CreateTodoItemRequest`, `UpdateTodoItemRequest` with the `[StringLength(100, ...)]` attribute on the requests.
3. Data: change `ITodoRepository.GetAllAsync` signature, then implement the identical filter in `InMemoryTodoRepository` and `JsonFileTodoRepository`.
4. API: add `NormalizeUserId` to `TodoMapper` and map `UserId` in `ToDto`, `ToEntity`, `ApplyTo`.
5. API: update `TodosController.GetAll` (parameter, validation attribute, normalization, XML docs, `[ProducesResponseType]` 400).
6. Tests: compile fix in `PerformanceProfilingTests`, then add mapping, DTO, controller, repository (both) and integration tests.
7. Commit in small focused commits, for example `feat(#54): Add userId to todos and filter GET /api/todos by user` and `test(#54): Cover userId tagging and user filtering`. Documentation (`docs/OpenAPI.md`, `README.md`, `README.cs.md`) is handled in stage 5, for example `docs(#54): Document userId filter in OpenAPI.md and README`.
8. Keep `.cs` files UTF-8 with BOM and CRLF; new `.cs` files must match (`dotnet format whitespace` on new files only); `git diff --check` must pass.

## Risks

- Changing the `GetAllAsync` parameter order (inserting `userId` before `cancellationToken`) breaks any positional `(bool?, CancellationToken)` caller. Only `TodosController.GetAll` does this today (verified by grep); the implementer must rebuild to confirm.
- Adding `userId` to the controller action breaks the positional call in `PerformanceProfilingTests`; the compile fix in A9 is required, otherwise the build fails.
- If `Clone()` is not updated, the in-memory repository silently drops `UserId` (it clones on add and read), and the JSON repository rollback loses it. Covered by `GetByIdAsync_ReturnsStoredUserId` and `UserId_PersistsAcrossRepositoryInstances`.
- Marking `TodoItem.UserId` as `required` would make legacy JSON files fail to deserialize and be moved to `.corrupt-*.bak`, i.e. data loss. It must stay a plain nullable property. Covered by `LegacyJsonWithoutUserId_LoadsWithNullUserId`.
- PUT full-update semantics mean an older client that does not know `userId` clears it on update. This is consistent with the existing `description` behavior and documented (A4).
- `userId` is not authentication; any client can read or set any user's todos. Documented as a limitation; no security change is in scope.
- No new NuGet packages are needed.

## Out of scope

- `POST /api/todos/repeat`, `CreateRecurringTodoRequest` and the `Repeat()` construction pattern (A7).
- Authentication, authorization or validation that a `userId` exists.
- Filtering for "unassigned" todos, case-insensitive matching, or a dedicated `/api/users/{id}/todos` route.
- Changes to `GET /api/todos/{id}`, `POST /api/todos/{id}/complete`, `DELETE /api/todos/{id}` and their concurrency behavior; no new `version` parameters.
- Seed data in both repositories (stays without `userId`).
- `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`, `Program.cs`, DI registration, `InfoController`, `.github/workflows/`, `Dockerfile`, `docker-compose.yml`, `.editorconfig`.
- Fixing the pre-existing omission of `version` in the recurring response examples in `docs/OpenAPI.md`.
- Renaming or refactoring existing types, routes, tests or files.
