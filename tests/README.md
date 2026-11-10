# Backend Unit Tests

Unit tests are implemented using xUnit in the `tests/UAI.ZeroVP.Tests` project.

## Repository tests

Location:

`tests/UAI.ZeroVP.Tests/Data/InMemoryTodoRepositoryTests.cs`

The tests verify the behavior of `InMemoryTodoRepository`, including:

- adding todo items,
- retrieving existing items,
- handling unknown IDs,
- updating existing items,
- handling updates of unknown items,
- deleting items,
- filtering by completion status,
- sorting by priority and creation time.

Each test uses a fresh in-memory repository with sample data disabled, so the tests are isolated from each other.

## Mapper tests

Location:

`tests/UAI.ZeroVP.Tests/Mapping/TodoMapperTests.cs`

The tests verify:

- creation of entities from API requests,
- trimming of text values,
- conversion of empty descriptions to `null`,
- initialization of timestamps,
- completion and reopening of todo items,
- updating entities using `UpdateTodoItemRequest`.

Fixed timestamps are used where appropriate to make the expected results deterministic.

## Controller tests

Location:

`tests/UAI.ZeroVP.Tests/Controllers/TodosControllerTests.cs`

The tests verify selected behavior of `TodosController`, including:

- returning an empty todo list,
- creating a todo item,
- returning `404 Not Found` for unknown items,
- deleting an existing todo item,
- deleting an unknown item,
- completing an existing todo item,
- completing an unknown item.

The controller tests use the real `InMemoryTodoRepository` with sample data disabled and `NullLogger`, keeping the tests lightweight without requiring a running web server.

## Running the tests
## MCP integration tests

Location:

`tests/UAI.ZeroVP.Tests/Integration/Mcp/`

The tests verify real MCP communication over Streamable HTTP using the official `ModelContextProtocol` client SDK:

- discovery of exactly `list_todos` and `get_todo`, their input schemas and required parameters, and the absence of write tools,
- `list_todos` results for all filter combinations, compared by count and exact content independently of ordering,
- `get_todo` returning all public fields, `TODO_NOT_FOUND` for a nonexistent ID, and rejection of missing or malformed IDs,
- rejection of invalid argument types and empty `userId` values without exposing application internals,
- unchanged storage contents and versions after tool calls,
- `/mcp` returning `404` on a running API when MCP is disabled in `Development` or enabled outside `Development`.

Each test class starts the API on a free loopback port (`127.0.0.1`) with a startup timeout. The server uses an isolated temporary JSON file seeded with 100 deterministic todos (40 for `user-a`, 40 for `user-b`, 20 without a user, each group split evenly into completed and incomplete items). The server is stopped and temporary data are deleted even when tests fail. No manually started API, MCP Inspector or AI client is required.
From the repository root:

```powershell
dotnet test UAI.ZeroVP.sln