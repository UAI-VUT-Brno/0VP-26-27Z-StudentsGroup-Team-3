# Documentation: #54 [Benchmark] Add user identification to tasks and "My Tasks" filtering

Status: done

## Changed files

- `docs/OpenAPI.md`: In "TodosController", `GET /api/todos` now lists the optional `isCompleted` and `userId` filters and the 400 Bad Request (ValidationProblemDetails) response; `POST /api/todos` and `PUT /api/todos/{id}` mention the optional `userId` body field. Added the section `## User Identification and "My Tasks" Filtering` covering: the query parameter table, an example request (`?userId=alice&isCompleted=false`), filter behavior (trimmed, blank = no filter, exact case-sensitive match, untagged todos never match, AND with `isCompleted`, unknown user returns an empty array, sorting unchanged, 400 when the query value exceeds 100 characters), the request body field and its validation (optional, max 100, trimmed, blank = `null`), PUT full-update behavior (omitting `userId` clears it), `POST /api/todos/repeat` not accepting `userId` (occurrences get `null`), a response example with `version` and `userId`, the `application/json` error content type caused by the controller-level `[Produces("application/json")]`, the note that `userId` is not authentication, and backward compatibility. The existing recurring section is unchanged.
- `README.md`: The `GET /api/todos` row in "API endpoints" now lists the `?userId=<id>` filter; added a short "User identification" subsection (body field, response field, exact case-sensitive filter combinable with `isCompleted`, PUT clears an omitted `userId`, not authentication, link to `docs/OpenAPI.md`).
- `README.cs.md`: The same changes in Czech ("Seznam úkolů; volitelné filtry ..." row and the subsection "Identifikace uživatele").

## Notes

- All statements were checked against the code in `src/` (`TodosController.GetAll`, `TodoMapper.NormalizeUserId`, the DTOs and both repository filters), not only against the plan.
- `docs/multi-instance.md` needed no change: multi-instance behavior is unchanged.
- The existing working-tree change `tests/UAI.ZeroVP.Tests/Integration/UserTodoFilterApiTests.cs` (QA repair) was not touched.
- The files keep their existing encoding (UTF-8, no BOM) and line endings; `git diff --check` passed. Nothing was committed.
