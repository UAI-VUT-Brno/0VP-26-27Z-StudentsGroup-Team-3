# Domain rules

## Optimistic concurrency (`version`)

Optimistic concurrency protects updates of existing todos. Reference: `TodoItem.Version`, `UpdateAsync` in both repositories, `TodoConcurrencyException`, `TodoConcurrencyTests`.

Repository behavior:

- A new todo starts with `Version = 1`.
- `UpdateAsync` compares the supplied version with the stored one. On a mismatch it throws `TodoConcurrencyException` and changes no stored data. On success it increments the version by exactly 1, using `checked(existing.Version + 1)`.
- The check and the increment happen inside the repository, atomically with the write. Controllers never compare versions themselves.
- Every response DTO exposes the current `Version`.

Which operations use a client-supplied version:

| Operation | Client sends `version` | Concurrency behavior |
|---|---|---|
| `PUT /api/todos/{id}` | Yes, `UpdateTodoItemRequest.Version` with `[Range(1, long.MaxValue)]` | 409 on a stale version |
| `POST /api/todos/{id}/complete` | No | Uses the version read from storage; 409 only if the item changed in the meantime |
| `POST /api/todos`, `POST /api/todos/repeat` | No | Creates new items with `Version = 1` |
| `DELETE /api/todos/{id}` | No | No version check |

- Do not add a `version` parameter to existing operations that do not have one, and do not remove it from `PUT`. That would break the public API contract.
- A new operation that updates an existing todo must go through `UpdateAsync` and must have a test for the 409 case. Require a client-supplied version only if the issue asks for it.

## Error responses

All errors use ProblemDetails (RFC 9457), configured with `AddProblemDetails()` and `UseStatusCodePages()` in `Program.cs`.

| Situation | Status | How |
|---|---|---|
| DTO validation failed | 400 | Automatic `ValidationProblemDetails` from `[ApiController]` |
| Todo does not exist | 404 | `NotFoundTodo(id)` in `TodosController` |
| Stale version | 409 | Catch `TodoConcurrencyException`, return `VersionConflict(ex)` |

- Reuse the existing helpers in the controller. Do not return plain strings, anonymous objects or empty bodies for errors.
- Declare every possible response with `[ProducesResponseType]` and document it with `<response code="...">` in XML comments.
- MCP tools report errors with `McpException` and a stable code prefix (`TODO_NOT_FOUND:`, `INVALID_ARGUMENT:`), as in `TodoTools.cs`.

## Validation and normalization

- Validate input with DataAnnotations on DTOs (`[Required]`, `[StringLength]`, `[Range]`, `[EnumDataType]`) and use clear `ErrorMessage` texts. Existing limits: title 1–200, description 1000, user ID 100 characters.
- Do not mark DTO properties with the `required` keyword when they have a `[Required]` attribute; the reason is documented in `CreateTodoItemRequest.Title`.
- Trim text input in the mapper. Convert empty or whitespace-only optional text to `null`.
- Normalize user IDs only with `TodoMapper.NormalizeUserId`.
- Keep `IsCompleted` and `CompletedAtUtc` consistent; change them only through `SetCompletionState`.
- Multi-item writes must be atomic (`AddRangeAsync`): either all items are stored or none.

## Public contract changes

When an endpoint, DTO, MCP tool or configuration option changes, update the matching documentation in the same pull request:

| Change | Documentation |
|---|---|
| REST endpoints or DTOs | `docs/OpenAPI.md` |
| MCP tools | `docs/MCP.md` |
| Storage or multi-instance behavior | `docs/multi-instance.md` |
| Setup, configuration or usage | `README.md` and `README.cs.md` |
