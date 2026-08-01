---
name: refactor-sweep
description: How to run iterative code-consistency refactoring across GT4 - one production/test boundary per sweep, categories bundled into it, investigation before any edit. Use when asked for a refactor pass, a consistency cleanup, or before starting any multi-file refactor.
---

# Iterative refactoring in GT4

The goal: converge the codebase on one consistent pattern per concern, a little at a time, without ever risking a production regression and a test regression in the same change. Each individual edit still follows `surgical-change` — this skill governs what to work on and in what order.

## The unit of work is one boundary

A "pass" fixes one or more consistency-issue categories (e.g. naming scheme, DI pattern, collection type usage, error-handling idiom, member ordering) within exactly one boundary:

- **Production**: `Core/`, `UI/` (excluding `Tests/`), `Tools/` (excluding its `.Tests` project).
- **Test**: everything under `Tests/`.

Never mix boundaries in the same branch or PR — a production regression and a test regression must never be able to land in the same change. If fixing a production site surfaces a matching inconsistency in test code (or vice versa), file it as a separate issue for a future pass — don't fix it inline.

Categories, by contrast, are bundled by default: they're behavior-preserving and mechanical, so several in one PR stays reviewable as long as the commits keep them apart. The cost is that GT4 squash-merges, making a bundled sweep a single revert unit on `master`; that's acceptable for mechanical categories and is exactly why the scope-discipline rule below holds contestable ones back.

## Investigation stage (always first, always its own turn)

Before any edit:

1. Survey the target boundary for candidate inconsistency categories with Grep/Glob — don't rely on memory of the codebase, it drifts.
2. For each category found, determine: how many sites, which pattern(s) are competing, and which pattern should become canonical. Prefer whichever pattern already dominates the existing code, or the one most aligned with `surgical-change`'s "prefer the lighter mechanism" — don't invent a new pattern when one already wins.
3. Check `gh label list` for a `refactoring` label; create it with `gh label create refactoring --color <hex> --description "Code-consistency refactor, tracked by refactor-sweep"` if missing.
4. File one GitHub issue per category via `gh issue create --label refactoring`. Each issue states: the category, the target boundary (prod/test), the chosen canonical pattern with one concrete example site, and the full list of non-conforming sites (file:line).

Stop there. Don't start editing in the same turn as filing issues — the user should get to see the issue list before an agent spends edits on it.

## Selecting the sweep

Before executing, check `gh issue list --label refactoring` rather than re-surveying from scratch. Take every open issue for one boundary — that's the sweep — minus any the scope-discipline rule below holds back. If none are open, run the investigation stage first.

## Execution stage

For the chosen issues:

- One branch, `drafts/ai/<slug>`, cut fresh from `origin/master`; one PR that closes every issue in the sweep — list them all (`Closes #NNN`, `Closes #MMM`), or the unlisted ones stay open silently.
- Apply `surgical-change` for every edit: minimal diff, blend into surrounding style, no behavior change.
- Refactors are behavior-preserving by definition. If a listed site can't be converted without changing behavior, drop it from this pass and say so in the PR description rather than changing behavior silently.
- Commit granularity: one commit per file or per tightly-related group of sites, `(~)` prefix on each. Keep each category's commits contiguous and never interleave two categories — with several categories in one PR, the commit list is what keeps review tractable. Don't squash a whole category into one commit.
- Build and run the full affected test suite after the pass, before opening the PR. Since nothing should have changed behaviorally, existing tests are the regression check.

## Scope discipline

Bundle only categories with the same risk profile. A category whose canonical pattern is a judgment call — a naming scheme, say — goes in its own PR, so the user can reject it without blocking the mechanical ones (collection types, member ordering: already-settled conventions). Mixing a contestable style choice into a mechanical sweep makes the whole PR all-or-nothing.

If a sweep has too many sites for one reviewable PR, split it — by sub-area (e.g. per project) or by category, whichever yields the more coherent PRs. Still one boundary — just narrower.

## Cross-reference

For the mechanics of each individual edit — targeting, avoiding overengineering, blending in, verification, branch/commit conventions — see `surgical-change`. This skill does not duplicate that content.
