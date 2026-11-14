# Where each listing claim comes from

Written alongside [microsoft-store-listing-en.md](microsoft-store-listing-en.md)
so a future edit can re-check a claim instead of re-deriving it. Verified against
`v4.0.656.0` (commit `6d588c18`); rows touched by the October 2026 release were
re-checked against master `03446f6e`. The screenshots two of the rows below cite
were shot at that tag or at `v4.0.652.0` — [their README](listings/README.md)
says which came from which.

| Claim | Source |
|-------|--------|
| App name "Genealogy Tree", identity `gasizdat.GenealogyTree` | `UI/App/AppCommon.props` — both are `Condition`-set for the Windows TFM, with comments saying they must match what is reserved in Partner Center |
| Five UI languages: EN, RU, DE, ES, FR | `UI/Utils/Language.cs` (`Languages = [EN, RU, DE, ES, FR]`), backed by five `UI/Resources/UIStrings*.resx` files. True of the app, but the **MSIX declares `EN-US` only** — the `.resx` files become .NET satellite assemblies and the package's `x-generate` PRI build never sees them, so Partner Center will show English alone. Keep the claim in the copy; don't expect the Store's language list to agree |
| Zoom 0.4×–2.5×, drag-to-pan, load more generations up/down, collaterals toggle | `UI/App/Pages/FamilyTreePage.md` — `MinZoom`/`MaxZoom`/`ZoomStep`, `OnCanvasPan`, `LoadAncestors`/`LoadDescendants`, `_IncludeCollaterals`. Re-checked against the October 2026 tree rewrite: all four survive under the same names |
| Tree is a tidy pedigree: children centred under their parents, each family drawn once | `UI/App/Pages/FamilyTreePage.md`, layout step of the load pipeline (PR #470, issue #469). It replaced the spring layout, so the old "lines that never cross more than they have to" wording is gone |
| Drag people into place / hide them, both remembered per project | Arrange mode and `OnNodePan`-driven pins saved through `IFamilyTreeArrangementStore`; "Hide from tree" (`UIStrings.MenuItemNameHideFromTree`) through `IFamilyTreeHiddenPersonsStore` (#423, #424). Both persist to the project's `projectconfig.json` in the app's project cache (`Core/Utils/ProjectConfigurationProvider.cs`), **not** inside the `.gt4` file — so "remembered per project" is true on this machine, but the arrangement doesn't travel with a copied project file. Pins are also per centre person. Hiding is a right-click: desktop only, no mobile entry point |
| Hovering a node highlights it and its lines | `FamilyTreePage.md` "Hovering" (`_HoveredId`), PR #476 |
| Ctrl + / − / 0 scales all text; pinch does it on Android | `UIStrings.FieldFontScaleHintWindows` and its Android sibling; `IZoomablePage` redirects those to the tree while the tree is open |
| Theme: System / Light / Dark | `UIStrings.FieldTheme*`, issue #323 |
| Kinship finder returns a relationship **and** the connecting chain | `UI/App/Pages/KinshipFinderPage.xaml` — `FieldKinshipSummary` card above a `FieldKinshipChain` list |
| Relationship vocabulary: "Great-", "Grand aunt", "once/twice/thrice removed", cousins | `UIStrings.RelGreat_1` ("Great-{0}"), `RelGrandAunt_en` ("Grand aunt") and its uncle/niece/nephew siblings, `Rel1Removed_en_1`, `Rel2Removed_en_1`, `Rel3Removed_en_1`, `RelNRemoved_en_2`, `RelCousin*`. Two traps: **there is no ordinal cousin degree** — the app says "Cousin", never "second cousin" — and the grand- forms are two words ("Grand aunt"), so quote them as the app renders them |
| "Over twenty measures" in Statistics | `UIStrings.FieldStat*` — 29 strings, of which 6 are section headings (Overview, Age, Birth years, Names, Data completeness, Relationships), leaving 23 measures. **"Generations" is not among them**; an earlier draft of this listing claimed it |
| Calendar of births, anniversaries, remembrances, with milestones | `UIStrings.DateCalendar*` — `FilterBirths`, `FilterWeddings`, `FilterDeaths`, `MilestoneBadge`, `FilterMilestoneOnly` |
| Calendar surfaces month-only dates separately | `UIStrings.DateCalendarDayUnknownHeader_1` ("Sometime in {0} — exact day not recorded") and `DateCalendarUnplaceableFootnote_1`; both visible in `listings/10-date-calendar.png` |
| Filters: name wildcards, sex, year | `UI/App/Components/PersonFilterView.xaml` binds `FieldSearchText` (hint `HintNameFilterWildcard`, "`*` and `?` wildcards supported"), `FieldFilterSex`, `FieldYear` |
| Several names per person: first, patronymic, last, family | `UIStrings.NameFirst` / `NamePatronymic` / `NameLast` / `NameFamily`; display order is a user setting (`PersonNameSetting`, keyed per `NameFormat`) |
| Approximate and unknown dates are first-class | `UIStrings.DateStatusYearApproximate_1` ("about {0}"), `DateStatusUnknown`, `DateStatusNotDefined`; rendered as "about 1755 (about 271 years)" in `listings/09-kinship-finder.png`'s picker |
| GEDCOM 5.5.1 import/export preserves unmodeled tags | `doc/dev/00-overview.md` and `doc/dev/subsystems/core-gedcom.md`: import/export "is built around preserving whatever isn't natively modeled rather than silently dropping it on a round-trip". Visible as the "Additional details" / "Family details" sections in `listings/05-biography.png`. **Scope this to tags** — see the round-trip note below |
| Family (clan) photos and attachments survive a GEDCOM export/reimport round trip | PR #369 (fixes #281): a new GT4 extension record, `_FAML` (keyed by the family's bare `NAME`), carries `FamilyMainPhoto`/`FamilyPhoto`/`FamilyAttachment` through export and back. Other GEDCOM tools ignore the underscore-prefixed tag, so this is safe to round-trip through them too — they just won't preserve it themselves. Person photos/attachments already round-tripped via `PersonData`; this closes the gap for family-level media specifically |
| Ambiguous GEDCOM charsets prompt rather than guess | `UIStrings.HintGedcomDeclaredCharset_1` + `TitleSelectEncodingDialog` (issue #121) |
| Image attachments open in the app's viewer; other attachments in the OS's own app | `PersonPage.xaml.cs`, `FamilyPage.xaml.cs` and `GalleryPage.xaml.cs` push `PhotoViewerDialog` for an attachment with an `Image`, and call `attachment.OpenAsync` otherwise (#443). Before this release every attachment went to `OpenAsync`, which is what the old "they open in whichever app you normally use" copy said |
| Biographies are Markdown with inline media | `MarkdownView` over Markdig (`Markdig` package in `AppCommon.props`); `InlineMediaProvider` resolves embedded media |
| Biography formatting toolbar; insert a photo as a picture at a width, or as a plain link | `MarkdownEditor` Bold/Italic/Heading/List buttons (PR #454); `SelectMediaDialog`'s "Insert as: Picture / Link" and 25–200% Width picker (PR #456). The picker lists every media item in the project, the person's own first (`SelectMediaDialog` orders by `_OwnMediaIds`), hence "photos from the project" — the older "the person's own photos" undersold it |
| Captions on a person's photos and attachments | PR #450 (issue #437), edited through `EditCaptionDialog` (`UIStrings.TitleEditCaptionDialog`). **Person** media only — don't extend the claim to family photos |
| Right-click to copy a person's name, dates or biography | `UI/App/Behaviors/CopyText.cs` (`CopyText.IsEnabled`), wired only in `PersonPage.xaml` (PR #459). A desktop context flyout; Android has no long-press yet, so drop this from any Play copy |
| Export the whole project as a self-contained HTML site, a page per person and family | `UI/App/HtmlExport/HtmlExporter.cs` writes a zipped static site from `UI/App/Resources/HtmlTemplates` (PRs #462, #464, #477, #478, #481). **Self-contained** was checked: the templates load only their own `style.css` and an inline script, and the one URL in them is the SVG namespace inside a `data:` URI. A biography's own web links and web images are carried into the site as written, which is user content, not something the export adds. The zip is built in the app's cache and handed to the OS share sheet (`ProjectPage.OnExportHtml`), so it goes wherever the user sends it. The export asks for confirmation first because it includes living people |
| GEDCOM import pairs a Russian surname's male and female forms under one family | `Core/Gedcom/GedcomFamilyName.cs`, `GedcomImporter` (PR #480, issue #479). Cyrillic surnames whose gendered ending agrees with `SEX` only; Latin script and invariant surnames keep the bare-surname family. Not claimed in the Description, only in What's new |
| Multiple projects, each a file in Documents | `Core/Utils/Storage.cs` — `ProjectsRoot` is `SpecialFolder.MyDocuments\GT4`; `ProjectList` scans it for `*.gt4` |
| Built-in Brontë demo tree | `ProjectListPage.OnOpenDemoProject` imports `demo.ged`; `UIStrings.TitleDemoProject` = "Brontë Family (Demo)". **Offered only when the project list is empty** (`IsEmptyStateVisible`) |
| No account, no cloud, no analytics | No auth or sync code anywhere; no analytics/telemetry package in any `.csproj` (checked AppCenter, Sentry, Application Insights). The listing's phrasing is "no account system, no cloud sync, no analytics and no ads" |

## The one network claim to keep precise

The app **does** declare `INTERNET` on Android and registers an
`IHttpClientFactory`. Both serve exactly one path: `ImageUtils.ToBytesAsync`
fetching an `ImageSource.FromUri`, i.e. a web image a user put in a biography.
`MarkdownView` additionally hands external links to `Launcher.Default.OpenAsync`.

So "works offline, nothing is sent anywhere" is true of the app's own behaviour,
and the listing says so in that form — *"The only time the app reaches the
internet at all is when you put a web link or a web image into a biography and
then open it."* Do not shorten this to a flat "no internet access": the manifest
says otherwise, and a reviewer can read the manifest.

## Claims deliberately **not** made

- **"A GEDCOM round trip doesn't cost you data," stated as a single unqualified
  claim.** An earlier draft said this outright. Issue #281 (family photos and
  attachments silently dropped by export/reimport) closed via PR #369 — see the
  round-trip row in the table above — so as of this release the copy can and
  does say family media round-trips too. It's still stated precisely rather than
  as one blanket claim: unmodeled *tags* round-trip losslessly (native GEDCOM
  behaviour), while family media round-trips through a GT4-specific extension
  tag. Both are true; they're not the same mechanism, so they're not collapsed
  into one sentence.
- **"Revision history — every change is tracked."** The previous listing draft
  said this. Revisions are recovery snapshots, not an edit log: `ProjectHost`
  copies the project to a working cache and flushes it back on clean close, so a
  `version-*.gt4` survives only when a session ended without flushing.
  `ProjectRevisionsPage` lets you restore or delete those. The listing calls them
  "restore points" for that reason.
- **"Zoomable" applied to anything but the family tree.** Only `FamilyTreePage`
  implements `IZoomablePage`; elsewhere the same gesture scales text.
- **Any iOS/macOS availability.** `App.csproj` multi-targets those TFMs, but no
  `.slnf` build head includes them (`doc/dev/00-overview.md`).
