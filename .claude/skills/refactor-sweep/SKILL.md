---
name: refactor-sweep
description: How to run iterative code-consistency refactoring across GT4 - one production/test boundary per sweep, categories bundled into it, sites enumerated with the Roslyn code model (never grep) before any edit. Use when asked for a refactor pass, a consistency cleanup, or before starting any multi-file refactor.
---

# Iterative refactoring in GT4

The goal: converge the codebase on one consistent pattern per concern, a little at a time, without ever risking a production regression and a test regression in the same change. Each individual edit still follows `surgical-change` — this skill governs what to work on and in what order.

## The unit of work is one boundary

A "pass" fixes one or more consistency-issue categories (e.g. naming scheme, DI pattern, collection type usage, error-handling idiom, member ordering, comment hygiene) within exactly one boundary:

- **Production**: `Core/`, `UI/` (excluding `Tests/`), `Tools/` (excluding its `.Tests` project).
- **Test**: everything under `Tests/`.

Never mix boundaries in the same branch or PR — a production regression and a test regression must never be able to land in the same change. If fixing a production site surfaces a matching inconsistency in test code (or vice versa), file it as a separate issue for a future pass — don't fix it inline.

Categories, by contrast, are bundled by default: they're behavior-preserving and mechanical, so several in one PR stays reviewable as long as the commits keep them apart. The cost is that GT4 squash-merges, making a bundled sweep a single revert unit on `master`; that's acceptable for mechanical categories and is exactly why the scope-discipline rule below holds contestable ones back.

## Investigation stage (always first, always its own turn)

Before any edit:

1. Survey the target boundary for candidate inconsistency categories with Grep/Glob — don't rely on memory of the codebase, it drifts. Grep's job ends here: it proposes what to look at, it never produces the site list.
2. For each category found, determine: how many sites, which pattern(s) are competing, and which pattern should become canonical. Prefer whichever pattern already dominates the existing code, or the one most aligned with `surgical-change`'s "prefer the lighter mechanism" — don't invent a new pattern when one already wins.
3. Check `gh label list` for a `refactoring` label; create it with `gh label create refactoring --color <hex> --description "Code-consistency refactor, tracked by refactor-sweep"` if missing.
4. File one GitHub issue per category via `gh issue create --label refactoring`. Each issue states: the category, the target boundary (prod/test), the chosen canonical pattern with one concrete example site, and the full list of non-conforming sites (file:line) as produced by the code-model query, noting how many projects/documents it analyzed.

### Enumerate with the code model, never with grep

The site list for a category comes from Roslyn. A category is a *shape* — a return type, a member kind, a mutation — and grep matches tokens, so it silently misses every spelling it didn't anticipate. Concretely: #222's grep survey found 3 of the 16 real sites for "List/IReadOnlyList as an unmutated return type", missing tuple-embedded returns, `Task<(...)>`-wrapped returns, and a local function (#224, #225).

- Walk the **syntax** model for the declaration shape (`MethodDeclarationSyntax`, `LocalFunctionStatementSyntax`, `PropertyDeclarationSyntax`, …) so no spelling escapes. Expect it to over-enumerate — it surfaces shapes the category doesn't want, so triage the hits rather than assuming a query that returns more than you expected is broken. Use the **semantic** model wherever the rule depends on types or usage rather than syntax — "never mutated after it's returned" is a usage question and syntax alone cannot answer it.
- **Assert completeness before trusting the output.** Always report the document count actually analyzed. A syntax-only walk needs nothing more. Once the query uses the semantic model, also confirm there are no unresolved-symbol diagnostics: a compilation with unresolved references returns *fewer* sites and looks exactly like a clean run — the same failure as the grep it replaced, with better provenance.
- Getting the load right is the first step, not an afterthought. If opening the solution filter doesn't work, parse the boundary's `.cs` files and add `MetadataReference`s to the already-built `bin/Release/…` assemblies — same semantic model, no MSBuild dependency.
- `GT4.Core.Utils` multi-targets (`net10.0;net10.0-android`), so it loads as two projects and yields each document twice. Dedup the site list or pin one target framework.
- Comments are trivia, not nodes: `DescendantNodes()` finds none of them and reports a clean zero (measured — 0 across 174 files in `Core/`, where `DescendantTrivia()` finds 604). Sweeping comment hygiene means `DescendantTrivia()` filtered to `SingleLineCommentTrivia`, `MultiLineCommentTrivia`, `SingleLineDocumentationCommentTrivia`, `MultiLineDocumentationCommentTrivia`. Classify per `comments-sweep` (history / narration / claim / contract / rationale); only the last two survive.
- Run the query from the scratchpad, not the repo — it's throwaway, and nothing about it belongs in the product tree.
- Where a built-in IDE analyzer already covers the category, enable it and read the build diagnostics instead: that enumerates *and* stops the inconsistency coming back.

Stop after filing the issues. Don't start editing in the same turn — the user should get to see the issue list before an agent spends edits on it.

## Selecting the sweep

Before executing, check `gh issue list --label refactoring` rather than re-surveying from scratch. Take every open issue for one boundary — that's the sweep — minus any the scope-discipline rule below holds back. If none are open, run the investigation stage first.

## Execution stage

For the chosen issues:

- One branch, `drafts/ai/<slug>`, cut fresh from `origin/master`; one PR that closes every issue in the sweep — list them all (`Closes #NNN`, `Closes #MMM`), or the unlisted ones stay open silently.
- Apply `surgical-change` for every edit: minimal diff, blend into surrounding style, no behavior change.
- Refactors are behavior-preserving by definition. If a listed site can't be converted without changing behavior, drop it from this pass and say so in the PR description rather than changing behavior silently.
- Commit granularity: one commit per file or per tightly-related group of sites, `(~)` prefix on each. Keep each category's commits contiguous and never interleave two categories — with several categories in one PR, the commit list is what keeps review tractable. Don't squash a whole category into one commit.
- Build and run the full affected test suite after the pass, before opening the PR. Since nothing should have changed behaviorally, existing tests are the regression check.
- Re-run the enumeration query after the pass. It should return nothing but the sites you deliberately dropped — anything else means a site was edited into a different spelling rather than converted.

## Scope discipline

Bundle only categories with the same risk profile. A category whose canonical pattern is a judgment call — a naming scheme, say — goes in its own PR, so the user can reject it without blocking the mechanical ones (collection types, member ordering: already-settled conventions). Mixing a contestable style choice into a mechanical sweep makes the whole PR all-or-nothing.

If a sweep has too many sites for one reviewable PR, split it — by sub-area (e.g. per project) or by category, whichever yields the more coherent PRs. Still one boundary — just narrower.

## Cross-reference

For the mechanics of each individual edit — targeting, avoiding overengineering, blending in, verification, branch/commit conventions — see `surgical-change`. This skill does not duplicate that content.
