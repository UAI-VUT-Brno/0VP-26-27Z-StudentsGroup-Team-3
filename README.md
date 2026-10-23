# UAI.ZeroVP – ToDo REST API

**Language:** **English** | [Čeština](README.cs.md)

A sample REST API for managing ToDo items, developed as a .NET 10 teaching project. The solution demonstrates layered architecture, HTTP endpoints, automated testing, persistent JSON storage, concurrency control, and running multiple API instances on one host.

## Repository structure

```text
UAI.ZeroVP.sln                  Solution file
src/
├── UAI.ZeroVP.DTO/            API contracts and validation
├── UAI.ZeroVP.Data/           Entities and repository implementations
└── UAI.ZeroVP.API/            ASP.NET Core API, controllers and mapping
tests/                          xUnit tests
docs/                           Technical documentation
scripts/                        Local integration-test scripts
```

The solution has three main projects:

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| `UAI.ZeroVP.DTO` | Request/response DTOs, validation, priority enum | None |
| `UAI.ZeroVP.Data` | `TodoItem`, `ITodoRepository`, in-memory and JSON repositories | DTO |
| `UAI.ZeroVP.API` | HTTP endpoints, mapping, dependency injection and middleware | DTO, Data |

Controllers depend on the `ITodoRepository` abstraction rather than a concrete storage implementation.

## Requirements and local setup

- .NET 10 SDK
- PowerShell for the Windows examples and the optional two-process integration test
- A writable directory for persistent JSON data

Build and run from the repository root:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
dotnet run --project src/UAI.ZeroVP.API
```

The project's HTTP launch profile uses **http://localhost:5258**. Other launch configurations may use different URLs. The root route `/` redirects to Swagger UI when enabled, or to `/api/todos` otherwise.

## Data storage

The API has two `ITodoRepository` implementations:

| Implementation | Used when | Persistence | Sharing between processes |
| --- | --- | --- | --- |
| `JsonFileTodoRepository` | Normal application environments | JSON file | Yes, with the same file path |
| `InMemoryTodoRepository` | `LoadTest` environment | Process memory only | No |

### Persistent JSON storage

Normal application runs use `JsonFileTodoRepository` by default. The `TodoStorage` configuration section specifies the file path and whether sample items are seeded. The current configuration uses `data/todos.json` and enables sample data when a new store is initialized.

Override the configuration using environment variables, for example in PowerShell:

```powershell
$env:TodoStorage__FilePath = "C:\Temp\ZeroVP\todos.json"
$env:TodoStorage__SeedSampleData = "false"
```

All API processes that share a store must use the **same absolute file path**. A relative path depends on the process working directory and should not be relied on for multi-instance operation.

The JSON repository uses a cross-process lock file, reloads the current content before operations, and writes updates through a temporary file followed by file replacement. Invalid JSON detected during startup is backed up; corruption encountered during normal operation does not silently overwrite the file.

For Docker deployment, keeping data after container replacement requires a correctly configured volume or bind mount. Multiple containers must have suitable access to the same storage and locking semantics.

### In-memory storage

The `LoadTest` environment uses `InMemoryTodoRepository` for performance-oriented HTTP tests. Its data is lost when the process terminates. **Do not use `LoadTest` to demonstrate shared JSON storage.**

## API endpoints

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/todos` | List todos; optional `?isCompleted=true` or `false` |
| `GET` | `/api/todos/{id}` | Get a todo by ID |
| `POST` | `/api/todos` | Create a todo (`201 Created`, `Location` header) |
| `POST` | `/api/todos/repeat` | Create a weekly series of independent todos |
| `PUT` | `/api/todos/{id}` | Fully update a todo, including its expected version |
| `POST` | `/api/todos/{id}/complete` | Mark a todo as completed |
| `DELETE` | `/api/todos/{id}` | Delete a todo (`204 No Content`) |
| `GET` | `/api/info` | Instance details, including unique `instanceId` |
| `GET` | `/health` | Health-check endpoint |
| `GET` | `/swagger` | Interactive Swagger UI, when enabled |
| `GET` | `/openapi/v1.json` | Machine-readable OpenAPI description, when enabled |

