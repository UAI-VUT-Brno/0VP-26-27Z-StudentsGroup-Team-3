
# OpenAPI 3.x API Documentation

The API utilizes built-in OpenAPI support in .NET 10 (`Microsoft.AspNetCore.OpenApi`) complemented by Swagger UI.

## Runtime Documentation Access

- **Interactive Swagger UI:** `http://localhost:5258/swagger`
- **OpenAPI 3.x Specification (JSON):** `http://localhost:5258/openapi/v1.json`

## Covered Endpoints and Response Status Codes

All controllers are annotated with XML documentation comments and `[ProducesResponseType]` attributes.

### TodosController (`/api/todos`)

- `GET /api/todos` – 200 OK (task list with optional `isCompleted` filter)
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

### Backward Compatibility

The existing `POST /api/todos` endpoint remains unchanged.

Tasks created without a due date have `dueAtUtc: null`. The `dueAtUtc` field is included in the standard todo API response.
