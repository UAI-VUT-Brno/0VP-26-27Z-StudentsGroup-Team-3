
# Multi-Instance API and Shared JSON Storage

## Overview

The ToDo API supports running multiple independent instances on
localhost while sharing a single persistent JSON storage file.

This functionality is implemented as part of issue #22
(OVP-T3-09).

The implementation provides:

- Multiple API instances running on different localhost ports.
- Shared persistent JSON storage.
- Immediate visibility of changes between instances.
- Cross-process file locking to prevent lost updates.
- Atomic JSON file replacement.
- Unique identifiers for individual API instances.
- Optimistic concurrency control using todo versions.
- HTTP 409 Conflict responses for outdated updates.

## Requirements

- .NET 10 SDK
- PowerShell
- Two available localhost ports (5258 and 5259)
- A shared writable directory on the local filesystem

## Running Two API Instances

All commands are executed from the repository root.

### 1. Build the application

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
```

### 2. Start API instance A

Open the first PowerShell terminal.

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"

dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5258
```

### 3. Start API instance B

Open a second PowerShell terminal.

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"

dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5259
```

Both instances use the same JSON file, but run as independent
operating system processes.

Stop each instance using Ctrl+C.

## Verifying Multi-Instance Behavior

### Instance identification

```powershell
Invoke-RestMethod http://127.0.0.1:5258/api/info
Invoke-RestMethod http://127.0.0.1:5259/api/info
```

Both instances must return different `instanceId` values.

### Shared data

Create a todo through instance A:

```powershell
$task = Invoke-RestMethod `
    -Uri http://127.0.0.1:5258/api/todos `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"title":"Shared storage test","priority":"Normal"}'

$task
```

Read the same todo through instance B:

```powershell
Invoke-RestMethod "http://127.0.0.1:5259/api/todos/$($task.id)"
```

The created todo must be visible without restarting instance B.

## Concurrency and Data Consistency

### Cross-process synchronization

Both API instances use a shared lock file associated with
the JSON storage path.

Each storage operation acquires an exclusive file lock.
Write operations reload the latest data before applying
changes.

The updated JSON is first written to a temporary file
and then replaces the previous storage file.

This prevents concurrent processes from silently
overwriting each other's changes.

### Optimistic concurrency

Each todo contains a `version` property.

- New todos start at version 1.
- Every successful update increments the version.
- PUT requests must include the expected version.
- An outdated version produces HTTP 409 Conflict.
- Rejected updates do not overwrite newer data.

Clients should retrieve the latest todo before retrying
an update after a conflict.

### Corrupted JSON

Corrupted storage detected during initialization is backed
up before the repository initializes a clean state.

Corruption detected during normal operation raises an error
instead of silently overwriting the invalid file.

## Automated Tests

Run the complete test suite:

```powershell
dotnet test UAI.ZeroVP.sln --configuration Release
```

Relevant automated tests include:

- `JsonFileTodoRepositoryTests`
- `JsonFileMultiInstanceTests`
- `TodoConcurrencyTests`

These cover persistence, shared-file visibility,
concurrent writes, version conflicts, and data integrity.

The JSON repository tests use independent repository
instances in the same test process.

Separate API processes have also been verified manually
on localhost, including concurrent writes and shared
data visibility.

## Limitations

- Shared storage is intended for multiple instances
  running on the same host.
- All instances must use the same absolute JSON file path.
- The `LoadTest` environment uses in-memory storage and
  must not be used for shared JSON testing.
- This solution is intended for educational and small-scale
  deployment scenarios, not as a replacement for a
  transactional database.

## Multi-Process Integration Test

An automated multi-process integration test is available at
[`scripts/test-multi-instance.ps1`](../scripts/test-multi-instance.ps1).

Run from the repository root:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\test-multi-instance.ps1"
```

The test automatically verifies:

- Two independent API processes with unique instance IDs.
- Immediate visibility of changes between instances.
- Optimistic concurrency and HTTP 409 Conflict.
- 40 concurrent writes without data loss.
- Integrity of the shared JSON storage.

The script uses temporary test data and stops both API
processes after execution.

This test runs independently of the xUnit test suite
and is not part of the standard CI workflow.
