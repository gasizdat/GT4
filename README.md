# tech-debts/collectionview-page-swap-leak

Parking branch for an abandoned investigation into a UI memory leak. **No fix landed.**
This branch exists to preserve the repro tests and findings for whoever picks this up next.
It is not intended to be merged into `master`.

## The reported symptom

Opening a large GT4 project (e.g. `WilliamLongsword`, ~1500 families / ~12,000 people) and
scrolling causes the process's memory footprint to keep growing. Switching to a smaller
project and back does not return memory to the system. The in-memory person-family model
itself was suspected clean (no DB transactions during rendering, and this particular project
has no user photos — everyone renders the same cached default stub image), which the
investigation confirmed: `PersonInfo` DTO instance counts stayed flat across gcdump snapshots.
This is a UI-realization-layer leak, not a data-layer one.

## What was ruled out

- **`CardView`'s `GraphicsView` shadow as the primary cause** (a co-pilot's initial theory).
  `CardViewSwitchIsolationTests` removed `CardView` from the template entirely — the leak
  still compounded just as strongly. Not the main driver (see "one real outlier" below,
  though — it's not nothing either).
- **`CollectionView` itself.** `CollectionViewSwitchIsolationTests` swaps `ItemsSource` between
  large/small arrays with a bare `Label` template (no `SafeBindableLayout`, no `CardView`) —
  flat, no compounding.
- **Production's actual data-refresh path.** `ProductionRefreshIsolationTests` mirrors
  `ProjectPage`'s real refresh mechanism — one stable `ObservableCollection` instance, mutated
  via `Clear()` + `Add()`, `ItemsSource` never reassigned (matches `ViewUtils.RefreshView` and
  `EnsureFamiliesLoaded`) — flat, no compounding. So the leak is NOT simply "refreshing the
  family list leaks."

## What was confirmed

1. **`SafeBindableLayoutSwitchIsolationTests`** — reassigning the *outer* `CollectionView`'s
   `ItemsSource` to a different collection instance repeatedly compounds, even with a plain
   `Label` item template (no `PersonInfoView` involved). This isolates a real leak mechanism to
   `SafeBindableLayout`'s rebuild-on-`ItemsSource`-change path, independent of any specific item
   view.

2. **`PageSwitchIsolationTests`** — the closest match to the actual bug report. Two full
   `ContentPage`s (real `CardView` / `SafeBindableLayout` / `PersonInfoView` family-card
   template, matching `ProjectPage.xaml`) are repeatedly swapped in as `Window.Page`, mirroring
   what `Window.OnPageChanged` does for real project-switch navigation. Result: **strong,
   reliable compounding** — working set 293→607 MB, managed heap 33→94 MB over just 6 swap
   cycles at 1500-family scale. This is the strongest repro obtained and the one future work
   should build on.

## gcdump findings (from `PageSwitchIsolationTests`)

Two `dotnet-gcdump` snapshots were taken mid-run (early cycle vs. later cycle) and diffed by
type instance count:

- **General ~25–30% growth**, consistent with entire realized `CollectionView` cells from the
  *discarded* page staying reachable after `Window.Page` is reassigned, instead of being
  released: `PersonInfoView` 415→532, `Label` 855→1107, `Binding` 3809→4892, `AppThemeBinding`
  3022→3877, `SafeBindableLayout` 65→77, `FlexLayoutManager` 65→77. Framework-internal
  `WeakReference<BindableObject>` bookkeeping grew 15,695→20,234 alongside this — the tracked
  live-object set is genuinely growing, not just heap fragmentation.

- **One real outlier**: `CardView`, `GraphicsView`, `GraphicsViewHandler`, `W2DGraphicsView`
  (Win2D native), `PlatformTouchGraphicsView`, and `CardShadowDrawable` all roughly **tripled**
  (9→27, 16→27) — about 3x the growth rate of everything else. The co-pilot's shadow-surface
  theory isn't the *primary* driver, but it does look like a real, smaller, independent leak
  worth fixing on its own merits (native graphics resources are more expensive per instance
  than a `Label`).

- **`ComWrappers`/WinRT interop bookkeeping** (`ComWrappers+NativeObjectWrapper`,
  `ManagedObjectWrapperHolder`, and their backing `ConditionalWeakTable`/`Container`
  structures) also grew substantially, scaling with realized-cell count — consistent with
  native handlers from the old page's cells not being released either, not just their managed
  wrappers.

- **`PersonInfo` DTO count was flat** (12,003→12,003 both snapshots) — confirms the data model
  is not the leak; it's purely realized-view accumulation.

Raw reports (`gt4_page_report_early.txt` / `gt4_page_report_late.txt`) and the `.gcdump` files
themselves were left in `%TEMP%` and were not committed here (large binary artifacts, easy to
regenerate — see "Reproducing" below).

## The one fix attempt (reverted)

Added a `SafeBindableLayout.OnHandlerChanging` override to proactively unsubscribe from the
source collection and tear down children when the layout's own native handler is disconnected.
This required several follow-up fixes for crashes it introduced (a stowed-exception race
against `WindowHost`'s own page teardown, a regression in a "not yet attached" construction
path). After all of those were fixed, the full 42-test regression suite passed — but a
diagnostic counter added to `RemoveChildren` proved `OnHandlerChanging` **never fires at all**
in either `SafeBindableLayoutSwitchIsolationTests` or `PageSwitchIsolationTests`. The whole
mechanism was inert; re-running the repro after the "fix" showed identical compounding growth.

**Reverted in full** via `git checkout -- UI/App/Components/SafeBindableLayout.cs`.
`SafeBindableLayout.cs` on this branch is byte-identical to its state on `pre-master` — no
source changes are included here, only new test files and this README.

## Why it wasn't root-caused

The retaining reference isn't visible from `SafeBindableLayout`'s own lifecycle hooks, which
means it's rooted somewhere further up the chain — plausibly in how WinUI's `ItemsRepeater` (or
MAUI's window/handler-mapper plumbing) releases a realized page's cell graph when `Window.Page`
is swapped wholesale, as opposed to when a single layout is removed from within a still-live
page. Pinning that down precisely needs object-graph "path to root" analysis (e.g. PerfView's
GUI), not just `dotnet-gcdump`'s flat type-count report — that tooling wasn't available in this
session.

## Unverified guesses for a future attempt

1. Check whether the platform's page-hosting code (`Window.OnPageChanged` / whatever backs it
   on WinUI) explicitly disposes the outgoing page's handler tree, or just unparents it from the
   visual tree and assumes GC will collect it. If nothing else roots it, GC should still collect
   it — so if it doesn't, something concrete is holding a reference, not just "GC hasn't gotten
   around to it yet."
2. The `CardView`/`GraphicsView` outlier deserves its own narrower repro (isolate `CardView`
   alone, no `SafeBindableLayout`) — worth checking whether `GraphicsViewHandler` correctly
   releases `PlatformTouchGraphicsView`/`W2DGraphicsView` native resources on disconnect.
3. If project-switch navigation could instead update one long-lived `ProjectPage` instance's
   data in place (mirroring what `ProductionRefreshIsolationTests` shows to be leak-free)
   rather than constructing a brand-new `ProjectPage` and swapping `Window.Page` per switch,
   that might sidestep the whole class of leak rather than fixing it at the `SafeBindableLayout`
   level. Depends on how project switching is actually wired at the Shell/navigation layer,
   which wasn't investigated here.

## Files on this branch

All under `Tests/GT4.UI.App.DeviceTests/Tests/Components/`:

- `MemoryGrowthReproTests.cs` — first, broadest repro (scroll + repeated project-switch cycles
  against the full real template), with a 1.5 GB working-set safety abort. Superseded by
  `PageSwitchIsolationTests` as the most production-representative repro, kept for reference.
- `CollectionViewSwitchIsolationTests.cs` — bare `CollectionView` + `Label`, rules out
  `CollectionView` itself.
- `CardViewSwitchIsolationTests.cs` — real template minus `CardView`, rules out the
  `GraphicsView` shadow as the *primary* cause.
- `SafeBindableLayoutSwitchIsolationTests.cs` — isolates the `SafeBindableLayout` +
  outer-`ItemsSource`-reassignment leak mechanism down to a plain `Label` item template.
- `ProductionRefreshIsolationTests.cs` — mirrors `ProjectPage`'s real `Clear()`+`Add()` refresh;
  proves that mechanism is leak-free.
- `PageSwitchIsolationTests.cs` — the main repro: full `Window.Page` swap between a large and
  small project's page, matching real navigation. **Start here.**

All of these use `Assert.Fail(report)` to surface working-set/managed-heap sample sequences in
the test output rather than a proper assertion — they're diagnostic scaffolding, not
regression tests, and were never wired into CI.

## Reproducing / capturing new gcdumps

Build and run (Release only — Debug is hard-blocked for this project, see the .csproj):

```
dotnet test Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj -c Release -f net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~PageSwitchIsolationTests"
```

`PageSwitchIsolationTests` (and `SafeBindableLayoutSwitchIsolationTests`) write the running
test process's PID to `%TEMP%\gt4_repro_pid.txt` and, if `GT4_GCDUMP_PAUSE=1` is set in the
environment, pause for ~15s partway through the cycle loop — giving a window to run
`dotnet-gcdump collect -p <pid>` against the paused process. Take one snapshot early and one
late in the run and diff with `dotnet-gcdump report <file> > report.txt`, then compare type
instance counts between the two reports.

## Constraints observed throughout this investigation

Per the original ask: nothing was committed/pushed until this shelving, no source file changes
remain on this branch (`SafeBindableLayout.cs` matches `pre-master`), and the full existing
`GT4.UI.App.DeviceTests` regression suite (`PersonInfoViewLeakTests`, `ScrollCrashReproTests`,
`FamilyInfoItemTests`, `ProjectPageTests` — 42 tests) was green at the point this was shelved.
