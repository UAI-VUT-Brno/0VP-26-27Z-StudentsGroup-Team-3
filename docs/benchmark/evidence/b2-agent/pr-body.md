## Changes

- Add `PATCH /api/todos/{id}/priority`, which changes only the priority of an existing todo and keeps all other fields unchanged.
- Add the `UpdateTodoPriorityRequest` DTO with `priority` (`Low`, `Normal`, `High`) and `version`, both required; `version` must be at least 1.
- The endpoint uses optimistic concurrency: `200 OK` returns the updated todo with the incremented `version`, a stale `version` returns `409 Conflict` as ProblemDetails, an unknown id returns `404 Not Found` and invalid input returns `400 Bad Request` as ValidationProblemDetails.
- Document the endpoint in `docs/OpenAPI.md`, `README.md` and `README.cs.md`.

## Verification

- Release build passed.
- All 175 tests passed (20 new).
- First run passed: yes; repair cycles: 0.
- `git diff --check` passed.

Closes #55

🤖 Generated with [Claude Code](https://claude.com/claude-code)
