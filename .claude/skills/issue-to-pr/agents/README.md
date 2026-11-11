# Agent roles

The subagents are defined in `.claude/agents/`, where Claude Code loads them and enforces their tool permissions. This folder documents their roles.

| Subagent | Stages | Tool scope | Issue |
|---|---|---|---|
| `issue-to-pr-planner` | 2 Plan | Read-only access to the repository | OVP-T3-19 |
| `issue-to-pr-implementer` | 3 Implement | Edit files in `src/` and `tests/`, local git commits | OVP-T3-19 |
| `issue-to-pr-qa` | 4 Verify | `dotnet build`, `dotnet test`, edit files in `src/` and `tests/` | OVP-T3-20 |
| `issue-to-pr-release` | 1 Prepare, 5 Document, 6 Publish | git, `gh pr create`, edit files in `docs/` and `README*.md` | OVP-T3-21 |
