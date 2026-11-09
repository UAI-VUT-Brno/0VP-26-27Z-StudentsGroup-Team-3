
# OpenAPI 3.x API Documentation

The API utilizes built-in OpenAPI support in .NET 10 (`Microsoft.AspNetCore.OpenApi`) complemented by Swagger UI.

## Runtime Documentation Access

- **Interactive Swagger UI:** `http://localhost:5258/swagger`
- **OpenAPI 3.x Specification (JSON):** `http://localhost:5258/openapi/v1.json`

## Covered Endpoints and Response Status Codes

All controllers are annotated with XML documentation comments and `[ProducesResponseType]` attributes.

### TodosController (`/api/todos`)

- `GET /api/todos` – 200 OK (task list with optional `isCompleted` and `userId` filters)
- `GET /api/todos/{id}` – 200 OK / 404 Not Found (ProblemDetails)
- `POST /api/todos` – 201 Created (Location header) / 400 Bad Request (ValidationProblemDetails)
- `POST /api/todos/repeat` – 201 Created (created occurrences) / 400 Bad Request (ValidationProblemDetails)
- `PUT /api/todos/{id}` – 200 OK / 400 Bad Request / 404 Not Found / 409 Conflict (ProblemDetails)
- `POST /api/todos/{id}/complete` – 200 OK / 404 Not Found / 409 Conflict (ProblemDetails)
- `DELETE /api/todos/{id}` – 204 No Content / 404 Not Found

### InfoController (`/api/info`)

- `GET /api/info` – 200 OK (diagnostic information about the instance including unique `instanceId`, machine name, environment, version, uptime, and total count of stored todos)

## Weekly Recurring Todo Creation

### POST /api/todos/repeat

Creates a sequence of independent todo items with weekly due dates.

The first occurrence is created at `firstDueAtUtc`. Each additional occurrence is scheduled exactly seven days after the previous one.

The total number of created items is `repeatCount + 1`.

### Request Body

```json
{
  "title": "Weekly team meeting",
  "description": "Project status meeting",
  "priority": "High",
  "firstDueAtUtc": "2026-10-20T10:00:00Z",
  "repeatCount": 3
}
```

### Request Validation

| Field | Validation |
|---|---|
| `title` | Required, 1–200 characters |
| `description` | Optional, maximum 1000 characters |
| `userId` | Optional, maximum 100 characters |
| `priority` | Valid TodoPriority value; defaults to Normal |
| `firstDueAtUtc` | Required, valid UTC date and time (offset +00:00) |
| `repeatCount` | Required integer between 1 and 52 |

The recurrence sequence must not exceed the supported DateTimeOffset range.

Invalid requests return `400 Bad Request` with standard `ValidationProblemDetails`. No todo items are created when request validation fails.

### Successful Response

**HTTP 201 Created**

The response contains an array of `repeatCount + 1` created todo items.

Example response (IDs and creation timestamps are illustrative):

```json
[
  {
    "id": "c7c1d760-1234-4567-8901-111111111111",
    "title": "Weekly team meeting",
    "description": "Project status meeting",
    "userId": null,
    "priority": "High",
    "isCompleted": false,
    "createdAtUtc": "2026-10-19T20:00:00+00:00",
    "dueAtUtc": "2026-10-20T10:00:00+00:00",
    "completedAtUtc": null,
    "version": 1
  },
  {
    "id": "c7c1d760-1234-4567-8901-222222222222",
    "title": "Weekly team meeting",
    "description": "Project status meeting",
    "userId": null,
    "priority": "High",
    "isCompleted": false,
    "createdAtUtc": "2026-10-19T20:00:00+00:00",
    "dueAtUtc": "2026-10-27T10:00:00+00:00",
    "completedAtUtc": null,
    "version": 1
  }
]
```

The example shows only the first two occurrences. The complete response contains four items for `repeatCount: 3`.

### Recurrence Behavior

