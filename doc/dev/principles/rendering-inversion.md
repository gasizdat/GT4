# Principle: rendering inversion

**A view that renders untrusted or variably-sourced content never holds the means to fetch that
content itself.** It declares what it needs through a narrow callback or event, and the host —
the page or dialog that actually owns the project, the network, and navigation — supplies the
answer. The view ends up unable to violate boundaries it was never given the tools to cross.

This is not "dependency injection" in the constructor-parameter sense (see
[dependency-injection.md](dependency-injection.md)); it's specifically about *rendering*
components, where the temptation is to hand the view a pre-built map or a live service reference
"because it's convenient right there." The inversion means the view instead asks a question, once,
about the one thing it's currently drawing.

## What this buys

- **The view can't leak.** `MarkdownView` (`UI/App/Components/MarkdownView.cs`) never imports
  `GT4.Core.Project`, never sees a `byte[]`, never opens a `Uri` for anything but the OS `Launcher`.
  A biography's raw content — however it got there — cannot smuggle project access through this
  component, because the component has none.
- **The view can't over-fetch.** It asks only for the links the parsed document actually
  references (`MarkdownView.ReferencedLinks`), not "everything the host has loaded" — which is what
  lets a biography link to a photo or attachment the host never collected in the first place. See
  `InlineMediaProviderTests.PhotoTheHostNeverLoaded_IsStillInlined`
  (`Tests/GT4.UI.App.DeviceTests/Tests/Components/InlineMediaProviderTests.cs`) for the case this
  makes possible.
- **Navigation and link semantics live with the host, not the renderer.** `MarkdownView` raises
  `PersonLinkTapped` / `AttachmentLinkTapped` instead of navigating; `PersonPage.xaml.cs` decides
  what tapping a person link means. The renderer doesn't know what a "person" is.

## The shape, concretely

1. **A delegate type owned by neither side**, describing the question, not the answer's storage —
   `InlineMediaResolver` (`UI/App/Components/InlineMedia.cs`): `Task<InlineMedia?> (string link)`.
   It takes the whole link, not a parsed id — which link *schemes exist at all* (`person:`,
   `attachment:`, `media:`, or a plain URL) is left to the resolver, not baked into the view.
2. **A bindable property carrying the delegate**, not a map — `MarkdownView.MediaResolverProperty`.
   Set once per host; the view calls through it per distinct link, memoizing by link string
   (`MarkdownView._ResolvedMedia`) so a re-render doesn't re-ask.
3. **A resolver implementation that does the crossing**, sitting on the other side of the
   boundary — `InlineMediaProvider.ResolveAsync` (`UI/App/Converters/InlineMediaProvider.cs`).
   It parses the scheme (via `MarkdownLinkUtils`, see below), reaches into the project through
   `ICurrentProjectProvider`, and turns whatever it finds into the one shape the view understands
   (`InlineMedia`) — never the domain type, never raw bytes.
4. **Both directions of any custom link syntax owned by one shared utility**, so the writer and
   the reader of a link cannot drift — `MarkdownLinkUtils` (`UI/Utils/MarkdownLinkUtils.cs`) has
   both `PersonUrl`/`TryParsePersonId`, `AttachmentUrl`/`TryParseAttachmentId`, and
   `MediaUrl`/`TryParseMediaId`. Neither `MarkdownView` nor `InlineMediaProvider` builds or parses
   a `"media:"` string itself.

## Non-goals

- Not a mandate that every view take a delegate for everything — a view with genuinely
  self-contained, trusted input has nothing to invert.
- Not a description of `MarkdownView`'s rendering logic (block/inline dispatch, styling). That's
  implementation of the *renderer*, not the resolution boundary; renaming `RenderBlock` wouldn't
  falsify anything above.
- Not the retry/memoization/generation-tracking mechanics inside `MarkdownView.Refresh` /
  `ResolveMediaAsync` — those are a correct implementation of "ask once, apply on the UI thread,
  don't lose in-flight work across a resolver swap," worth reading when you touch that file, not a
  transferable principle.

## Where else to look for this shape

Nowhere else in the UI layer, currently — an explicit audit (2026-08) found no second component
built the older way (a pre-built map handed in by the host). If you're about to hand a rendering
component a `Dictionary<id, byte[]>`, a raw `IProjectConnection`, or a `NavigationPage` reference
"because it's already available at the call site," that's the sign this principle applies and
doesn't yet.
