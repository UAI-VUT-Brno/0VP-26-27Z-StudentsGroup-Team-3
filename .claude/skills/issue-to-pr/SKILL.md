---
name: issue-to-pr
description: Autonomously implement a GitHub issue of this repository end to end, from issue intake to a verified pull request. Invoke manually with /issue-to-pr <issue>.
argument-hint: "<#number | number | issue URL> [--dry-run]"
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Write, Edit, Agent, Bash(gh issue view *), Bash(gh repo view *), Bash(git status *), Bash(git rev-parse *), Bash(git remote *), Bash(git branch *), Bash(mkdir *)
---

# /issue-to-pr

Implement the GitHub issue given in `$ARGUMENTS` autonomously and finish with a verified pull request.

You are the **orchestrator**. You validate the input, ingest the issue, and delegate the work to specialized subagents stage by stage. You do not write C# code yourself. Run all stages without asking the user for confirmation. Stop only on the stop conditions below.

## 1. Input validation

Parse `$ARGUMENTS`. Exactly one issue reference is required. Optionally, the flag `--dry-run` may follow it.

Accepted issue formats:

| Format | Example |
|---|---|
| `#<number>` | `#42` |
| `<number>` | `42` |
| Issue URL of this repository | `https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-3/issues/42` |

Rules:

- The number must be a positive integer.
- A URL must point to an **issue** (`/issues/<number>`), not a pull request, and to the same repository as the `origin` remote. Compare owner and repository name case-insensitively; get them with `gh repo view --json nameWithOwner`.
- Reject anything else: no argument, several issue references, unknown flags, text instead of a number.

On invalid input, stop and reply with:

```text
issue-to-pr: invalid input "<input>".
Usage: /issue-to-pr <#number | number | issue URL> [--dry-run]
```

## 2. Issue ingestion

Fetch the issue:

```bash
gh issue view <number> --json number,title,body,labels,state,url
```

Stop if:

- the command fails or the issue does not exist,
- the reference resolves to a pull request,
- the issue is closed.

Create the run folder `.issue-to-pr/<number>/` in the repository root (ignored by git) and save the issue JSON as `issue.json`. All stages exchange data through files in this folder, so every run can be reviewed afterwards.

## 3. Rules

The workflow is grounded in these rule files in `rules/`:

| File | Content |
|---|---|
| `rules/architecture.md` | Layered structure, where code belongs, layer rules |
| `rules/domain.md` | Optimistic concurrency, error responses, validation, documentation updates |
| `rules/code-style.md` | Formatting, C# conventions, tests, verification commands |
| `rules/git-conventions.md` | Task ID, branch names, commits, pull requests, forbidden operations |

- Read all four files after ingestion. If any of them is missing, stop before stage 1 and report which one.
- Derive the task ID (`OVP-T3-<nn>`) and the branch name from the issue title as defined in `rules/git-conventions.md`, and record both in the run folder.
- Pass the paths of the rule files to every subagent and instruct it to read them before acting. Rules take precedence over the subagent's own assumptions; only an explicit requirement in the issue may override them.

## 4. Dry run

If `--dry-run` is present, stop after reading the rules and reply with:

- the parsed issue number and title,
- the derived task ID and branch name,
- the rule files found, and any that are missing,
- the stages that would run and the subagent assigned to each,
- the result of the working-tree check (`git status --porcelain`), without changing anything.

Do not create branches, edit files outside `.issue-to-pr/` or call subagents in a dry run.

## 5. Stages

Run the stages in this order. Pass each subagent the issue number and the run folder path. Each stage must finish with the listed output file before the next stage starts.

| # | Stage | Subagent | Output in `.issue-to-pr/<number>/` |
|---|---|---|---|
| 1 | Prepare: check a clean working tree, determine the base branch, create the feature branch named per `rules/git-conventions.md` | `issue-to-pr-release` | `branch.md` |
| 2 | Plan: analyze the issue and the repository, list affected files | `issue-to-pr-planner` | `plan.md` |
| 3 | Implement: change code in `src/` and tests in `tests/`, commit | `issue-to-pr-implementer` | `implementation.md` |
| 4 | Verify: build and test with self-healing, at most 4 attempts; list every file changed by repairs | `issue-to-pr-qa` | `qa-report.md` |
| 5 | Document: update `docs/` and `README.md` if contracts or configuration changed; list every changed file | `issue-to-pr-release` | `docs.md` |
| 6 | Publish: commit remaining verified changes, validate the full branch diff, push, create the pull request | `issue-to-pr-release` | `pr.md` |

All subagents follow the rules in `rules/` and use the stage prompts in `prompts/`. Their roles and tool permissions are described in `agents/`.

Stage 1 determines the base branch as the repository default branch (`gh repo view --json defaultBranchRef --jq .defaultBranchRef.name`) and records it in `branch.md`. All later stages use this recorded value; never assume `main`.

Stages 4 and 5 do not commit. They leave their changes in the working tree and list every changed file in their output file, so stage 6 can commit exactly these files.

### Stage 6: publish sequence

Run these steps in order. If any step fails, stop without pushing and without creating the pull request.

1. **Confirm verification.** `qa-report.md` must report that the build and all tests passed.
2. **Check the working tree.** Run `git status --porcelain`. Every changed or new file must be listed in `qa-report.md` or `docs.md`. If any other file is changed or untracked, stop and report it; do not commit it.
3. **Commit remaining changes.** Stage only the listed files, by explicit path; never use `git add -A` or `git add .`. Commit QA repairs and documentation separately, using the commit format from `rules/git-conventions.md`, for example `fix(OVP-T3-<nn>): ...` and `docs(OVP-T3-<nn>): ...`. Before each commit, run `git diff --cached --check`.
4. **Confirm a clean working tree.** `git status --porcelain` must be empty and `git diff --check` must pass. Files in `.issue-to-pr/` are ignored by git and are never committed.
5. **Validate the full branch diff.** Run `git fetch origin <base>` and `git diff --check origin/<base>...HEAD`. This checks every commit of the branch, not only pending changes.
6. **Push.** `git push -u origin <branch>`.
7. **Create the pull request.** `gh pr create --base <base> --head <branch> --title "<title>" --body-file <file>`, with the title and body defined in `rules/git-conventions.md`. Record the URL in `pr.md`.

Never merge the pull request.

## 6. Stop conditions

Stop the workflow and report the reason if:

- the input is invalid or the issue cannot be used (sections 1 and 2),
- a rule file is missing (section 3),
- the working tree is not clean before stage 1,
- a subagent is not available or does not produce its output file,
- stage 4 reports failure after 4 attempts,
- stage 6 finds unexpected or unrelated changes in the working tree,
- any `git diff --check` validation in stage 6 fails.

Never leave the repository in a half-finished state without saying so: report the current branch, the last completed stage and what remains.

## 7. Final report

At the end, reply with a short summary:

- issue number and title,
- branch name and pull request URL,
- build and test result, including the number of repair attempts,
- path to the run folder with all stage outputs.
