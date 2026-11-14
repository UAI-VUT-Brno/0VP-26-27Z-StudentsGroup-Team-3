# Implementer prompt

Use this method to implement `plan.md`. Work through the steps in order.

## 1. Prepare

- Confirm that `git status --porcelain` shows no changes outside `.issue-to-pr/`. If it does, stop with `Status: failed`; never commit changes you did not make.
- For every file in **Affected files**, read the whole file before changing it.
- For every new file, read the existing example the plan references, or the closest file in the same folder, and copy its structure: namespace, usings, XML comments, class modifiers, formatting.

## 2. Implement layer by layer

Follow **Implementation order** from the plan. If it is missing, use this order:

1. **DTO** (`src/UAI.ZeroVP.DTO/`): records, DataAnnotations, enums.
2. **Data** (`src/UAI.ZeroVP.Data/`): entity fields, `ITodoRepository` and **both** implementations.
3. **API** (`src/UAI.ZeroVP.API/`): `TodoMapper`, controller actions, MCP tools, `Program.cs` configuration.
4. **Tests** (`tests/UAI.ZeroVP.Tests/`): every test from the **Tests** table of the plan.

While implementing, apply these rules from `rules/` continuously:

- Controllers and MCP tools use only `ITodoRepository`; mapping goes through `TodoMapper`.
- Updates of existing todos go through `UpdateAsync`; catch `TodoConcurrencyException` and return `VersionConflict(ex)`.
- Errors use the existing ProblemDetails helpers (`NotFoundTodo`, `VersionConflict`) and are declared with `[ProducesResponseType]` and `<response code="...">`.
- Use the injected `TimeProvider` and pass `CancellationToken` through every async call.
- Every public type and member has an XML `<summary>`.
- Tests follow `Method_Scenario_ExpectedResult`, `// Arrange`, `// Act`, `// Assert`, and never touch developer data.

Never remove, skip or weaken an existing test. If the plan requires a behavior change that breaks an existing test, update that test to the new behavior and record it under **Deviations from the plan**.

## 3. Check your own work

Without building, review every changed file against this checklist:

- [ ] Every requirement and test from the plan is implemented.
- [ ] Every new `using`, type and member exists in the code you read; no invented APIs.
- [ ] Every method called with new parameters has been updated at all call sites, including both repository implementations and test doubles.
- [ ] New `.cs` files are UTF-8 with BOM; all files end with a newline and have no trailing whitespace.
- [ ] No file outside `src/` and `tests/` was changed.

Then normalize formatting of **newly created** C# files according to `.editorconfig` (UTF-8 with BOM, line endings, final newline, indentation). `--folder` works without building the solution:

```powershell
dotnet format whitespace --folder --include <new .cs files only>
```

Do not run the formatter on existing files you modified: it can reformat lines you did not change and alter the file encoding. Keep their original formatting and fix only your own lines by hand.

After formatting, run `git diff` and check that only your intended lines changed. If the formatter changed anything else, revert those changes.

If the `dotnet` command is not available, skip the formatter and record it under **Notes**.

## 4. Commit

Commit production code and tests separately, using the format from `rules/git-conventions.md`:

```text
feat(OVP-T3-<nn>): <Imperative summary of the production change>
test(OVP-T3-<nn>): <Imperative summary of the tests>
```

Use `fix` instead of `feat` when the issue is a bug fix. For each commit:

1. Stage files by explicit path: `git add <path> <path>`. Never use `git add -A` or `git add .`.
2. Run `git diff --cached --check` and fix any reported problem.
3. Commit.

After the last commit, `git status --porcelain` must show no changes outside `.issue-to-pr/`.

If the plan lists no changes in `src/` or `tests/`, make no commits and write `Status: done` with the note `No code changes required by the plan`.

## 5. Write the report

Write `implementation.md` in exactly this format:

```markdown
# Implementation: #<issue number> <issue title>

Status: done | failed
Branch: <current branch>

## Changed files

| File | Change | Covers |
|---|---|---|
| `src/...` | add / modify | R1 |

## Commits

| Commit | Message |
|---|---|
| `<short hash>` | `feat(OVP-T3-<nn>): ...` |

## Tests added or changed

- `<test file>`: `<test names>`

## Deviations from the plan

- <file or decision that differs from the plan and why, or None>

## Notes

- <anything the QA stage should know, or None>
```
