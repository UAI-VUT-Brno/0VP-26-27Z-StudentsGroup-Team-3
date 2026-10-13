# OpenAPI 3.x API Documentation

The API utilizes built-in OpenAPI support in .NET 10 (`Microsoft.AspNetCore.OpenApi`) complemented by Swagger UI.

## Runtime Documentation Access
- **Interactive Swagger UI:** `http://localhost:5258/swagger`
- **OpenAPI 3.x Specification (JSON):** `http://localhost:5258/openapi/v1.json`

## Covered Endpoints and Response Status Codes
All controllers are annotated with XML documentation comments and `[ProducesResponseType]` attributes:

### TodosController (`/api/todos`)
- `GET /api/todos` – 200 OK (task list with optional `isCompleted` filter)
- `GET /api/todos/{id}` – 200 OK / 404 Not Found (ProblemDetails)
- `POST /api/todos` – 201 Created (Location header) / 400 Bad Request (ValidationProblemDetails)
- `PUT /api/todos/{id}` – 200 OK / 400 Bad Request / 404 Not Found
- `POST /api/todos/{id}/complete` – 200 OK / 404 Not Found
- `DELETE /api/todos/{id}` – 204 No Content / 404 Not Found

### InfoController (`/api/info`)
- `GET /api/info` – 200 OK (diagnostic information about the instance and in-memory storage)