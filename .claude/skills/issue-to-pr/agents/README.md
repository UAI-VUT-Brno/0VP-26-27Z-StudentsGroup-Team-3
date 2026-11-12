# Agent roles

The subagents are defined in `.claude/agents/`, where Claude Code loads them and enforces their tool permissions. This folder documents their roles.

| Subagent | Stages | Allowed tools | Scope enforced by instructions | Issue |
|---|---|---|---|---|
| `issue-to-pr-planner` | 2 Plan | Read, Grep, Glob, Write | Writes only `plan.md` in the run folder; no shell access | OVP-T3-19 |
| `issue-to-pr-implementer` | 3 Implement | Read, Grep, Glob, Edit, Write, Bash, PowerShell | Edits only `src/` and `tests/`; local git commits and `dotnet format whitespace` only; no build, test or push | OVP-T3-19 |
| `issue-to-pr-qa` | 4 Verify | Defined in OVP-T3-20 | `dotnet build`, `dotnet test`, edit files in `src/` and `tests/` | OVP-T3-20 |
| `issue-to-pr-release` | 1 Prepare, 5 Document, 6 Publish | Defined in OVP-T3-21 | git, `gh pr create`, edit files in `docs/` and `README*.md` | OVP-T3-21 |

## How permissions are enforced

- **Allowed tools** are enforced by Claude Code through the `tools` field of each agent definition. For example, the planner has no Bash or PowerShell tool, so it cannot run commands at all.
- **Scope** (which folders an agent may edit, which commands it may run) is enforced by the agent's instructions. Claude Code cannot restrict file paths or individual shell commands per subagent through the `tools` field. Hooks could add such checks later; they are not used in this version.
- The orchestrator stops the workflow when an agent reports `blocked` or `failed`, and stage 6 refuses to commit any file not listed by the earlier stages.

## Input and output contract

Every agent receives the issue number and the run folder path from the orchestrator, reads the rules in `rules/` and its prompt in `prompts/`, and writes exactly one output file to the run folder.

| Subagent | Reads | Writes |
|---|---|---|
| `issue-to-pr-planner` | `issue.json`, `branch.md` | `plan.md` |
| `issue-to-pr-implementer` | `plan.md`, `branch.md` | `implementation.md`, commits on the feature branch |
