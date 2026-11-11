# Architecture rules

These rules describe the existing structure of the solution. Every change must fit into it. When a rule and an issue conflict, follow the issue only if it explicitly asks for the architectural change.

## Projects and dependencies

| Project | Responsibility | May reference |
|---|---|---|
| `src/UAI.ZeroVP.DTO` | Request and response contracts, DataAnnotations validation, `TodoPriority` enum | nothing |
| `src/UAI.ZeroVP.Data` | `TodoItem` entity, `ITodoRepository`, in-memory and JSON repositories, DI registration | DTO |
| `src/UAI.ZeroVP.API` | Controllers, mapping, MCP tools, `Program.cs` configuration | DTO, Data |
| `tests/UAI.ZeroVP.Tests` | xUnit unit and integration tests | all projects |

- Never add a reference that points upwards, for example from `Data` to `API` or from `DTO` to `Data`.
- Target framework is `net10.0` with `Nullable` and `ImplicitUsings` enabled in every project. Do not change these settings.

## Where code belongs

| Change | Location | Existing example |
|---|---|---|
| New or changed request/response shape | `src/UAI.ZeroVP.DTO/` | `CreateTodoItemRequest.cs`, `TodoItemDto.cs` |
| Stored fields | `src/UAI.ZeroVP.Data/Entities/TodoItem.cs` | `Version`, `UpdatedAtUtc` |
| Storage operations | `ITodoRepository` and **both** implementations | `InMemory/InMemoryTodoRepository.cs`, `Json/JsonFileTodoRepository.cs` |
| Repository registration | `src/UAI.ZeroVP.Data/DependencyInjection/DataServiceCollectionExtensions.cs` | `AddJsonFileTodoData` |
| Entity ↔ DTO conversion | `src/UAI.ZeroVP.API/Mapping/TodoMapper.cs` | `ToDto`, `ToEntity`, `ApplyTo` |
| HTTP endpoints | `src/UAI.ZeroVP.API/Controllers/` | `TodosController.cs` |
| MCP tools | `src/UAI.ZeroVP.API/Mcp/` | `TodoTools.cs` |
| Service and middleware configuration | `src/UAI.ZeroVP.API/Program.cs` | `AddProblemDetails`, `MapMcp` |

## Layer rules

- Controllers and MCP tools depend only on `ITodoRepository`, never on a concrete repository class.
- A change to `ITodoRepository` must be implemented in both `InMemoryTodoRepository` and `JsonFileTodoRepository`, with the same behavior, and covered by tests for both.
- Mapping between `TodoItem` and DTOs happens only in `TodoMapper`. Controllers do not copy fields by hand.
- The entity is never returned from the API. Internal fields such as `UpdatedAtUtc` must not appear in DTOs.
- Use the injected `TimeProvider` for the current time. Never call `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly.
- Pass `CancellationToken` through every asynchronous call down to the repository.
- MCP tools are read-only. Do not add tools that modify data unless the issue explicitly asks for it.

## Out of scope unless the issue asks for it

- New NuGet packages. CI audits all dependencies (`.github/workflows/ci.yml`).
- Changes to `.github/workflows/`, `Dockerfile`, `docker-compose.yml` and `.editorconfig`.
- Renaming or moving existing public types, routes or files.
