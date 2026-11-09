# Read-only ToDo MCP server

The application exposes two read-only tools through Streamable HTTP at `/mcp`, using the official `ModelContextProtocol.AspNetCore` SDK version 2.3.1.

Both tools use the existing `ITodoRepository` and public todo DTOs. They do not modify data. The REST API remains unchanged.

## Availability

MCP is disabled by default through `Mcp:Enabled=false`.

The endpoint is mapped only when both conditions hold:

- The environment is `Development`.
- `Mcp:Enabled` is `true`.

Otherwise, `/mcp` returns HTTP 404.

This classroom configuration has no authentication or authorization. Use only test data and a loopback address. It is not safe to expose publicly. The Development check alone does not restrict network access; keep the application bound to loopback.

## Local startup

Run from the repository root:

```powershell
& {
    dotnet run --project src/UAI.ZeroVP.API --no-launch-profile -- `
        --environment Development `
        --urls http://127.0.0.1:5258 `
        --Mcp:Enabled=true `
        --TodoStorage:FilePath=data/mcp-dev-todos.json `
        --TodoStorage:SeedSampleData=true
}
```

This uses a separate development JSON file. The relative storage path is resolved by the application's storage configuration. Do not commit generated test data.

Stop the application with Ctrl+C.

## Client connection

Configure an MCP client supporting Streamable HTTP:

- URL: `http://127.0.0.1:5258/mcp`
- Transport: Streamable HTTP
- Authentication: none

The server uses stateless transport. Let the client SDK handle protocol negotiation and HTTP headers.

## Tools

### `list_todos`

Optional arguments:

| Argument | Type | Behavior |
|---|---|---|
| `isCompleted` | boolean | Omit or use null to include both completion states. |
| `userId` | string | Omit or use null to include all users and unassigned todos. |

User IDs are trimmed and matched exactly, with case sensitivity, as in the REST API. Unlike the REST API, which treats an empty or whitespace-only `userId` as no filter, `list_todos` rejects such values with `INVALID_ARGUMENT`.

When both filters are supplied, todos must match both.

Example arguments:

```json
{
  "isCompleted": false,
  "userId": "user-a"
}
```

Successful structured output:

```json
{
  "todos": []
}
```

The array contains all matching public `TodoItemDto` objects. An empty array is a successful result. No pagination or silent truncation is applied.

### `get_todo`

Required argument:

| Argument | Type | Behavior |
|---|---|---|
| `id` | string | A todo identifier in GUID format. |

Example arguments:

```json
{
  "id": "bedc61b3-d8f5-4b69-80d3-e17ee16b7062"
}
```

Use an actual ID returned by `list_todos`.

Successful structured output contains a `todo` object matching the public `TodoItemDto`.

## Results and errors

Successful results are available in `structuredContent`. The SDK also provides a text representation.

Todo objects include ID, title, description, user ID, priority, completion status, timestamps, and version. Priorities are serialized as strings.

Tool errors use `isError=true` and a text message:

- `TODO_NOT_FOUND`: no todo exists with the supplied valid ID.
- `INVALID_ARGUMENT`: an invalid GUID or empty user filter was supplied.

The SDK may prepend context to error messages. Error identifiers are contained in the text; they are not separate structured error-code fields.

Missing required arguments and incompatible argument types are rejected by the SDK. Clients must handle protocol-level errors as well as tool errors.

## Basic verification

The following scenarios were manually checked during implementation:

- Discovery of exactly `list_todos` and `get_todo`.
- Listing without filters.
- Completion filtering.
- User filtering and combined filtering.
- Successful empty results.
- Retrieval of an existing todo.
- Nonexistent and malformed IDs.
- Rejection of whitespace-only user IDs.
- Preservation of all public todo fields and versions after tool calls.
- A healthy API with `/mcp` returning 404 in Production, even when enabled.
- A healthy API with `/mcp` returning 404 in Development when disabled.

Extended MCP client integration tests are covered by T3-15. Inspector and AI-client verification are covered by T3-16.
