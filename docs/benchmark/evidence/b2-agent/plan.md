# Plan: #55 [Benchmark] Change todo priority with PATCH /api/todos/{id}/priority

Status: ready
Task ID: #55 (the issue title has no `OVP-T3-<nn>` ID; per `rules/git-conventions.md` commits use scope `#55`, for example `feat(#55): Add PATCH /api/todos/{id}/priority`)
Branch: feat/t3-55-todo-priority-patch (base branch: `benchmark/base-current`, from `branch.md`)

## Goal

Add `PATCH /api/todos/{id}/priority` that changes only the priority of an existing todo, protected by client-supplied optimistic concurrency (`version`), returning 200/400/404/409 with ProblemDetails for errors.

## Requirements

- R1: Add the endpoint `PATCH /api/todos/{id}/priority` (route constraint `{id:guid}`, like the other ID routes in `TodosController`).
- R2: The JSON request body contains `priority` (`Low`, `Normal`, `High`) and `version` (the version the client last read).
- R3: The endpoint changes only `Priority` (plus the internal `UpdatedAtUtc` audit timestamp and the `Version` increment done by the repository); `Title`, `Description`, `UserId`, `IsCompleted`, `CompletedAtUtc`, `CreatedAtUtc`, `DueAtUtc` stay unchanged.
- R4: Optimistic concurrency like `PUT /api/todos/{id}`: the client version is passed to `ITodoRepository.UpdateAsync`; a stale version returns `409 Conflict` (ProblemDetails via `VersionConflict(ex)`) and changes nothing.
- R5: On success return `200 OK` with the updated `TodoItemDto`, including the new `version` (old + 1).
- R6: Unknown id returns `404 Not Found` as ProblemDetails (`NotFoundTodo(id)`).
- R7: Invalid priority or missing `version` returns `400 Bad Request` as `ValidationProblemDetails` (automatic `[ApiController]` model validation).
- R8: Automated tests cover the behavior.
- R9: The endpoint is documented in `docs/OpenAPI.md`.

## Acceptance criteria

- AC1: A valid request changes the priority, keeps all other fields and increments `version` (verified by: `UpdatePriority_ValidRequest_ReturnsUpdatedTodoWithIncrementedVersion`, `PatchPriority_ValidRequest_ChangesOnlyPriorityAndIncrementsVersion`, `ApplyTo_PriorityRequest_ChangesOnlyPriorityAndUpdatedAtUtc`)
- AC2: An unknown id returns `404 Not Found` as ProblemDetails (verified by: `UpdatePriority_UnknownId_ReturnsNotFound`, `PatchPriority_UnknownId_Returns404ProblemDetailsAndPreservesStorage`)
- AC3: A stale `version` returns `409 Conflict` as ProblemDetails and changes nothing (verified by: `UpdatePriority_StaleVersion_ReturnsConflictAndPreservesData`, `PatchPriority_StaleVersion_Returns409ProblemDetailsAndPreservesStorage`)
- AC4: An invalid priority or a missing `version` returns `400 Bad Request` as ValidationProblemDetails (verified by: `PatchPriority_InvalidBody_Returns400ValidationProblemAndPreservesStorage` theory, `Validate_InvalidPriority_FailsValidation`, `Validate_MissingVersion_FailsValidation`, `Validate_VersionBelowOne_FailsValidation`)
- AC5: The behavior is covered by automated tests (verified by: all tests in the **Tests** table pass with `dotnet test UAI.ZeroVP.sln --configuration Release`)
- AC6: The endpoint is documented in `docs/OpenAPI.md` (verified by: Manual check: `docs/OpenAPI.md` lists `PATCH /api/todos/{id}/priority – 200 OK / 400 Bad Request / 404 Not Found / 409 Conflict` under "TodosController" and contains a section "Changing Todo Priority" with the request body, validation table and concurrency behavior)

## Assumptions

