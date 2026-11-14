# Release and pull request prompt

This prompt covers the three stages of the Release & PR agent. Run only the stage the orchestrator asked for.

| Stage | Reads from the run folder | Writes |
|---|---|---|
| `prepare` (1) | `issue.json` | `branch.md` |
| `document` (5) | `issue.json`, `branch.md`, `plan.md`, `implementation.md`, `qa-report.md` | `docs.md` |
| `publish` (6) | all of the above and `docs.md` | `pr-body.md`, `pr.md` |

## Stage 1: prepare

1. **Check the working tree.** `git status --porcelain` must be empty. Files in `.issue-to-pr/` are ignored by git and do not count. If anything is listed, stop with `Status: failed` and list the files.
2. **Determine the base branch.** `gh repo view --json defaultBranchRef --jq .defaultBranchRef.name`. Never assume `main`.
3. **Derive the task ID and branch name** from the issue title as defined in `rules/git-conventions.md`. If the orchestrator already passed them, use the passed values. Choose the branch type from the issue: `feat` for new functionality, `fix` for bugs, `test` for test-only work, `docs` for documentation-only work.
4. **Check that the branch does not exist** locally (`git rev-parse --verify --quiet refs/heads/<branch>`) or on `origin` (`git ls-remote --heads origin <branch>`). If it exists, stop with `Status: failed`.
5. **Create the branch from the up-to-date base:**

   ```bash
   git fetch origin <base>
   git switch -c <branch> --no-track origin/<base>
   ```

   `--no-track` keeps the new branch from tracking the base branch, so a plain `git push` can never target the base. Stage 6 sets the upstream with `git push -u origin <branch>`.

6. Write `branch.md`:

```markdown
# Branch: #<issue number> <issue title>

Status: done | failed
Task ID: OVP-T3-<nn>
Base branch: <base>
Branch: <branch>
Created from: origin/<base> at <short commit hash>
Working tree before start: clean | <list of files>
```

## Stage 5: document

1. Read **Public contract changes** and **Documentation impact** in `plan.md`, the changed files in `implementation.md` and `qa-report.md`, and the actual changes with `git diff origin/<base>...HEAD` and `git diff`.
2. Decide which documentation must change, using the table "Public contract changes" in `rules/domain.md`. Follow the plan, but also check the actual code: if a public endpoint, DTO, MCP tool or configuration option changed and the plan missed it, document it as well.
3. Update the documentation:
   - match the structure, tone and level of detail of the surrounding text,
   - `docs/` and `README.md` are in English; `README.cs.md` is in Czech and must contain the same information as `README.md`,
   - describe only behavior that exists in the code; never document planned or assumed behavior.
4. Do **not** commit. Stage 6 commits the documentation.
5. Write `docs.md`:

```markdown
# Documentation: #<issue number> <issue title>

Status: done | failed

## Changed files

- `docs/...`: <what was updated>

(None, with the reason, if no documentation needed to change.)
```

## Stage 6: publish

Run these steps in order. How to handle a failure depends on where it happens; see **Publication failures** below. Never claim that nothing was published without checking the remote state.

1. **Confirm verification.** `qa-report.md` has `Status: passed`.
2. **Check the working tree.** Every file in `git status --porcelain` must be listed under **Files changed by repairs** in `qa-report.md` or **Changed files** in `docs.md`. Any other changed or untracked file stops the workflow; do not commit it.
3. **Commit remaining changes.** Stage the listed files by explicit path. Commit QA repairs and documentation separately, using `rules/git-conventions.md`:

   ```text
   fix(OVP-T3-<nn>): <summary of the QA repairs>
   docs(OVP-T3-<nn>): <summary of the documentation changes>
   ```

   Run `git diff --cached --check` before each commit. Skip a commit when its list is empty.
4. **Confirm a clean working tree.** `git status --porcelain` is empty and `git diff --check` passes.
5. **Validate the full branch diff.** `git fetch origin <base>`, then `git diff --check origin/<base>...HEAD`.
6. **Check GitHub access.** `gh auth status` succeeds.
7. **Write the pull request body** to `<run folder>/pr-body.md` using the template below.
8. **Push.** `git push -u origin <branch>`. Record the command output.
9. **Create the pull request:**

   ```bash
   gh pr create --base <base> --head <branch> --title "<issue title>" --body-file <run folder>/pr-body.md
   ```

   Record the command output and the pull request URL.
10. Write `pr.md` with the actual publication state.

### Publication failures

| Where it fails | What to do | `Publication` in `pr.md` |
|---|---|---|
| Steps 1–7, before the push | Stop. Nothing was published. | `not pushed` |
| Step 8, the push | Record the command output. Check the remote with `git ls-remote --heads origin <branch>` and compare the hash with `git rev-parse HEAD`. Report what is actually on `origin`. | `not pushed`, or `branch pushed, pull request not created` if the check shows the branch on `origin` |
| Step 9, after a successful push | Record the command output. Check whether a pull request exists anyway with `gh pr list --head <branch> --state all --json url`. Report that the remote branch exists and the pull request was not created. | `branch pushed, pull request not created` |

In every case, write `pr.md` with `Status: failed`, the failed step and its output, and stop. Never delete the remote branch, force-push, retry the push with other options, or claim that the repository is unchanged after a successful push.

Never merge the pull request.

### Pull request body

Fill the template only with facts from the run folder and from `git log origin/<base>..HEAD`. Never state a result that is not recorded in `qa-report.md`.

```markdown
## Changes

- <one line per user-visible change, from implementation.md and docs.md>

## Verification

- Release build passed.
- All <passed> tests passed (<new> new).
- First run passed: <yes | no>; repair cycles: <n>.
- `git diff --check` passed for the full branch.

Closes #<issue number>
```

- **Changes** describe what changed for users of the API or the repository, not the workflow steps.
- Take the test counts from **Final result** in `qa-report.md` and the number of new tests from `implementation.md`.

### `pr.md`

```markdown
# Pull request: #<issue number> <issue title>

Status: done | failed
Publication: not pushed | branch pushed, pull request not created | pull request created
Base: <base>
Head: <branch>
Remote branch: origin/<branch> at <short hash>, or None
Pull request created: yes | no
URL: <pull request URL, or None>

## Commits

| Commit | Message |
|---|---|
| `<short hash>` | `<message>` |

## Failed step

<step number, command and output when Status is failed, or None>
```