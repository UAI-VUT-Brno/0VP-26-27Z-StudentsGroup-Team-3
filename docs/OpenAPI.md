
# OpenAPI 3.x API Documentation

The API utilizes built-in OpenAPI support in .NET 10 (`Microsoft.AspNetCore.OpenApi`) complemented by Swagger UI.

## Runtime Documentation Access

- **Interactive Swagger UI:** `http://localhost:5258/swagger`
- **OpenAPI 3.x Specification (JSON):** `http://localhost:5258/openapi/v1.json`

## Covered Endpoints and Response Status Codes

All controllers are annotated with XML documentation comments and `[ProducesResponseType]` attributes.

### TodosController (`/api/todos`)

- `GET /api/todos` – 200 OK (task list with optional `isCompleted` and `userId` filters) / 400 Bad Request (ValidationProblemDetails)
- `GET /api/todos/{id}` – 200 OK / 404 Not Found (ProblemDetails)
- `POST /api/todos` – 201 Created (Location header) / 400 Bad Request (ValidationProblemDetails); accepts an optional `userId` body field
- `POST /api/todos/repeat` – 201 Created (created occurrences) / 400 Bad Request (ValidationProblemDetails)
- `PUT /api/todos/{id}` – 200 OK / 400 Bad Request / 404 Not Found; accepts an optional `userId` body field
- `POST /api/todos/{id}/complete` – 200 OK / 404 Not Found
- `DELETE /api/todos/{id}` – 204 No Content / 404 Not Found

### InfoController (`/api/info`)

- `GET /api/info` – 200 OK (diagnostic information about the instance and in-memory storage)

## User Identification and "My Tasks" Filtering

Todos can carry an optional user identifier, `userId`. It is set when a todo is created or updated, returned in every todo response, and can be used to list only the todos of one user.

`userId` is an opaque label, not authentication. The API does not verify that the user exists, and any client can read or set any `userId`.

### Query Parameters of GET /api/todos

| Parameter | Type | Description |
|---|---|---|
| `isCompleted` | boolean, optional | `true` returns only completed todos, `false` only open todos; omit for all |
| `userId` | string, optional | Returns only todos assigned to this user; maximum 100 characters; omit or leave empty for all users |

Example request for the open todos of one user:

```http
GET /api/todos?userId=alice&isCompleted=false
```

### Filter Behavior

- The query value is trimmed before matching. An empty or whitespace-only `userId` is treated as no filter, the same as omitting it.
- Matching is exact and case-sensitive: `alice` and `Alice` are different users.
- Todos without a `userId` never match a `userId` filter. They are returned only when `userId` is omitted.
- `userId` and `isCompleted` are combined with a logical AND.
- An unknown `userId` returns `200 OK` with an empty array.
- Sorting is unchanged: priority descending, then creation time.
- A `userId` query value longer than 100 characters returns `400 Bad Request` with `ValidationProblemDetails` and the error message `User ID cannot exceed 100 characters.`

### Request Body Field

`POST /api/todos` and `PUT /api/todos/{id}` accept an optional `userId` field:

```json
{
  "title": "Prepare sprint review",
  "description": "Collect demo notes",
  "priority": "High",
  "userId": "alice"
}
```

| Field | Validation |
|---|---|
| `userId` | Optional, maximum 100 characters; trimmed; empty or whitespace-only means not assigned (`null`) |

A longer value returns `400 Bad Request` with `ValidationProblemDetails`, and no todo is created or changed.

`PUT /api/todos/{id}` is a full update. A `PUT` request without `userId`, or with an empty value, clears the stored user identifier, in the same way as `description`.

`POST /api/todos/repeat` does not accept `userId`. Its occurrences are created with `userId: null`.

### Response Field

Every todo response contains `userId`, either the stored value or `null`:

```json
{
  "id": "3f2b8c1e-1234-4567-8901-333333333333",
  "title": "Prepare sprint review",
  "description": "Collect demo notes",
  "priority": "High",
  "isCompleted": false,
  "createdAtUtc": "2026-11-16T09:00:00+00:00",
  "dueAtUtc": null,
  "completedAtUtc": null,
  "version": 1,
  "userId": "alice"
}
```

The ID and creation timestamp are illustrative.

### Error Content Type

Validation errors from `TodosController`, including the `userId` errors above, are returned with the content type `application/json`, because the controller declares `[Produces("application/json")]`. The body is a standard `ValidationProblemDetails` object.

### Backward Compatibility

- Requests without `userId` keep working; the created todo has `userId: null`.
- `GET /api/todos` without `userId` returns the same result as before, including todos with and without a user.
- Existing todos and existing JSON storage files without a `userId` property load unchanged with `userId: null`.

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
