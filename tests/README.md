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

From the repository root:

```powershell
dotnet test UAI.ZeroVP.sln