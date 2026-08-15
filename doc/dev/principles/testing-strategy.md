# Principle: what each test tier is for

GT4's tests split by what they need to be true to run, not by which project they cover. That
split is the thing worth understanding before adding a test — putting a test in the wrong tier
either makes it unable to prove what it claims, or makes it needlessly slow/fragile. The live list
of test projects is `GT4.sln` and the tiering into build heads is `GT4.CI.slnf` /
`GT4.Windows.slnf` / `GT4.Android.slnf` (root of the repo) — this doc explains the *why* behind
that split, not a project-by-project inventory that will drift as projects are added.

## The tiers

**Pure unit tests** (`Tests/GT4.Core.Project.Tests`, `Tests/GT4.Core.Gedcom.Tests`,
`Tests/GT4.Tools.RelativesCli.Tests`) — no MAUI, no device, no UI thread. These run anywhere
`dotnet test` runs, including plain CI, and are the right tier for anything that's really about
data or logic: SQLite persistence semantics, GEDCOM parsing/export, CLI behavior.

**View-layer unit tests** (`Tests/GT4.UI.View.Tests`, `Tests/GT4.UI.Utils.Tests`) — formatters,
comparers, link-parsing utilities like `MarkdownLinkUtilsTests`, `SafeTaskTests`: MAUI types are
referenced but nothing needs a real device or a rendered page. Fast, and the right tier for logic
that happens to live in the `UI.*` namespaces but doesn't need a page host to exercise.

**Device tests** (`Tests/GT4.UI.App.DeviceTests`) — everything that needs an actual page,
dialog, or rendered component: navigation, bindings resolving through the real DI container,
`MarkdownView` actually drawing something. Built on `DeviceRunners` + `xunit.v3`
(`GT4.UI.App.DeviceTests.csproj`), Release-only, and requires `-p:SolutionDir=` — this is the
in-process MAUI device-test harness, not a UI-automation tool driving a separately-running app.
This is also the tier that runs on Windows headlessly (`GT4.Windows.slnf` includes it) and can run
on a physical Android device — see `run-android-device-tests.ps1` at the repo root.

**Functional round-trip tests** (`Tests/Functional/run-gedcom-roundtrip.ps1`) — real GEDCOM files
carried `ged → GT4 project → ged`, checked two ways: **stability** (does a second round-trip
produce byte-identical output to the first) and **fidelity** (does the export still say what the
original said, compared tag-by-tag through `RelativesCli compare` rather than literally, since
xrefs and `FAM` records regenerate on every export). Samples live in a sibling repository the
script skips gracefully when absent. This tier exists because unit-level GEDCOM tests can each
pass while the pipeline still loses something no single unit test's fixture happened to exercise —
only a real file end-to-end catches that class of regression.

## Why the split matters when adding a test

**Match the tier to what the test needs to be true, not to where the code under test lives.** A
converter's parsing logic belongs in a unit test even though the converter is `UI.App` code; the
same converter's behavior when wired through the real `DataConverterResolver` and a rendered
`MarkdownView` belongs in a device test. Writing the second kind where the first would do makes
the suite slower for no added confidence; writing the first kind where the real wiring is the
point (see [dependency-injection.md](dependency-injection.md)) leaves the wiring itself unverified.

**Pin prevented regressions, not just new behavior**, including assertions that something must
*not* happen (`PersonPageTests.AttachmentLinkTapped_with_a_dangling_id_is_inert` is the running
example — a genuinely deleted id must stay a silent no-op).

**Run the canonical entry point, not an ad-hoc subset**: `run-tests-coverage.ps1` (repo root) runs
every unit/view/device test project plus the GEDCOM round-trip, then produces a combined coverage
report and pass/fail summary. Affected test classes first while iterating, full suite before
calling anything done.

## Non-goals

- Not a how-to-run reference — `run-tests-coverage.ps1`, `run-android-device-tests.ps1`, and the
  `surgical-change` skill already own the exact commands.
- Not a project-by-project test inventory — `GT4.sln` and the three `.slnf` files are the live
  source of truth for which projects exist and which build head runs them.
