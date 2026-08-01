---
name: surgical-change
description: How to implement a feature or fix in GT4 with minimal impact - accurate targeting, no excess code, no overengineering. Use before writing any change to product source in this repo.
---

# Surgical changes in GT4

The goal of every change: smallest diff that fully solves the problem, placed at the true source of the behavior, indistinguishable in style from the surrounding code.

## Target before you type

- Read the exact code path end-to-end first (UI → manager → table). Fix where the behavior originates, not where the symptom shows up.
- Before inventing anything, search for existing machinery that already does 80% of it. Prefer wiring into it over building parallel structure.
  - Example: issue #96 needed "persons without a family" surfaced. Right answer: a sentinel `Name` (`FamilyInfoItem.NoFamilyName`, Id 0 — SQLite rowids start at 1) flowing through the existing `FamilyInfoItem` card, Shell navigation, and `FamilyPage` — not a new page, not a subclass, not a new DB table.

## Prefer the lighter mechanism

Each of these beats its heavier alternative unless proven insufficient:

- A sentinel/default value over a new type or subclass.
- An in-memory LINQ filter over new SQL/manager API surface, when the data is already loaded (`persons.Where(FamilyInfoItem.HasNoFamily)` vs a new `NOT EXISTS` query).
- A declarative XAML binding over imperative UI manipulation in code-behind (`IsEnabled="{Binding EnableFamilyChanges}"` on a ToolbarItem beats removing items from `ToolbarItems`; the binding is also reversible when state changes back).
- Extending an existing method's behavior over adding a parallel method.
- One shared helper when the same rule is needed in two places (`FamilyInfoItem.HasNoFamily` used by both ProjectPage and FamilyPage keeps the card and the listing in agreement) — but don't extract helpers for single-use logic.

## No overengineering

- Guard only states that can actually occur. No defensive null-checks, try/catch, or fallbacks "just in case".
- Don't add configuration, abstractions, interfaces, or extension points for hypothetical future needs.
- Hide/skip empty states instead of designing UI for them (an empty "No family" card is noise — only append it when non-empty).
- If a change exposes an adjacent latent bug, fix it only when the fix is a line or two on the same seam (PersonPage passing null `FamilyName` → fall back to the sentinel); otherwise mention it or file an issue.

## Blend in

- Match the file's member grouping (fields → ctor → public members → private methods); never interleave access levels.
- Follow user conventions from memory: minimalist code, no nested call expressions, LINQ/lazy + raw arrays, commit prefixes (*)/(+)/(~)/(-)/(=).
- Comments only for non-obvious invariants (why Id 0 is collision-free), never narrating what the code does. Scratch comments are fine *while* you work — they're memory knots holding the shape of a half-built change — but they don't ship. Sweep them before the PR (see `comments-sweep`).
- Before writing a doc comment, check whether the signature already says it: `static` already says "no instance state needed"; a descriptive method/param name already says what it does. If the comment would just restate that in prose, skip it. A strong tell: if you're about to write near-identical doc comments on two or three sibling methods (e.g. the same "stateless, so callers don't need an instance" paragraph on `DateFormatter.Format`/`DateSpanFormatter.Format`/`NameFormatter.Format`, removed in PR #136), that's boilerplate, not a real per-method invariant — cut it everywhere, or say it once where it's actually non-obvious.

## Verify like CI does

- Tests: extend the existing test class for the touched page/component, reusing its harness helpers (`ReloadPersonsAsync`, `WaitForFamiliesAsync`, `ModalDialogHarness`) and mirroring the style of neighboring tests. Pin each new behavior and each prevented regression (e.g. a `Times.Never()` on the call that must not happen).
- Comments: sweep the ones you added, scoped to the code you touched — classify and act per `comments-sweep`.
- Build: `dotnet build GT4.CI.slnf -c Release` (device tests refuse Debug).
- Run the affected device-test classes first (`--filter "FullyQualifiedName~<Page>Tests"`), then the full suite: `dotnet test Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj --no-build -c Release --framework net10.0-windows10.0.19041.0`. A nonzero exit with "Failed: 0" is a known flaky runner teardown, not a failure.
- Branch-per-item under `drafts/ai/<slug>` (a `refactor-sweep` sweep counts as one item), always cut from `origin/master` (`git fetch` first — local `master` goes stale) and never stacked on an unmerged branch; if a change genuinely cannot sit on `master`, discuss it with the user before branching. One focused commit per logical change; never push without being asked.
