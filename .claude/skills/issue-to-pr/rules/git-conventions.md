# Git conventions

## Task ID

Issues are titled `OVP-T3-<nn> <summary>`, for example `OVP-T3-14 Expose read-only ToDo queries through MCP`. The task ID `OVP-T3-<nn>` is taken from the issue title. If the title has no task ID, use the issue number instead (`t3-<issue_number>` in branch names, `#<issue_number>` in commits).

## Branches

The orchestrator (`SKILL.md`) and all subagents use this convention.

Format:

```text
<type>/t3-<nn>-<short-slug>
```

| Part | Rule |
|---|---|
| `<type>` | `feat` for new functionality, `fix` for bug fixes, `test` for test-only changes, `docs` for documentation-only changes |
| `<nn>` | Task number from the issue title, for example `14` for `OVP-T3-14` |
| `<short-slug>` | 2–4 lowercase English words from the issue title, separated by hyphens |

Examples from the repository: `feat/t3-14-mcp-tools`, `fix/t3-13-api-validation`, `test/t3-15-mcp-integration`, `docs/t3-16-mcp-verification`.

- Create the branch from an up-to-date `main`.
- If the branch already exists locally or on `origin`, stop and report it. Do not reuse or overwrite it.

## Commits

Format (Conventional Commits with the task ID as scope):

```text
<type>(OVP-T3-<nn>): <Imperative summary>
```

| Type | Use for |
|---|---|
| `feat` | New or changed production code |
| `fix` | Bug fixes |
| `test` | Tests only |
| `docs` | Documentation only |
| `refactor` | Code changes without behavior change |

Examples:

```text
feat(OVP-T3-24): Add priority filter to GET /api/todos
test(OVP-T3-24): Cover priority filter validation and empty results
docs(OVP-T3-24): Document priority filter in OpenAPI.md
```

- The summary starts with a capital letter, uses the imperative mood and has no trailing period.
- Keep commits small and focused: production code, tests and documentation may be separate commits.
- Never commit generated data, logs, build outputs, `.issue-to-pr/` run files or secrets.
- Do not add `Co-Authored-By` or other trailers.

## Pull requests

- **Title:** the issue title, for example `OVP-T3-14 Expose read-only ToDo queries through MCP`.
- **Base branch:** `main`.
- **Body:** follows this structure, as in previous pull requests:

```markdown
## Changes

- <what changed, one line per item>

## Verification

- Release build passed.
- All <N> tests passed (<M> new).
- `git diff --check` passed.

Closes #<issue_number>
```

## Forbidden operations

- Pushing directly to `main`, merging pull requests, or force-pushing.
- Rewriting history that is already on `origin` (`rebase`, `reset --hard`, `commit --amend` on pushed commits).
- Deleting branches, tags or issues.
- Changing git configuration, remotes or credentials.