- A1: The body is plain `application/json` with the two properties `priority` and `version`, not RFC 6902 JSON Patch (`application/json-patch+json`). The issue describes exactly this shape; JSON Patch would need a new NuGet package (`Microsoft.AspNetCore.JsonPatch`), which the rules forbid.
- A2: `priority` is **required** (missing or `null` → 400). The issue only explicitly names "invalid priority" and "missing version", but defaulting a missing priority to `Normal` (as `UpdateTodoItemRequest` does for a full update) would silently change data in an endpoint whose only purpose is to set the priority. This is a new endpoint, so requiring it breaks no existing client.
- A3: "Missing `version`" is detected reliably by declaring `Version` as `long?` with `[Required]` plus `[Range(1, long.MaxValue)]`, so a missing value produces a clear "Version is required." error, and `0`/negative values are rejected by `Range` (same lower bound as `UpdateTodoItemRequest.Version`). `PUT` keeps its existing `long` declaration unchanged.
- A4: Invalid priority covers both an unknown string (for example `"Urgent"`, rejected by `JsonStringEnumConverter` during model binding → automatic 400 `ValidationProblemDetails`, same mechanism as the existing `invalid-field-type` scenario in `ApiValidationTests`) and an undefined numeric value (for example `99`, which `JsonStringEnumConverter` accepts by default and `[EnumDataType]` then rejects).
- A5: Setting the same priority the todo already has is still a successful update (200, version incremented), consistent with `PUT`, which always increments on success. No special no-op handling.
- A6: Changing the priority sets `UpdatedAtUtc` to `_timeProvider.GetUtcNow()` (internal audit field, as `ApplyTo` does for `PUT`). It is not exposed in the DTO, so "all other fields stay unchanged" holds for the public contract.
- A7: No repository change is needed: `UpdateAsync` in both `InMemoryTodoRepository` and `JsonFileTodoRepository` already compares versions atomically, throws `TodoConcurrencyException`, increments with `checked(existing.Version + 1)` and writes the new version back to the passed entity (`item.Version = replacement.Version`), so the controller can return `entity.ToDto()` exactly like `Update` does.
- A8: The endpoint is REST only. MCP tools stay read-only (no new MCP tool), because the issue does not ask for one.
- A9: `README.md` and `README.cs.md` contain an "API endpoints" table listing every endpoint; the new endpoint row is added to both (usage documentation, `rules/domain.md` "Public contract changes"). No other README content changes.

## Affected files

| File | Layer | Change | Reason | Covers |
|---|---|---|---|---|
| `src/UAI.ZeroVP.DTO/UpdateTodoPriorityRequest.cs` | DTO | add | New `public sealed record UpdateTodoPriorityRequest` in `namespace UAI.ZeroVP.DTO;` with XML docs (summary: input for `PATCH /api/todos/{id}/priority`, changes only the priority). Properties: `[Required(ErrorMessage = "Priority is required.")] [EnumDataType(typeof(TodoPriority), ErrorMessage = "Invalid priority value.")] public TodoPriority? Priority { get; init; }` and `[Required(ErrorMessage = "Version is required.")] [Range(1, long.MaxValue, ErrorMessage = "Version must be at least 1.")] public long? Version { get; init; }` (doc: expected version, must match the stored one). No `required` keyword (see `CreateTodoItemRequest.Title`). Pattern: `UpdateTodoItemRequest.cs`. UTF-8 with BOM, CRLF. | R2, R7 |
| `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs` | API | modify | Add `public static void ApplyTo(this UpdateTodoPriorityRequest request, TodoItem entity, DateTimeOffset nowUtc)` with XML doc: sets `entity.Priority = request.Priority!.Value;` and `entity.UpdatedAtUtc = nowUtc;` and nothing else. Keeps mapping in the mapper (architecture rule); overload next to the existing `ApplyTo(UpdateTodoItemRequest, ...)`. Do not reformat existing lines. | R3 |
| `src/UAI.ZeroVP.API/Controllers/TodosController.cs` | API | modify | Add action `UpdatePriority(Guid id, [FromBody] UpdateTodoPriorityRequest request, CancellationToken cancellationToken)` with `[HttpPatch("{id:guid}/priority")]`, placed after `Update` (PUT). Return type `Task<ActionResult<TodoItemDto>>`. Attributes `[ProducesResponseType(typeof(TodoItemDto), 200)]`, `(typeof(ValidationProblemDetails), 400)`, `(typeof(ProblemDetails), 404)`, `(typeof(ProblemDetails), 409)`, wrapped one argument per line as in `Update`. XML doc `<summary>` (changes only the priority, optimistic concurrency) and `<response code="200|400|404|409">`. Body mirrors `Update`: `GetByIdAsync(id, cancellationToken)` → `NotFoundTodo(id)` if null; `request.ApplyTo(entity, _timeProvider.GetUtcNow())`; `entity.Version = request.Version!.Value;` with the same comment as in `Update`; `try { var updated = await _repository.UpdateAsync(entity, cancellationToken); return updated ? Ok(entity.ToDto()) : NotFoundTodo(id); } catch (TodoConcurrencyException ex) { return VersionConflict(ex); }`. Optionally log `"Changed priority of todo item {TodoId} to {Priority}."` with `_logger.LogInformation` after success. Controller never compares versions itself. | R1, R3, R4, R5, R6, R7 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | Tests | add | DataAnnotations validation tests with `Validator.TryValidateObject(..., validateAllProperties: true)`, pattern `CreateRecurringTodoRequestTests.cs`. No repository. | R7, R8 |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | Tests | modify | Add one test for the new `ApplyTo` overload. Do not change existing tests. | R3, R8 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | Tests | modify | Add controller unit tests for `UpdatePriority` using `InMemoryTodoRepository` with `SeedSampleData = false` (pattern `Complete_ExistingTodo_ReturnsCompletedTodo`). Do not change existing tests. | R4, R5, R6, R8 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | Tests | add | HTTP integration tests via `WebApplicationFactory<Program>` with isolated JSON storage, copying the setup of `ApiValidationTests.cs` (environment `Production`, `ConfigureTestServices` replacing `JsonFileTodoOptions` with a unique temp folder under `Path.GetTempPath()`, `SeedSampleData = false`, folder deleted in `Dispose()`). Use `HttpClient.PatchAsJsonAsync` / `HttpRequestMessage(HttpMethod.Patch, ...)` with `application/json`. Include private helpers like `CreateTodoAsync`, `ReadObjectAsync`, `AssertProblemAsync` (copied locally; do not modify `ApiValidationTests.cs`). Never touches `data/todos.json`. | R1–R7, R8 |

