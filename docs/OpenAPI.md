
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
- `PUT /api/todos/{id}` – 200 OK / 400 Bad Request / 404 Not Found
- `POST /api/todos/{id}/complete` – 200 OK / 404 Not Found
- `DELETE /api/todos/{id}` – 204 No Content / 404 Not Found

### InfoController (`/api/info`)

- `GET /api/info` – 200 OK (diagnostic information about the instance and in-memory storage)

## Weekly Recurring Todo Creation

### POST /api/todos/repeat

Creates a sequence of independent todo items with weekly due dates.

The first occurrence is created at `firstDueAtUtc`. Each additional occurrence is scheduled exactly seven days after the previous one.

The total number of created items is `repeatCount + 1`.

An optional `userId` string may be supplied in the recurring creation body; it is copied to every occurrence.

### Request Body

```json
{
  "title": "Weekly team meeting",
  "description": "Project status meeting",
  "userId": "team-user",
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
| `userId` | Optional string user identifier |
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
    "userId": "team-user",
    "priority": "High",
    "isCompleted": false,
    "createdAtUtc": "2026-10-19T20:00:00+00:00",
    "dueAtUtc": "2026-10-20T10:00:00+00:00",
    "completedAtUtc": null
  },
  {
    "id": "c7c1d760-1234-4567-8901-222222222222",
    "title": "Weekly team meeting",
    "description": "Project status meeting",
    "userId": "team-user",
    "priority": "High",
    "isCompleted": false,
    "createdAtUtc": "2026-10-19T20:00:00+00:00",
    "dueAtUtc": "2026-10-27T10:00:00+00:00",
    "completedAtUtc": null
  }
]
```

The example shows only the first two occurrences. The complete response contains four items for `repeatCount: 3`.

### Recurrence Behavior

- Every occurrence has a unique identifier.
- Title, description and priority are copied to each occurrence.
- Every occurrence starts incomplete.
- Every occurrence has its own due date.
- Completing or deleting one occurrence does not affect the others.
- Occurrences are regular todo items, not persistent recurrence rules.
- Data is stored in memory and does not survive an application restart.

## User Tagging and Filtering

The `userId` field is optional on `POST /api/todos`, `POST /api/todos/repeat`, and `PUT /api/todos/{id}` request bodies, and is included in todo responses. A full update that omits `userId` clears the existing value.

### GET /api/todos

The endpoint accepts two optional query parameters:

| Parameter | Type | Behavior |
|---|---|---|
| `isCompleted` | boolean | Returns only completed (`true`) or open (`false`) tasks. |
| `userId` | string | Returns only tasks whose `userId` exactly matches the supplied value. |

Both filters can be combined, for example:

```http
GET /api/todos?userId=alice&isCompleted=false
```

Omitting `userId` returns tasks for all users, including tasks without a user identifier.

### Backward Compatibility

`userId` is optional on todo creation and update. Existing tasks and requests that omit it continue to work, and omitted user filters return all tasks regardless of whether they have a user identifier.

Tasks created without a due date have `dueAtUtc: null`. The `dueAtUtc` field is included in the standard todo API response.
