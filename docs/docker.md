# Docker and Docker Compose

## Overview

The ToDo API can run as a container. `docker-compose.yml` starts
**three API instances** on distinct host ports that share one persistent
JSON storage file through a named Docker volume.

| File                 | Purpose                                              |
|----------------------|------------------------------------------------------|
| `Dockerfile`         | Multi-stage build (SDK → slim ASP.NET runtime image) |
| `docker-compose.yml` | Three API replicas + shared `todo-data` volume       |
| `.dockerignore`      | Keeps build outputs, tests and docs out of the image |

## Requirements

- Docker Desktop / Docker Engine with Docker Compose v2

## Build the image

From the repository root:

```powershell
docker build -t uai-zerovp-api .
```

The image:

- uses `mcr.microsoft.com/dotnet/sdk:10.0` for restore/publish and
  `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime,
- runs as the non-root `app` user,
- listens on port `8080` (HTTP only),
- stores todos in `/data/todos.json` (`TodoStorage__FilePath`).

## Run multiple instances

```powershell
docker compose up --build -d
```

| Service | URL                     |
|---------|-------------------------|
| api-1   | http://localhost:5001   |
| api-2   | http://localhost:5002   |
| api-3   | http://localhost:5003   |

Swagger UI is enabled in Compose (`Swagger__Enabled=true`) at `/swagger`,
health check at `/health`.

Stop the instances:

```powershell
docker compose down          # keeps the data volume
docker compose down -v       # also deletes the shared data
```

## Verifying shared storage

Each instance reports a different `instanceId`:

```powershell
Invoke-RestMethod http://localhost:5001/api/info
Invoke-RestMethod http://localhost:5002/api/info
```

Create a todo through instance 1 and read it through instance 2:

```powershell
$task = Invoke-RestMethod `
    -Uri http://localhost:5001/api/todos `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"title":"Docker shared storage test","priority":"Normal"}'

Invoke-RestMethod "http://localhost:5002/api/todos/$($task.id)"
```

Persistence check – the todo must still exist after recreating containers:

```powershell
docker compose down
docker compose up -d
Invoke-RestMethod "http://localhost:5003/api/todos/$($task.id)"
```

Inspect the shared file directly:

```powershell
docker compose exec api-1 cat /data/todos.json
```

## Concurrency

All containers mount the same volume on the same host, so the existing
cross-process file lock (`todos.json.lock`), atomic file replacement and
optimistic concurrency (`version`, HTTP 409) described in
[multi-instance.md](multi-instance.md) apply unchanged.

## CI

GitHub Actions (`.github/workflows/ci.yml`) intentionally does **not** build
the container. It keeps running the standard `dotnet restore / build / test`
steps directly on the runner OS.