## Tests

| Test file | Test name | Type | Covers |
|---|---|---|---|
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | `Validate_ValidRequest_PassesValidation` (theory over `Low`, `Normal`, `High`, version `1`) | success | AC4 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | `Validate_MissingVersion_FailsValidation` (`Version = null`) | failure | AC4 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | `Validate_VersionBelowOne_FailsValidation` (theory `0`, `-1`) | failure | AC4 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | `Validate_InvalidPriority_FailsValidation` (`(TodoPriority)999`) | failure | AC4 |
| `tests/UAI.ZeroVP.Tests/DTO/UpdateTodoPriorityRequestTests.cs` | `Validate_MissingPriority_FailsValidation` (`Priority = null`) | failure | AC4 (A2) |
| `tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs` | `ApplyTo_PriorityRequest_ChangesOnlyPriorityAndUpdatedAtUtc` (entity with title, description, userId, completed state, due date; assert only `Priority` and `UpdatedAtUtc` change, `Version` untouched by mapper) | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `UpdatePriority_ValidRequest_ReturnsUpdatedTodoWithIncrementedVersion` (seed item via `repository.AddAsync`, version 1, priority `Low`; request `High`/1; assert `OkObjectResult`, DTO priority `High`, version 2, title/description/isCompleted unchanged; stored item has `High` and version 2) | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `UpdatePriority_UnknownId_ReturnsNotFound` (assert `ObjectResult` with status 404) | failure | AC2 |
| `tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs` | `UpdatePriority_StaleVersion_ReturnsConflictAndPreservesData` (first update with version 1 succeeds; second with version 1 and another priority → `ObjectResult` status 409; stored item keeps first priority and version 2) | failure | AC3 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | `PatchPriority_ValidRequest_ChangesOnlyPriorityAndIncrementsVersion` (create todo with title, description, userId, priority `Normal`; PATCH `{"priority":"High","version":1}` → 200; response `priority` = `High`, `version` = 2, all other properties equal to the created todo; `GET /api/todos/{id}` equals the PATCH response) | success | AC1 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | `PatchPriority_UnknownId_Returns404ProblemDetailsAndPreservesStorage` (random GUID; status 404 with ProblemDetails `status`/`title`; storage file content unchanged) | failure | AC2 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | `PatchPriority_StaleVersion_Returns409ProblemDetailsAndPreservesStorage` (first PATCH with version 1 → 200; capture storage; second PATCH with version 1 → 409 ProblemDetails; storage file content identical to captured) | failure | AC3 |
| `tests/UAI.ZeroVP.Tests/Integration/TodoPriorityApiTests.cs` | `PatchPriority_InvalidBody_Returns400ValidationProblemAndPreservesStorage` (theory over raw JSON bodies: `{"priority":"Urgent","version":1}`, `{"priority":99,"version":1}`, `{"priority":"High"}` (missing version), `{"priority":"High","version":0}`, `{"version":1}` (missing priority); assert 400, `errors` object non-empty, storage unchanged) | failure | AC4 |

## Public contract changes

- New endpoint `PATCH /api/todos/{id}/priority` (`{id:guid}`), request `application/json`, responses 200 `TodoItemDto`, 400 `ValidationProblemDetails`, 404 `ProblemDetails`, 409 `ProblemDetails`.
- New DTO `UAI.ZeroVP.DTO.UpdateTodoPriorityRequest { TodoPriority? Priority; long? Version }` (both required; `Version` >= 1).
- Existing endpoints, DTOs, MCP tools and configuration are unchanged (backward compatible).

