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

Extended MCP client integration tests are covered by T3-15. Inspector and AI-client verification are documented in [Manual verification with MCP Inspector and Claude Code](#manual-verification-with-mcp-inspector-and-claude-code).

## Manual verification with MCP Inspector and Claude Code

Automated protocol coverage is provided by the MCP integration tests (T3-15). This section records a manual, reproducible verification of client setup and practical usability with MCP Inspector and one AI client.

### Summary

| Item | Value |
|---|---|
| Verification date | 2026-11-10 |
| Application commit | `f4fe3df` (`main` after PR #39) |
| Result | All Inspector and AI-client scenarios passed; storage unchanged |

### Tools and versions

| Tool | Variant | Version |
|---|---|---|
| OS and shell | Windows, Windows PowerShell | – |
| .NET SDK | – | 10.0.401 |
| Node.js | – | v26.13.0 |
| MCP Inspector | `@modelcontextprotocol/inspector`, web UI started with `npx` | 2.14.0 |
| AI client | Claude Code (CLI), model Opus 5.5, Claude Pro plan, permission mode `auto` | 2.1.322 |

### Local startup

Run from the repository root:

```powershell
$storageDir = Join-Path $env:TEMP "zerovp-t3-16"
New-Item -ItemType Directory -Force $storageDir | Out-Null

dotnet run --project src/UAI.ZeroVP.API --no-launch-profile -- --environment Development --urls http://127.0.0.1:5258 --Mcp:Enabled=true --TodoStorage:FilePath="$storageDir\todos.json" --TodoStorage:SeedSampleData=false
```

- The API is bound to the loopback address only.
- The isolated storage file is outside the repository. It is created on the first write; the lock file `todos.json.lock` is created at startup.
- The logging configuration does not print `Now listening on`. Check availability from a second terminal:

  ```powershell
  Invoke-RestMethod http://127.0.0.1:5258/health
  ```

  The expected response is `Healthy`.
- If startup fails with `address already in use`, stop the previous instance using port 5258 first.

Keep this terminal open during the whole verification.

### Test data preparation

Six todos were created and completed using only the existing REST API (`POST /api/todos` and `POST /api/todos/{id}/complete`):

```powershell
$api = "http://127.0.0.1:5258/api/todos"

$seed = @(
    @{ Key = "A1"; Title = "T3-16 user-a completed 1";  UserId = "user-a"; Priority = "High";   Complete = $true  }
    @{ Key = "A2"; Title = "T3-16 user-a completed 2";  UserId = "user-a"; Priority = "Normal"; Complete = $true  }
    @{ Key = "A3"; Title = "T3-16 user-a incomplete 1"; UserId = "user-a"; Priority = "Low";    Complete = $false }
    @{ Key = "A4"; Title = "T3-16 user-a incomplete 2"; UserId = "user-a"; Priority = "High";   Complete = $false }
    @{ Key = "B1"; Title = "T3-16 user-b incomplete 1"; UserId = "user-b"; Priority = "Normal"; Complete = $false }
    @{ Key = "N1"; Title = "T3-16 no-user completed 1"; UserId = $null;    Priority = "Low";    Complete = $true  }
)

$created = foreach ($t in $seed) {
    $body = @{ title = $t.Title; description = "T3-16 MCP verification"; priority = $t.Priority }
    if ($t.UserId) { $body.userId = $t.UserId }

    $todo = Invoke-RestMethod -Method Post -Uri $api -ContentType "application/json" -Body ($body | ConvertTo-Json)

    if ($t.Complete) {
        $todo = Invoke-RestMethod -Method Post -Uri "$api/$($todo.id)/complete"
    }

    [pscustomobject]@{ Key = $t.Key; Id = $todo.id; UserId = $todo.userId; IsCompleted = $todo.isCompleted; Version = $todo.version; Title = $todo.title }
}

$created | Format-Table -AutoSize
```

IDs are generated by the API, so they differ in every run. The IDs from this verification:

| Key | ID | User | Completed | Priority | Version |
|---|---|---|---|---|---|
| A1 | `19675897-977d-4777-8e16-4cdbe499fa79` | `user-a` | yes | High | 2 |
| A2 | `bace383b-d6b5-4a6d-887a-7f347a7cc0a4` | `user-a` | yes | Normal | 2 |
| A3 | `07576a52-61a0-4934-842b-8ac083cd0164` | `user-a` | no | Low | 1 |
| A4 | `4e6f7f59-6206-46a8-aaa5-33791db05633` | `user-a` | no | High | 1 |
| B1 | `fcc2be22-2d0e-43a7-bae5-4d6dd2d1664f` | `user-b` | no | Normal | 1 |
| N1 | `d5df5ff1-e206-42aa-acb6-98f16f19376d` | – | yes | Low | 2 |

Completing a todo is an update, so completed todos have version 2.

Expected results:

| Query | Expected todos |
|---|---|
| No filters | 6 |
| Completed | 3 (A1, A2, N1) |
| Incomplete | 3 (A3, A4, B1) |
| `user-a` | 4 (A1–A4) |
| `user-a` and incomplete | 2 (A3, A4) |
| Unknown user | 0 (successful empty list) |

Before client verification, the storage was confirmed to contain exactly six todos and its hash was recorded:

```powershell
$storage = "$env:TEMP\zerovp-t3-16\todos.json"

(Invoke-RestMethod $api).Count                         # 6
(Get-Content $storage -Raw | ConvertFrom-Json).Count  # 6
Get-FileHash $storage -Algorithm SHA256 | Select-Object Hash
```

No data were changed through REST after this point.

### MCP Inspector

Start Inspector from a second terminal:

```powershell
npx.cmd @modelcontextprotocol/inspector@2.14.0
```

On Windows PowerShell with script execution disabled, use `npm.cmd` and `npx.cmd` instead of `npm` and `npx`.

Connection:

1. Inspector 2.x starts with public example servers preconfigured. Disable or remove them; this verification uses only the local server.
2. Use **Add Servers** with name `zerovp-local`, transport **Streamable HTTP**, URL `http://127.0.0.1:5258/mcp` and no authentication.
3. Enable the server and wait for **Connected**. Inspector completes protocol initialization automatically.
4. Open **Tools** and enter tool arguments with **Edit as JSON**.

| # | Scenario | Tool and arguments | Expected | Actual | Status |
|---|---|---|---|---|---|
| I1 | Discovery and schemas | `tools/list` | Exactly `list_todos` and `get_todo`, both read-only; `id` required; `isCompleted` and `userId` optional | As expected | Pass |
| I2 | No filters | `list_todos` `{}` | 6 todos | 6 todos (A1–A4, B1, N1) | Pass |
| I3 | Completed | `list_todos` `{"isCompleted": true}` | 3 todos | A1, A2, N1 | Pass |
| I4 | Incomplete | `list_todos` `{"isCompleted": false}` | 3 todos | A3, A4, B1 | Pass |
| I5 | User filter | `list_todos` `{"userId": "user-a"}` | 4 todos | A1–A4 | Pass |
| I6 | Combined filter | `list_todos` `{"userId": "user-a", "isCompleted": false}` | 2 todos | A3, A4 | Pass |
| I7 | Unknown user | `list_todos` `{"userId": "user-unknown"}` | Successful result `{"todos": []}` | `{"todos": []}`, not an error | Pass |
| I8 | Existing ID | `get_todo` `{"id": "07576a52-61a0-4934-842b-8ac083cd0164"}` | A3 with all public fields | A3 with `id`, `title`, `description`, `userId`, `priority`, `isCompleted`, `createdAtUtc`, `dueAtUtc`, `completedAtUtc`, `version` | Pass |
| I9 | Nonexistent ID | `get_todo` `{"id": "00000000-0000-0000-0000-000000000001"}` | Tool error containing `TODO_NOT_FOUND` | `isError: true`, `An error occurred invoking 'get_todo': TODO_NOT_FOUND: No todo exists with the supplied ID.` | Pass |

All successful results contained the expected structured output (`todos` or `todo`). The order of listed todos is by priority and creation time; ordering is not part of the contract.

Inspector schema portability check:

| Tool | Errors | Warnings | Fields |
|---|---|---|---|
| `get_todo` | 0 | 4 | Output: `description`, `userId`, `dueAtUtc`, `completedAtUtc` |
| `list_todos` | 0 | 6 | Input: `isCompleted`, `userId`; output: the same four fields |

All warnings concern nullable fields described as `"type": [X, "null"]`. This is valid JSON Schema generated by the SDK, but some clients using a single-type schema dialect may handle it differently. The schemas were not changed because MCP contract changes are out of scope.

### Claude Code

Claude Code was run from an empty folder outside the repository, so it could answer only through MCP tools and not by reading source code or storage files. The server was registered in the default local scope, so no configuration is written to the repository.

```powershell
npm.cmd install -g --allow-scripts=@anthropic-ai/claude-code @anthropic-ai/claude-code

$clientDir = Join-Path $env:TEMP "zerovp-t3-16-client"
New-Item -ItemType Directory -Force $clientDir | Out-Null
cd $clientDir

claude.cmd mcp add --transport http zerovp-local http://127.0.0.1:5258/mcp
claude.cmd mcp list
claude.cmd
```

`claude.cmd mcp list` must report `zerovp-local` as connected. In Claude Code, `/mcp` showed exactly `list_todos` and `get_todo`, both read-only.

Each scenario was run after `/clear`, so the answer could not reuse earlier tool results. Tool calls were expanded with **Ctrl+O** to verify tool names and arguments.

| # | Prompt | Tool call and arguments | Expected | Actual answer | Status |
|---|---|---|---|---|---|
| C1 | `Using the zerovp-local MCP server, list all todos.` | `list_todos` without arguments | 6 todos | 6 todos; 3 completed, 3 incomplete; 4 for `user-a`, 1 for `user-b`, 1 without user | Pass |
| C2 | `Using the zerovp-local MCP server, which incomplete todos belong to user-a?` | `list_todos` `userId: "user-a"`, `isCompleted: false` | A3, A4 | A4 and A3 | Pass |
| C3 | `Using the zerovp-local MCP server, show details of todo fcc2be22-2d0e-43a7-bae5-4d6dd2d1664f.` | `get_todo` `id: "fcc2be22-2d0e-43a7-bae5-4d6dd2d1664f"` | B1 details | B1: `user-b`, Normal, not completed, version 1 | Pass |
| C4 | `Using the zerovp-local MCP server, which todos belong to user user-unknown?` | `list_todos` `userId: "user-unknown"` | Empty list, not an error | "No todos belong to user-unknown"; tool result `{"todos": []}` | Pass |
| C5 | `Using the zerovp-local MCP server, show details of todo 00000000-0000-0000-0000-000000000001.` | `get_todo` `id: "00000000-0000-0000-0000-000000000001"` | Error, no invented todo | Reported `TODO_NOT_FOUND`; no todo invented | Pass |

The client distinguished an empty result (C4) from a tool error (C5) and did not invent a missing todo.

### Data preservation

| Point | SHA-256 of `todos.json` | Todos in file |
|---|---|---|
| After data preparation, before verification | `9DD97818ECF82220249ED424C9D127757696F41ACAFB365813D42260173AE2AD` | 6 |
| After all Inspector and Claude Code scenarios | `9DD97818ECF82220249ED424C9D127757696F41ACAFB365813D42260173AE2AD` | 6 |

The storage file remained unchanged during the whole MCP verification.

### Evidence

Evidence is stored in [`docs/evidence/t3-16/`](evidence/t3-16/):

| File | Content |
|---|---|
| [inspector-i1-tools.png](evidence/t3-16/inspector-i1-tools.png) | Inspector tool discovery, read-only tools, schema portability summary |
| [inspector-i2-list-all.json](evidence/t3-16/inspector-i2-list-all.json) | Structured output of `list_todos` without filters |
| [inspector-i6-protocol.png](evidence/t3-16/inspector-i6-protocol.png) | `tools/call` request with arguments and result for the combined filter |
| [inspector-i7-unknown-user.json](evidence/t3-16/inspector-i7-unknown-user.json) | Successful empty result for an unknown user |
| [inspector-i9-not-found.json](evidence/t3-16/inspector-i9-not-found.json) | JSON-RPC response with `isError: true` and `TODO_NOT_FOUND` |
| [claude-code-tools.png](evidence/t3-16/claude-code-tools.png) | Tools discovered by Claude Code |
| [claude-code-c1-call.png](evidence/t3-16/claude-code-c1-call.png) | C1 tool call without arguments |
| [claude-code-c1-list-all.png](evidence/t3-16/claude-code-c1-list-all.png) | C1 answer |
| [claude-code-c2-user-a-incomplete.png](evidence/t3-16/claude-code-c2-user-a-incomplete.png) | C2 tool call with arguments and answer |
| [claude-code-c3-get-existing.png](evidence/t3-16/claude-code-c3-get-existing.png) | C3 tool call with arguments and answer |
| [claude-code-c4-unknown-user.png](evidence/t3-16/claude-code-c4-unknown-user.png) | C4 tool call with arguments and answer |
| [claude-code-c5-not-found.png](evidence/t3-16/claude-code-c5-not-found.png) | C5 tool call with arguments and error handling |

The evidence contains only test data. No storage files, lock files, tokens or personal data are committed.

### Shutdown and cleanup

1. In Claude Code, run `/exit`. Remove the local registration from the client folder:

   ```powershell
   cd "$env:TEMP\zerovp-t3-16-client"
   claude.cmd mcp remove zerovp-local
   ```

2. Stop Inspector with Ctrl+C in its terminal.
3. Stop the API with Ctrl+C in its terminal.
4. Delete only the isolated test file and its lock file:

   ```powershell
   Remove-Item "$env:TEMP\zerovp-t3-16\todos.json", "$env:TEMP\zerovp-t3-16\todos.json.lock"
   ```

Do not delete `data/todos.json` or any other development data. The empty folders `%TEMP%\zerovp-t3-16` and `%TEMP%\zerovp-t3-16-client` can be removed afterwards.

### Limitations

- The MCP endpoint has no authentication or authorization. Any process that can reach the port can read all todos.
- Use it only in `Development`, bound to `127.0.0.1`. Do not bind it to `0.0.0.0`, publish the port, or expose it through a public tunnel.
- An AI client sends tool results to its model provider. Use only test data.
- Nullable schema fields produce portability warnings in Inspector; some clients may handle them differently.
- Results apply to the tool versions listed above.

### Findings

No implementation defects were found. Observations:

- Inspector schema portability warnings for nullable fields (see above).
- On Windows PowerShell with script execution disabled, `npm`, `npx` and `claude` must be run as `npm.cmd`, `npx.cmd` and `claude.cmd`.
- Recent npm versions block install scripts by default; Claude Code requires `--allow-scripts=@anthropic-ai/claude-code`.