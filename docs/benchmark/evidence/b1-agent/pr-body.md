## Changes

- Todos have an optional `userId` string (max 100 characters, trimmed, blank stored as `null`), accepted by `POST /api/todos` and `PUT /api/todos/{id}` and returned in every todo response.
- `GET /api/todos` accepts an optional `userId` query parameter (exact, case-sensitive match) that can be combined with `isCompleted`; a value longer than 100 characters returns 400 ValidationProblemDetails.
- Todos without a `userId`, including existing JSON data, keep working and are returned when no user filter is given.
- `userId` is persisted by both the in-memory and the JSON file repository.
- `docs/OpenAPI.md`, `README.md` and `README.cs.md` document the `userId` field and the user filter.

## Verification

- Release build passed.
- All 115 tests passed (29 new).
- First run passed: no; repair cycles: 1.
- `git diff --check` passed for the full branch.

Closes #54

🤖 Generated with [Claude Code](https://claude.com/claude-code)
