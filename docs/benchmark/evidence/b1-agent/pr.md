# Pull request: #54 [Benchmark] Add user identification to tasks and "My Tasks" filtering

Status: done
Publication: pull request created
Base: benchmark/base-t3-10
Head: feat/t3-54-user-task-filtering
Remote branch: origin/feat/t3-54-user-task-filtering at b2a22ab
Pull request created: yes
URL: https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-3/pull/57

## Commits

| Commit | Message |
|---|---|
| `b3cb0cd` | `feat(#54): Add userId to todos and filter GET /api/todos by user` |
| `0168c87` | `test(#54): Cover userId tagging and user filtering` |
| `146940c` | `test(#54): Align userId validation test with the controller content type` |
| `b2a22ab` | `docs(#54): Document userId field and user filter in OpenAPI.md and READMEs` |

`146940c` and `b2a22ab` were created in this stage; the first two come from stage 3. No trailers were added, per `rules/git-conventions.md`.

## Checks

- `qa-report.md`: `Status: passed` (115/115 tests, build succeeded); `docs.md`: `Status: done`.
- Working tree before commit: only the four expected files (1 QA repair, 3 docs), all listed by stages 4 and 5.
- `git diff --cached --check` passed before both commits; `git status --porcelain` empty and `git diff --check` passed afterwards.
- `git diff --check origin/benchmark/base-t3-10...HEAD` passed.
- `gh auth status` succeeded.
- Push: `* [new branch] feat/t3-54-user-task-filtering -> feat/t3-54-user-task-filtering`, upstream set; `git ls-remote` shows `b2a22ab`, same as local HEAD.
- `gh pr create` printed the URL above and exited 0. The pull request was not merged.

## Failed step

None
