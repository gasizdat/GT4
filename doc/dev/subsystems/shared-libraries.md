# Subsystem: shared libraries

`Core.Utils`, `UI.Utils`, and `UI.Resources` don't each carry their own feature set — the reason
they're three separate projects instead of one is a dependency-direction rule, verifiable directly
in their `.csproj` files, not a functional split worth documenting project-by-project.

## The rule: MAUI-free layers stay MAUI-free, even where they need a platform API

`Core.Project.csproj` (`Core/Project/GT4.Core.Project.csproj`) states it outright in its own
comment: *"Platform-agnostic domain/persistence library: no MAUI, no UI, no platform APIs. Targets
plain net10.0 so it stays the most reusable and testable layer."* `Core.Utils`
(`Core/Utils/GT4.Core.Utils.csproj`) is the layer underneath it and follows the same rule with one
addition: it multi-targets `net10.0` plus the platform TFMs (`net10.0-android`, `-ios`,
`-maccatalyst`, a Windows TFM) so it can reach a platform-specific API — `AndroidFileSystem.cs`
reads from Android's `MediaStore` — **without ever referencing `Microsoft.Maui.Controls`**. That's
what makes it possible for `Tools.RelativesCli` (a plain `net10.0` console app,
`Tools/GT4.Tools.RelativesCli/GT4.Tools.RelativesCli.csproj`) to reference `Core.Project` and
`Core.Gedcom` directly and run with no MAUI runtime at all — see
[tools-relativescli.md](tools-relativescli.md).

`UI.Utils` (`UI/Utils/GT4.UI.Utils.csproj`) is the opposite side of the line: it sets
`<UseMaui>true</UseMaui>` and references `Microsoft.Maui.Controls` deliberately, because its job is
shared code that *does* need MAUI types (`ImageSource`, `Color`, the converters and formatters
`UI.App`'s pages and dialogs bind against). It's still not `UI.App` — no page, dialog, or
navigation reference lives here — but it's allowed to know MAUI exists, where `Core.Utils` isn't.

`UI.Resources` (`UI/Resources/GT4.UI.Resources.csproj`) is narrower still: localized strings
(`UIStrings.resx`/`UIStrings.ru.resx`) and nothing else. It exists as its own project so adding a
translation doesn't require rebuilding logic, not because it enforces a dependency boundary.

## What this buys, concretely

- `Tools.RelativesCli`'s diagnostic value (comparing relative trees, GEDCOM round-trip checking —
  see [core-gedcom.md](core-gedcom.md)) depends on being runnable with no MAUI app to launch. That
  only works because everything it needs (`Core.Project`, `Core.Gedcom`, and transitively
  `Core.Utils`) is MAUI-free by construction, not by convention someone has to remember.
- A test project for domain/persistence logic (`GT4.Core.Project.Tests`, `GT4.Core.Gedcom.Tests`)
  runs as a plain unit-test project — no device runner needed — for the same reason. See
  [principles/testing-strategy.md](../principles/testing-strategy.md).

## Non-goals

- Not a rule that Core.Utils must avoid *all* platform-specific code — it's the designated place
  for platform API access below the MAUI layer, `AndroidFileSystem.cs` being the existing example.
  The rule is narrower: platform-specific is fine, MAUI-specific isn't.
- Not a catalog of what's currently in each project — `Core/Utils/*.cs`, `UI/Utils/*.cs` are the
  live lists.
