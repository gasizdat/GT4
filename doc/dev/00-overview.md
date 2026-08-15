# GT4 — system overview

GT4 is a genealogy application: it holds a family tree — people, their names, relationships,
photos, attachments, and biographies — in a local project file, and can import/export that data as
GEDCOM 5.5.1, the standard interchange format genealogy tools use to exchange family trees.

This is the entry point into `doc/dev`. It answers "what is this and how is it shaped"; it
deliberately doesn't name a class or invariant that would still need explaining a level down — see
[subsystems/](subsystems/) for that.

## Who this is for

A developer or AI agent about to make a change and needing to find the right seam quickly, without
first reading the whole codebase. If you already know which subsystem you're touching, skip ahead
to [subsystems/](subsystems/) directly, or to [30-navigation.md](30-navigation.md) if you know the
*task* but not which subsystem it lands in.

## The shape of the system

**One project = one local SQLite file.** A user opens, edits, and closes one project at a time.
The file on disk during editing is a cache copy; the original is only overwritten when the edit
session closes with real changes to flush. See
[subsystems/core-project.md](subsystems/core-project.md).

**GEDCOM is the interchange format, not the storage format.** Importing a `.ged` file merges it
into a project (or creates a new one); exporting produces a `.ged` from the project. GT4's own
schema is a deliberately small subset of what GEDCOM can express, so import/export is built around
preserving whatever isn't natively modeled rather than silently dropping it on a round-trip. See
[subsystems/core-gedcom.md](subsystems/core-gedcom.md).

**One MAUI application, several build heads.** The same `UI.App` project builds for Windows
(`AppWinOnly.csproj`) and Android (`AppAndroidLib.csproj`/`AppAndroidOnly.csproj`); `App.csproj`
itself also multi-targets iOS/macCatalyst TFMs, but none of the `.slnf` build-head filters include
them, so they sit outside this repo's day-to-day dev/CI loop. Below the app, `Core.Project` and
`Core.Gedcom` are plain .NET libraries with no MAUI dependency at all,
which is also what lets the diagnostic CLI tool run without a MAUI runtime. See
[subsystems/ui-app.md](subsystems/ui-app.md) and
[subsystems/shared-libraries.md](subsystems/shared-libraries.md).

**The live, authoritative list of what exists is `GT4.sln`, not this document.** Run
`dotnet sln GT4.sln list`, or read `GT4.CI.slnf` / `GT4.Windows.slnf` / `GT4.Android.slnf` at the
repo root to see exactly which projects build for which head — those filters are what CI and the
Windows/Android dev loops actually run, and they will be more current than any prose list here.

## How the pieces fit together

The project-reference graph, read directly from each `.csproj` (`Core.Utils` has no project
references and sits at the bottom):

- `Core.Project` → `Core.Utils`
- `Core.Gedcom` → `Core.Project`, `Core.Utils`
- `Tools.RelativesCli` → `Core.Project`, `Core.Gedcom`
- `UI.Utils` → `Core.Project`, `UI.Resources`
- `UI.App` → `UI.Resources`, `UI.Utils`, `Core.Utils`, `Core.Project`, `Core.Gedcom` (all five,
  referenced directly rather than only reached transitively — see `UI/App/AppCommon.props`)

Dependencies only point down this list, never up. The MAUI-free layers (`Core.Utils`,
`Core.Project`, `Core.Gedcom`) staying MAUI-free is a maintained boundary, not an accident — see
[subsystems/shared-libraries.md](subsystems/shared-libraries.md) for what depends on it.

## Where to go next

- **A specific subsystem to work in** → [subsystems/](subsystems/) — one file per subsystem,
  covering its owned invariants.
- **A rule that applies across subsystems** (dependency injection, async safety, rendering
  untrusted content, how tests are tiered) → [principles/](principles/).
- **A task in hand, not yet sure which doc it needs** → [30-navigation.md](30-navigation.md).
- **How to actually make and verify a change** (commit conventions, build/test commands, the
  surgical-change discipline) → the repo's own `.claude/skills/*` and `CLAUDE.md`, which this
  document set intentionally does not duplicate.
