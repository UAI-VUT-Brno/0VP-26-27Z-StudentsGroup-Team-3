# OpenAPI 3.x Dokumentace API

API využívá vestavěnou podporu standardu OpenAPI v .NET 10 (`Microsoft.AspNetCore.OpenApi`) doplněnou o Swagger UI.

## Přístup k dokumentaci za běhu
* **Interaktivní Swagger UI:** `http://localhost:5258/swagger`
* **OpenAPI 3.x specifikace (JSON):** `http://localhost:5258/openapi/v1.json`

## Pokryté endpointy a návratové stavy
Všechny kontrolery jsou opatřeny XML komentáři a atributy `[ProducesResponseType]`:

### TodosController (`/api/todos`)
* `GET /api/todos` – 200 OK (seznam úkolů s volitelným filtrem `isCompleted`)
* `GET /api/todos/{id}` – 200 OK / 404 Not Found (ProblemDetails)
* `POST /api/todos` – 201 Created (hlavička Location) / 400 Bad Request (ValidationProblemDetails)
* `PUT /api/todos/{id}` – 200 OK / 400 Bad Request / 404 Not Found
* `POST /api/todos/{id}/complete` – 200 OK / 404 Not Found
* `DELETE /api/todos/{id}` – 204 No Content / 404 Not Found

### InfoController (`/api/info`)
* `GET /api/info` – 200 OK (diagnostické informace o instanci a paměťovém úložišti)