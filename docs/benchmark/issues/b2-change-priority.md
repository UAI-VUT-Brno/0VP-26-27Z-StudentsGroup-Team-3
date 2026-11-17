# B2 synthetic benchmark issue

Create this issue on GitHub exactly as written below, with the label `benchmark`.

**Title:**

```text
[Benchmark] Change todo priority with PATCH /api/todos/{id}/priority
```

**Body:**

```markdown
## Goal

Allow clients to change only the priority of an existing todo, without sending the whole todo as in `PUT /api/todos/{id}`.

## Scope

- Add `PATCH /api/todos/{id}/priority`.
- The request body contains `priority` (`Low`, `Normal` or `High`) and `version` (the version the client last read).
- The endpoint changes only the priority; all other fields stay unchanged.
- It uses optimistic concurrency like `PUT /api/todos/{id}`: a stale `version` returns `409 Conflict`.
- On success it returns `200 OK` with the updated todo, including the new `version`.

## Done when

- [ ] A valid request changes the priority, keeps all other fields and increments `version`
- [ ] An unknown id returns `404 Not Found` as ProblemDetails
- [ ] A stale `version` returns `409 Conflict` as ProblemDetails and changes nothing
- [ ] An invalid priority or a missing `version` returns `400 Bad Request` as ValidationProblemDetails
- [ ] The behavior is covered by automated tests
- [ ] The endpoint is documented in `docs/OpenAPI.md`
```
