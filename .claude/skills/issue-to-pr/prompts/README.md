# Prompt library

Stage-specific prompts used by the subagents.

| Prompt | Used by | Purpose | Issue |
|---|---|---|---|
| `planner.md` | `issue-to-pr-planner` | Requirement decomposition and affected-file analysis | OVP-T3-19 |
| `implementer.md` | `issue-to-pr-implementer` | Layered C# generation and tests | OVP-T3-19 |
| `diagnostic-repair.md` | `issue-to-pr-qa` | Analysis and repair of compiler (`CSxxxx`) and test failures | OVP-T3-20 |
| `pr-generator.md` | `issue-to-pr-release` | Pull request title and body | OVP-T3-21 |
