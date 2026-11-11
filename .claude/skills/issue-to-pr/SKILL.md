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

## 3. Dry run

If `--dry-run` is present, stop after ingestion and reply with:

- the parsed issue number and title,
- the stages that would run and the subagent assigned to each,
- the result of the working-tree check (`git status --porcelain`), without changing anything.

Do not create branches, edit files outside `.issue-to-pr/` or call subagents in a dry run.

## 4. Stages

Run the stages in this order. Pass each subagent the issue number and the run folder path. Each stage must finish with the listed output file before the next stage starts.

| # | Stage | Subagent | Output in `.issue-to-pr/<number>/` |
|---|---|---|---|
| 1 | Prepare: check a clean working tree and create the feature branch | `issue-to-pr-release` | `branch.md` |
| 2 | Plan: analyze the issue and the repository, list affected files | `issue-to-pr-planner` | `plan.md` |
| 3 | Implement: change code in `src/` and tests in `tests/`, commit | `issue-to-pr-implementer` | `implementation.md` |
| 4 | Verify: build and test with self-healing, at most 4 attempts | `issue-to-pr-qa` | `qa-report.md` |
| 5 | Document: update `docs/` and `README.md` if contracts or configuration changed | `issue-to-pr-release` | `docs.md` |
| 6 | Publish: `git diff --check`, push, create the pull request | `issue-to-pr-release` | `pr.md` |

All subagents follow the rules in `rules/` and use the stage prompts in `prompts/`. Their roles and tool permissions are described in `agents/`.

## 5. Stop conditions

Stop the workflow and report the reason if:

- the input is invalid or the issue cannot be used (sections 1 and 2),
- the working tree is not clean before stage 1,
- a subagent is not available or does not produce its output file,
- stage 4 reports failure after 4 attempts.

Never leave the repository in a half-finished state without saying so: report the current branch, the last completed stage and what remains.

## 6. Final report

At the end, reply with a short summary:

- issue number and title,
- branch name and pull request URL,
- build and test result, including the number of repair attempts,
- path to the run folder with all stage outputs.
