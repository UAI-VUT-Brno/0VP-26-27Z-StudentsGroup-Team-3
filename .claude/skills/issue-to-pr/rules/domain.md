# Domain rules

## Optimistic concurrency (`version`)

The API uses optimistic concurrency on every write to an existing todo. Reference: `TodoItem.Version`, `UpdateAsync` in both repositories, `TodoConcurrencyException`, `TodoConcurrencyTests`.

- A new todo starts with `Version = 1`.
- Every successful update increments the version by exactly 1, using `checked(existing.Version + 1)`.
- An update with a version different from the stored one fails with `TodoConcurrencyException` and must not change any stored data.
- The version check and the increment happen inside the repository, atomically with the write. Controllers never compare versions themselves.
- Endpoints that update a todo with a client-supplied version (`PUT /api/todos/{id}`) take it from the request body: `UpdateTodoItemRequest.Version`, validated with `[Range(1, long.MaxValue)]`.
- Every response DTO exposes the current `Version`.
- A new write operation must follow the same pattern and must be covered by a test with a stale version.

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
