# Documentation: #55 [Benchmark] Change todo priority with PATCH /api/todos/{id}/priority

Status: done

## Changed files

- `docs/OpenAPI.md`: added `PATCH /api/todos/{id}/priority` (200 / 400 ValidationProblemDetails / 404 / 409 ProblemDetails) to the TodosController endpoint list after `PUT`; added the section "Changing Todo Priority" with the purpose (only the priority changes, all other fields stay unchanged, plain JSON body, not JSON Patch), a request body example, a validation table (`priority` required, `Low`/`Normal`/`High`; `version` required, >= 1, must match the stored version), a response table (200 `TodoItemDto` with incremented `version`, 400, 404, 409 with data unchanged) and an example response; added a bullet to "Optimistic Concurrency Control" saying the PATCH endpoint follows the same `version` rules.
- `README.md`: added the `PATCH /api/todos/{id}/priority` row to the "API endpoints" table after `PUT`; added one sentence with a body example to "Optimistic concurrency".
- `README.cs.md`: the same changes in Czech (table row "Změna pouze priority včetně očekávané verze" and the matching sentence in "Optimistic concurrency").

## Notes

- All three files are UTF-8 with CRLF line endings in the repository; the existing CRLF line endings were kept, even though `rules/code-style.md` lists LF for `*.md`.
- `git diff --check` passed. Nothing was committed.
- No MCP, storage or configuration changes, so `docs/MCP.md` and `docs/multi-instance.md` were not changed.
