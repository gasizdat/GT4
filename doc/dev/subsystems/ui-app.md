# Subsystem: UI.App

`UI/App` (`App.csproj`, plus the platform heads `AppWinOnly.csproj`, `AppAndroidLib.csproj`,
`AppAndroidOnly.csproj`) is the MAUI Shell application: pages, dialogs, components, and the
converters that sit between stored data and what a page displays. It depends on `Core.Project` and
`Core.Gedcom` for domain logic and persistence, and on `UI.Utils` (see
[shared-libraries.md](shared-libraries.md)) for MAUI-adjacent shared code — never the reverse.

## Construction: pages vs. dialogs

MAUI Shell constructs every page in `UI/App/Pages/*.xaml.cs` once, through DI, so a page declares
its real dependencies as typed constructor parameters (see
[principles/dependency-injection.md](../principles/dependency-injection.md)). A dialog under
`UI/App/Dialogs/**` is different: it's `new`'d up by a caller at the moment a runtime value (which
person, which biological sex) becomes known, which DI can't supply. Dialogs that need DI-resolved
services take a DI-constructed `Factory` record instead — `SelectNameDialog.Factory`,
`CreateOrUpdatePersonDialog.Factory`, and siblings under `UI/App/Dialogs/**` all follow this
shape; see the DI doc for why the split exists and when a dialog doesn't need the pattern at all.

## Navigation: MAUI Shell routes, no push-refresh service

Navigation goes through `INavigationService.GoToAsync(route)` (`UI/Utils/Abstraction/INavigationService.cs`),
a thin wrapper over MAUI Shell routing — not a custom navigation stack. There is deliberately no
service that pushes a "the project changed" notification to open pages. Instead, `ProjectInfo`
carries the project's revision counter, and a page snapshots it when it loads its own data
(`_LastProjectInfo` in `ProjectPage.xaml.cs`, `PersonPage.xaml.cs`, and every other page with
sub-navigation to catch a return from — a pure leaf page with nothing to navigate to, like
`NamesPage`, has no stale-on-return path and doesn't need it); `OnNavigatedTo`
compares the snapshot against `ICurrentProjectProvider.Info` and re-fetches only when they differ.
**This means a page catches "something changed while I was on a subpage that just committed an
edit," not "something changed right now while I'm sitting on this page"** — an earlier live-monitor
design (`IProjectRevisionMonitor`) was deleted in favor of this simpler compare-on-return pattern.
A dialog that must reflect a change immediately (not just on next navigation) still does its own
explicit `Refresh()` — the revision compare is a navigation-time check, not a live subscription.

## Converters own the storage ↔ view-state boundary

`IDataConverter` (see [principles/dependency-injection.md](../principles/dependency-injection.md)
for how converters are registered and resolved) is used two ways, both worth knowing:

- **Direct display**: a page or component asks the resolver for the category it's displaying and
  converts straight to a `PhotoInfo`/`AttachmentInfo`, e.g. `ImageDataConverter`
  (`UI/Utils/Converters/ImageDataConverter.cs`) turning a stored `Data` row into an `ImageSource`
  through `IImageCache` — caching and downsizing happen together there, so a cached entry is
  always the already-downsized one, never a mismatch a caller has to remember to avoid.
- **Pulled through a resolver callback**: `MarkdownView` never calls a converter itself — it asks
  an injected `InlineMediaResolver`, and `InlineMediaProvider` is what calls the converter behind
  that callback. See [principles/rendering-inversion.md](../principles/rendering-inversion.md) for
  why that indirection exists; it is not the default way to use a converter, only what a
  rendering component with untrusted/variable content needs.

**For display, a page or component binds a `PhotoInfo`/`AttachmentInfo` — an `ImageSource` plus
metadata — never a `byte[]` pulled out of a `Data` row for its own use.** A `Data` row's `Content`
still travels as far as the converter boundary (it's what `ImageDataConverter.ToObjectAsync` reads
to build the `ImageSource`, and what `PersonFullInfo` carries up from `Core.Project`) — the
invariant is about what a page or component reaches into *after* that point, not that bytes never
exist anywhere in the call chain. Extend the DTO if a view needs one more piece of information,
don't have it unwrap the bytes itself.

## Photo vs. attachment is a display distinction, not just a GEDCOM one

`Core.Gedcom`'s import-time classification (only a direct-child image `OBJE` is a photo — see
[core-gedcom.md](core-gedcom.md)) determines the `DataCategory` a row is stored under, and that
category is what the UI layer's converter registrations key off. A person's main/additional photos
render through the image-specific path (`PersonInfoView`, `PhotoViewerDialog`); attachments render
through a generic file-list-with-open-action path (`PersonPage`'s attachment handling). Retagging
something from one category to the other is a data change with UI consequences on both sides, not
a cosmetic relabel.

## Non-goals

- Not a per-page walkthrough — `UI/App/Pages/*.xaml.cs` is the live list of pages and each one's
  own header comments are the right place for page-specific behavior.
- Not the dialog-by-dialog inventory — `UI/App/Dialogs/**` and its `Factory` records are
  self-describing once you know the construction pattern above.
- Not the styling/theming system (light/dark resource keys, `SetThemeColor`, font scaling) — that's
  presentation detail with no cross-cutting invariant beyond "use the existing resource keys."