- Every occurrence has a unique identifier.
- Title, description, priority and user identifier are copied to each occurrence.
- Every occurrence starts incomplete.
- Every occurrence has its own due date.
- Completing or deleting one occurrence does not affect the others.
- Occurrences are regular todo items, not persistent recurrence rules.
- In normal application environments, items are stored in persistent JSON storage and survive restarts when the storage file is preserved. The `LoadTest` environment uses non-persistent in-memory storage.

### Backward Compatibility

The existing `POST /api/todos` endpoint remains unchanged.

Tasks created without a due date have `dueAtUtc: null`. The `dueAtUtc` field is included in the standard todo API response.


## User Identification and "My Tasks" Filtering

Todo items can be tagged with an optional string user identifier (`userId`).
It is a simple label, not an authentication mechanism.

### Assigning a user

`userId` can be sent in the body of `POST /api/todos`, `POST /api/todos/repeat`
and `PUT /api/todos/{id}`:

```json
{
  "title": "Prepare presentation",
  "priority": "High",
  "userId": "karel"
}
```

| Field | Validation |
|---|---|
| `userId` | Optional, maximum 100 characters |

- Surrounding whitespace is trimmed (`" karel "` is stored as `"karel"`).
- An empty or whitespace-only value is stored as `null` (no user assigned).
- `PUT` is a full update: omitting `userId` removes the assignment.

The `userId` field is included in every todo response (`null` when not assigned).

### Filtering

`GET /api/todos` accepts an optional `userId` query parameter:

| Request | Result |
|---|---|
| `GET /api/todos` | All todos (all users and unassigned) |
| `GET /api/todos?userId=karel` | Only todos of user `karel` |
| `GET /api/todos?userId=karel&isCompleted=false` | Open todos of user `karel` |

- Matching is exact and case-sensitive (`karel` ≠ `Karel`).
- An empty `userId` parameter is ignored (no filtering).

### Backward Compatibility

- `userId` remains optional in create and update requests. However, clients performing `PUT /api/todos/{id}` must retrieve and send the current `version`.
- Todos created before this change (or without `userId`) have `userId: null`
  and are returned when no `userId` filter is applied.
- Existing JSON storage files without the `userId` field load without migration.

## Optimistic Concurrency Control

Updates via `PUT /api/todos/{id}` require optimistic concurrency control to prevent lost updates across multiple clients and instances:

- The request body must include the `version` property with a minimum value of `1`.
- A successful update increments the stored `version` by `1`.
- If the supplied `version` does not match the currently stored version, the update is rejected with `409 Conflict`, leaving stored data unchanged.
- Existing clients performing `PUT` requests must be updated to retrieve and send the current `version`.

## Request Validation and Error Handling

The API provides standardized RFC 9457 `ProblemDetails` and `ValidationProblemDetails` responses:

- **Invalid Requests:** Malformed JSON, missing required fields, or invalid parameter types return `400 Bad Request`.
- **Unsupported Content-Type:** Non-JSON payloads for write requests return `415 Unsupported Media Type`.
- **Unknown Routes and Nonexistent IDs:** Unknown paths under `/api` or validly formatted IDs that do not exist return `404 Not Found`.
- **Unsupported Methods:** Invoking unsupported HTTP methods on existing routes returns `405 Method Not Allowed` with a valid `Allow` header.
- **Unknown Parameters and Fields:** Extra query parameters or unexpected JSON properties in request bodies are ignored and not persisted. Known fields continue to be strictly validated.
- **State Isolation:** Rejected requests do not modify stored items or increment version counters.
- **Malformed IDs:** Todo ID routes use the `{id:guid}` constraint. An incorrectly formatted GUID does not match the route and returns `404 Not Found`.
- **Invalid Query Values:** Invalid values of known typed query parameters, such as `isCompleted=not-a-boolean`, return `400 Bad Request` with validation errors.
- **Required Fields:** Unknown JSON properties do not bypass required-field validation.
