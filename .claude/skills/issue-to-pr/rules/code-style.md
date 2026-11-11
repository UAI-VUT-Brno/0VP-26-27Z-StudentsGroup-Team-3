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
- Code, comments and documentation in `docs/` are in English.
- User documentation is bilingual: `README.md` in English and `README.cs.md` in Czech. When one of them changes, update the other with the same content in its language.
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
- Controller unit tests that need in-memory storage use `InMemoryTodoRepository` with `SeedSampleData = false`, so they start with known data.
- Repository-specific tests exercise the corresponding implementation. JSON repository tests use `JsonFileTodoRepository` with isolated temporary storage: a unique folder under `Path.GetTempPath()`, deleted in `Dispose()`, as in `JsonFileTodoRepositoryTests.cs`. They never modify developer data.
- Tests that do not need storage, such as mapping or DTO validation tests, do not create a repository.
- Integration tests use `WebApplicationFactory<Program>` and replace the repository or storage path, so they never touch `data/todos.json` or other developer data.
- Every new behavior needs at least one success test and one failure test (validation, not found, or stale version, as applicable).
- Never delete, skip or weaken an existing test to make the suite pass. If an existing test fails, fix the code, unless the issue explicitly changes the tested behavior.

## Verification commands

Local verification, used by the agents:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
dotnet test UAI.ZeroVP.sln --configuration Release
git diff --check
```

CI (`.github/workflows/ci.yml`) runs a stricter sequence. It is listed here for reference, so local results can be compared with CI:

```bash
dotnet restore UAI.ZeroVP.sln -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low -warnaserror:NU1900,NU1901,NU1902,NU1903,NU1904,NU1905
dotnet build UAI.ZeroVP.sln --no-restore --configuration Release
dotnet test UAI.ZeroVP.sln --no-build --configuration Release --verbosity normal
```

A change that passes locally can still fail in CI on a vulnerable NuGet package, which is one more reason not to add packages unless the issue requires it.
