---
name: comments-sweep
description: How to decide which comments survive in GT4 - classify each one, delete history and narration, pin behavioral claims with tests, keep only durable rationale. Use when cleaning up the comments in a change you just made, or running a comment-hygiene sweep over a boundary.
---

# Comment hygiene in GT4

Keep only comments carrying information that the code, its names, its types, and its tests cannot. The goal is fewer *and* shorter, not zero.

## Two modes, different scope

Comment cleanup happens one of two ways, and conflating them is how a small change becomes an unbounded rewrite:

- **At the end of a change** (`surgical-change`): scratch comments are fine *while* you work — memory knots holding the shape of a half-built change — but they don't ship. Sweep them before the PR, scoped to the code you actually touched. Don't widen into neighbouring comments that caught your eye; note them as a future pass.
- **As a sweep** (`refactor-sweep`): comment hygiene is a consistency category like any other — filed as an issue, worked as a deliberate pass over one boundary, bounded by that skill's scope discipline. Sites come from the code model, and comments are trivia rather than nodes.

## Classify, then act

Every comment is one of five:

- **History** — "changed X to Y", "added for #NNN", progress notes. Delete. Git log already says this, and says it accurately.
- **Narration** — restates the line, or walks control flow that is already plain. Make the code say it instead: rename, extract a named predicate, replace a magic value or boolean flag with an explicit domain concept. Then drop the comment. Don't extract something vague just to delete a comment — that relocates the confusion rather than removing it, and single-use logic doesn't want a helper.
- **Behavioral claim** — an invariant, an edge case, a "this would silently break if". Pin it with a test, then shrink the comment to a one-line statement of the invariant.
- **Contract** — public API docs, framework-required, generated, legal notices. Keep. Improve if inaccurate, but preserve what it promises.
- **Rationale** — why this is necessary, what constraint forbids something simpler, what would break if it changed. Before keeping it, check whether a more specific name already carries the constraint — a cache named for its key space needs no note about its bounds. If not, keep it, rewritten to answer that question directly: an external API/platform constraint, a required ordering, a workaround for someone else's defect, a deliberate trade-off.

**The discriminator:** delete the comment and re-read the code. If a reader who can read identifiers and test names loses nothing, it was never load-bearing. Verbose *why* is still boilerplate — three lines justifying two lines of code usually wants to be one clause, or nothing.

## Behavioral claims belong in tests

A comment must never be the only place a required behavior is recorded. Prose rots silently: the code changes, nobody updates the comment, and it now misleads both humans and agents.

- **Never leave a pointer** (`// see XyzTests`). It rots exactly the same way — rename the test or move the file and the comment lies. The durable form is the invariant itself, one line, with no names that can go stale.
- **Don't manufacture a test for a claim already covered transitively** — a caller's "throws for X" when the callee it delegates to already tests that throw. Trim the comment and skip the test.
- **Verify a new test actually discriminates the claim.** Break the behavior deliberately and confirm the test fails. A `PersonFilterView` ordering comment ("Maximum set before Minimum") looked testable, but both assignments run synchronously with nothing observing the intermediate state, so an end-state assertion passed even with the source order reversed — the test proved nothing while appearing to pin the claim. When a test can't discriminate, say so in the PR rather than implying coverage that isn't there.

## Cross-reference

For the mechanics of each edit — targeting, avoiding overengineering, blending in, verification — see `surgical-change`. For running this as a category over a boundary, see `refactor-sweep`. Writing *new* doc comments is governed by `surgical-change`'s "Blend in" rules, not here.