## Documentation impact

| File | Change |
|---|---|
| `docs/OpenAPI.md` | In "TodosController (`/api/todos`)" add `- PATCH /api/todos/{id}/priority – 200 OK / 400 Bad Request (ValidationProblemDetails) / 404 Not Found / 409 Conflict (ProblemDetails)` after the `PUT` line. Add a section `## Changing Todo Priority` / `### PATCH /api/todos/{id}/priority` with: purpose (changes only the priority, other fields unchanged); request body example `{"priority": "High", "version": 3}`; validation table (`priority`: required, `Low`/`Normal`/`High`; `version`: required, integer >= 1, must match stored version); responses (200 updated todo with incremented `version`, 400, 404, 409 with data unchanged); note that the body is plain JSON, not JSON Patch. In "Optimistic Concurrency Control" mention that `PATCH /api/todos/{id}/priority` also requires `version` with the same rules. (R9) |
| `README.md` | Add one row to the "API endpoints" table after `PUT`: `` | `PATCH` | `/api/todos/{id}/priority` | Change only the priority, including its expected version | ``. Optionally one sentence in "Optimistic concurrency" that `PATCH /api/todos/{id}/priority` also requires `version`. |
| `README.cs.md` | Same change in Czech: row `` | `PATCH` | `/api/todos/{id}/priority` | Změna pouze priority včetně očekávané verze | `` and the matching sentence if added in `README.md`. |

## Implementation order

1. Add `src/UAI.ZeroVP.DTO/UpdateTodoPriorityRequest.cs`.
2. Add the `ApplyTo(UpdateTodoPriorityRequest, TodoItem, DateTimeOffset)` overload to `TodoMapper.cs`.
3. Add the `UpdatePriority` action to `TodosController.cs`.
4. Commit production code: `feat(#55): Add PATCH /api/todos/{id}/priority endpoint`.
5. Add `UpdateTodoPriorityRequestTests.cs`, the mapper test, the controller tests and `TodoPriorityApiTests.cs`.
6. Commit tests: `test(#55): Cover priority PATCH success, validation, 404 and 409`.
7. (Stage 5, release agent) Update `docs/OpenAPI.md`, `README.md`, `README.cs.md`: `docs(#55): Document PATCH /api/todos/{id}/priority`.
8. Verify: `dotnet build UAI.ZeroVP.sln --configuration Release`, `dotnet test UAI.ZeroVP.sln --configuration Release`, `git diff --check`.

## Risks

- `[EnumDataType]` on a nullable enum: `EnumDataTypeAttribute` returns valid for `null` and checks the boxed underlying enum value otherwise, so `Required` + `EnumDataType` work together; the DTO tests confirm it. If it misbehaved, the integration theory case `{"priority":99,...}` would fail.
- CS8629 nullable warnings: access `request.Priority!.Value` / `request.Version!.Value` with the null-forgiving operator (pattern `request.FirstDueAtUtc!.Value` in `Repeat`); the build must not produce new warnings.
- Route conflict: `PATCH` on `{id:guid}/priority` does not overlap any existing route; `ApiValidationTests.UnsupportedMethod_Returns405WithSupportedMethods` checks only `/api/todos` and is unaffected.
- New `.cs` files must be UTF-8 with BOM and CRLF (`.editorconfig`); `git diff --check` must pass.
- Integration tests must use isolated temp storage; using `LoadTest` (in-memory) is acceptable only for tests that do not compare the storage file. The plan uses JSON temp storage to verify "changes nothing" byte-for-byte.

## Out of scope

- Changes to `ITodoRepository`, `InMemoryTodoRepository`, `JsonFileTodoRepository` or `TodoItem` (existing `UpdateAsync` suffices).
- Changes to `PUT /api/todos/{id}`, `UpdateTodoItemRequest` (keep `long Version`), `POST /api/todos/{id}/complete` or other existing endpoints.
- Generic PATCH / JSON Patch support or patching other fields; new NuGet packages.
- New or changed MCP tools (`TodoTools.cs`) and `docs/MCP.md`.
- Refactoring `TodosController.Repeat()`, `Program.cs`, workflows, Docker files, `.editorconfig`, rule files under `.claude/`.
- Modifying existing tests (`ApiValidationTests.cs`, `TodoConcurrencyTests.cs`, etc.) beyond appending new tests to `TodoMapperTests.cs` and `TodosControllerTests.cs`.
