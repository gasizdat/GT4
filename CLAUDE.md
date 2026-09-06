# Working in GT4

Architecture and subsystem docs live in [doc/dev](doc/dev/30-navigation.md) — start there for "how
does X work" questions. This file is process, traps, and settled decisions that aren't derivable
from the code.

## Delivery workflow

- Branch from `origin/master` with `git switch --no-track -c drafts/ai/<slug> origin/master` — a
  draft branch must never track `origin/master` (avoids accidental fast-forward pushes to it).
- One item of work = one `drafts/ai/<slug>` branch = one PR. A refactor sweep across many call
  sites is the exception: bundle it into one branch/PR instead of one per site.
- Run `dotnet format whitespace GT4.sln` before opening a PR; gate on `git diff` being empty
  afterward (not `dotnet format --verify-no-changes`, which reports differently). Never hand-fix
  formatting.
- Never delete a `drafts/ai/*` branch, local or remote, even after it's merged — omit
  `--delete-branch` on `gh pr merge`. Likewise, don't clean up old `git stash` entries you find;
  they're kept deliberately as recovery points for abandoned or paused work.
- `doc/store` (Store submission material, screenshots, listing text) lives on `release/rc-sep-04`,
  not `master` — don't move it.
- Never `git push` or open/merge a PR without being asked first, even if a previous push in the
  same session was approved.

## Rejected designs — don't re-propose without new evidence

- **Commit-driven foreground flush for persistence durability** — reverted after landing; adds
  complexity `ProjectHost`'s existing origin/cache model doesn't need.
- **`GT4.UI.Logic`, a MAUI-free extraction of page logic** — rejected: pages are mostly boilerplate
  around calls, not logic worth isolating; the untested surface stays covered by device tests
  against the real page instead.
- **A blocking-operation wait cursor on Windows** — not deliverable: WinUI's `ProtectedCursor` is
  `protected`, and nothing can paint a cursor for a blocked main thread.
- **`PersonFilterView` input debounce** — built, works, deliberately not shipped (2026-08-30). If
  you find a "no debounce" comment near `FamilyInfoItem`, it's not an oversight.
- **`PhotoCache`/`DefaultImageCache`** — superseded by the generic `IImageCache` +
  `ImageDataWithMaxSize`; don't resurrect the old branch or design.
- **Numeral-per-disjunct in the greatness-computation text** (issue #318) — tried, reverted, then
  restored; both arguments for "numeral once" were re-litigated once already, see PR #326.

## Platform traps worth knowing before you hit them

- **Desktop `Window.Deactivated` fires on mere focus loss**, including while a native
  `FilePicker.PickAsync` dialog is up and before it returns — measured ~130ms window. Treating it
  like mobile backgrounding (close-and-reopen the project) causes churn and a latent race on every
  file pick. Desktop only flushes debounced settings there; only `Destroying` closes the project.
  See `UI/App/App.xaml.cs::CreateWindow`.
- **A `TargetType=Label` style assigned directly to a custom `ContentView`** crashes only in
  Release (`InvalidCastException` to `ITextElement`) — pass label styling through bindable
  properties on the `ContentView` instead.
- **MAUI gives a star (`*`) row the whole grid once any child in that row spans columns** — a
  `ColumnSpan` on one cell can silently swallow a footer row or unbound a body slot that looked
  fine before the span was added.
- **A `GraphicsView` used for connector/edge drawing has a ~16384px GPU max-texture ceiling** — a
  tall enough content area (e.g. a deep family tree) crashes it; draw edges as per-item vector
  `Path` shapes instead of one big canvas.
- **`SafeBindableLayout` can hit a native stowed-exception crash (`0xC000027B`)** if `Children` is
  mutated from inside `CollectionView`'s own recycle callback — defer the mutation to the next
  dispatcher tick.
- **`GT4.UI.App.DeviceTests` has its own separate `MauiProgram.cs`** — a handler/mapper
  registration fix in the main app must be duplicated there or device tests won't see it. Also:
  `App.InitializeComponent()` cannot be instantiated from that host (it loads app-level resource
  dictionaries the test host doesn't have), so lifecycle behavior gated on a real `App` instance is
  verified against the built binary, not a device test — that's a deliberate gap, not a missed one.
- **MAUI derives the MSIX package version from `ApplicationDisplayVersion`+`ApplicationVersion`**,
  not from `$(Version)` — the Store reserves version field 4 for its own use. `release.yml`'s
  signing-certificate subject must track the `Publisher` identity (still the stock template GUID).

## Don't re-derive stale snapshots

Issue-priority and open-work snapshots rot fast — don't trust a memory or doc that reads like one.
Pull live state instead: `gh issue list --label <label>`, `gh pr list --state open`.