Failed requests return standard ASP.NET Core `ProblemDetails` responses.

`POST /api/todos/repeat` accepts a title, optional description, priority, UTC `firstDueAtUtc`, and `repeatCount` (1–52 additional weekly occurrences). Each occurrence has its own ID.

### Optimistic concurrency

Every todo has a `version` value starting at `1`. Successful updates increment it. A `PUT` request must include the version read by the client:

```json
{
  "title": "Updated task",
  "description": "Example update",
  "priority": "Normal",
  "isCompleted": false,
  "version": 1
}
```

If another operation has already updated the todo, the stale request receives **HTTP `409 Conflict`**, and the newer stored data is preserved. Clients should reload the current todo and resolve the conflict before retrying.

## Running two API instances

Build in Release mode first, then open **two separate PowerShell terminals** at the repository root. Both must reference exactly the same absolute JSON file.

**Terminal A:**

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5258
```

**Terminal B:**

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5259
```

Check their different instance IDs in a third terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5258/api/info
Invoke-RestMethod http://127.0.0.1:5259/api/info
```

Create a task through A and retrieve it immediately through B:

```powershell
$task = Invoke-RestMethod -Uri http://127.0.0.1:5258/api/todos `
    -Method Post -ContentType "application/json" `
    -Body '{"title":"Shared storage test","priority":"Normal"}'
Invoke-RestMethod "http://127.0.0.1:5259/api/todos/$($task.id)"
```

Stop each API instance with `Ctrl+C`. See [the full multi-instance guide](docs/multi-instance.md) for details.

## Testing

Run the entire .NET test suite from the repository root:

```powershell
dotnet test UAI.ZeroVP.sln --configuration Release
```

The last verified local run passed **84/84 xUnit tests**. Relevant suites include:

- `JsonFileTodoRepositoryTests` — JSON persistence and initialization.
- `JsonFileMultiInstanceTests` — two repository objects sharing a file, concurrent writes, version conflicts and file integrity.
- `TodoConcurrencyTests` — HTTP version increments, stale updates and `409 Conflict`.
- `ApiLoadTests` and `HttpPerformanceTests` — HTTP load and response-time measurements.

The six JSON multi-instance tests use separate repository objects **within one OS process**. To test two actual API processes and shared storage, run the standalone PowerShell integration test:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\test-multi-instance.ps1"
```

This script starts two APIs, checks distinct instance IDs, shared-data visibility, stale-update protection, 40 concurrent writes and JSON validity, then stops the processes. It uses temporary test data. The latest execution completed with `ALL MULTI-PROCESS TESTS PASSED`.

The PowerShell test is **not** included in `dotnet test` or automatically executed by CI. Run it separately; `ExecutionPolicy Bypass` applies only to the child PowerShell process in the shown command. Ensure the script comes from a trusted source before running it.

## Swagger and OpenAPI

The API exposes its OpenAPI document through .NET 10 and Swagger UI when enabled. Swagger is enabled by default in `Development` and can be enabled explicitly with:

```powershell
$env:Swagger__Enabled = "true"
```

The project also includes sample HTTP requests in [`src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`](src/UAI.ZeroVP.API/UAI.ZeroVP.API.http).

## Docker learning materials

The API is intended as a foundation for exercises on Dockerfiles, container tooling and configuration. Some original course exercises assume process-local storage; with the current JSON implementation, their restart and replication scenarios may behave differently unless configured accordingly.

Build Docker images from the repository root when using the relevant teaching Dockerfiles. The `case-study-a-dockerfile`, `case-study-b-tooling` and `case-study-c-configuration` directories referenced by earlier README versions were **not present in the supplied project snapshot**; consult the course materials if you need those examples.

## Further documentation

- [Multi-instance JSON storage](docs/multi-instance.md)
- [API performance](docs/api-performance.md)
- [HTTP load testing](docs/api-load-testing.md)
- [OpenAPI documentation](docs/OpenAPI.md)
- [Security](docs/security.md)

C# source files use UTF-8 with BOM according to `.editorconfig`; Markdown files use UTF-8 with LF line endings.
