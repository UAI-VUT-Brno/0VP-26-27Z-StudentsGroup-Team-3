# Code style and test rules

## Formatting

Follow `.editorconfig` in the repository root:

| Files | Encoding | Line endings | Indentation |
|---|---|---|---|
| `*.cs` | UTF-8 **with BOM** | CRLF | 4 spaces |
| `*.md` | UTF-8 | LF | 2 spaces |
| `*.json`, `*.yml`, `*.yaml`, `*.csproj` | UTF-8 | CRLF | 2 spaces |

- Every file ends with a newline. No trailing whitespace.
- Keep the encoding and line endings of existing files. Do not reformat lines you do not change.
- `git diff --check` must pass before every commit.

## C# conventions

Follow the style of the surrounding code, for example `TodosController.cs` and `TodoMapper.cs`:

- File-scoped namespaces matching the folder, for example `namespace UAI.ZeroVP.API.Mapping;`.
- Classes and records are `sealed` unless they are designed for inheritance.
- DTOs are `sealed record` types with `init` properties.
- Every public type and member has an XML documentation comment (`/// <summary>`).
- Asynchronous methods end with `Async` (repositories) and accept a `CancellationToken`.
- Private fields use the `_camelCase` prefix; dependencies are injected through the constructor.
- Long method calls and signatures are wrapped with one argument per line, as in the existing controllers.
- All code, comments and documentation are in English.
- The build must not produce new warnings.

## Tests

Framework: xUnit with `Assert`. All tests are in `tests/UAI.ZeroVP.Tests/`.

| Test type | Folder | Existing example |
|---|---|---|
| Controller unit tests | `Controllers/` | `TodosControllerTests.cs` |
| DTO validation | `DTO/` | `CreateRecurringTodoRequestTests.cs` |
| Repository tests | `Data/` | `InMemoryTodoRepositoryTests.cs`, `JsonFileTodoRepositoryTests.cs` |
| Mapping | `Mapping/` | `TodoMapperTests.cs` |
| HTTP and MCP integration | `Integration/` | `TodoConcurrencyTests.cs`, `Integration/Mcp/` |

- Name tests `Method_Scenario_ExpectedResult`, for example `GetById_UnknownId_ReturnsNotFound`.
- Structure unit tests with `// Arrange`, `// Act`, `// Assert` comments.
- Unit tests use `InMemoryTodoRepository` with `SeedSampleData = false`.
- Integration tests use `WebApplicationFactory<Program>` and replace the repository or storage path, so they never touch `data/todos.json` or other developer data.
- Every new behavior needs at least one success test and one failure test (validation, not found, or stale version, as applicable).
- Never delete, skip or weaken an existing test to make the suite pass. If an existing test fails, fix the code, unless the issue explicitly changes the tested behavior.

## Verification commands

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
dotnet test UAI.ZeroVP.sln --configuration Release
git diff --check
```

These are the same commands CI runs (`.github/workflows/ci.yml`).
