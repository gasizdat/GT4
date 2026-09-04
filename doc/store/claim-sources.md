# Where each listing claim comes from

Written alongside [microsoft-store-listing-en.md](microsoft-store-listing-en.md)
so a future edit can re-check a claim instead of re-deriving it. Verified against
commit `7816ec9d`.

| Claim | Source |
|-------|--------|
| App name "Genealogy Tree", identity `gasizdat.GenealogyTree` | `UI/App/AppCommon.props` — both are `Condition`-set for the Windows TFM, with comments saying they must match what is reserved in Partner Center |
| Five UI languages: EN, RU, DE, ES, FR | `UI/Utils/Language.cs` (`Languages = [EN, RU, DE, ES, FR]`), backed by five `UI/Resources/UIStrings*.resx` files. True of the app, but the **MSIX declares `EN-US` only** — the `.resx` files become .NET satellite assemblies and the package's `x-generate` PRI build never sees them, so Partner Center will show English alone. Keep the claim in the copy; don't expect the Store's language list to agree |
| Zoom 0.4×–2.5×, drag-to-pan, load more generations up/down, collaterals toggle | `UI/App/Pages/FamilyTreePage.md` — `MinZoom`/`MaxZoom`/`ZoomStep`, `OnCanvasPan`, `LoadAncestors`/`LoadDescendants`, `_IncludeCollaterals` |
| Ctrl + / − / 0 scales all text; pinch does it on Android | `UIStrings.FieldFontScaleHintWindows` and its Android sibling; `IZoomablePage` redirects those to the tree while the tree is open |
| Theme: System / Light / Dark | `UIStrings.FieldTheme*`, issue #323 |
| Kinship finder returns a relationship **and** the connecting chain | `UI/App/Pages/KinshipFinderPage.xaml` — `FieldKinshipSummary` card above a `FieldKinshipChain` list |
| Relationship vocabulary: "Great-", "Grand aunt", "once/twice/thrice removed", cousins | `UIStrings.RelGreat_1` ("Great-{0}"), `RelGrandAunt_en` ("Grand aunt") and its uncle/niece/nephew siblings, `Rel1Removed_en_1`, `Rel2Removed_en_1`, `Rel3Removed_en_1`, `RelNRemoved_en_2`, `RelCousin*`. Two traps: **there is no ordinal cousin degree** — the app says "Cousin", never "second cousin" — and the grand- forms are two words ("Grand aunt"), so quote them as the app renders them |
| "Over twenty measures" in Statistics | `UIStrings.FieldStat*` — 29 strings, of which 6 are section headings (Overview, Age, Birth years, Names, Data completeness, Relationships), leaving 23 measures. **"Generations" is not among them**; an earlier draft of this listing claimed it |
| Calendar of births, anniversaries, remembrances, with milestones | `UIStrings.DateCalendar*` — `FilterBirths`, `FilterWeddings`, `FilterDeaths`, `MilestoneBadge`, `FilterMilestoneOnly` |
| Calendar surfaces month-only dates separately | `UIStrings.DateCalendarDayUnknownHeader_1` ("Sometime in {0} — exact day not recorded") and `DateCalendarUnplaceableFootnote_1`; both visible in `screenshots/10-date-calendar.png` |
| Filters: name wildcards, sex, year | `UI/App/Components/PersonFilterView.xaml` binds `FieldSearchText` (hint `HintNameFilterWildcard`, "`*` and `?` wildcards supported"), `FieldFilterSex`, `FieldYear` |
| Several names per person: first, patronymic, last, family | `UIStrings.NameFirst` / `NamePatronymic` / `NameLast` / `NameFamily`; display order is a user setting (`PersonNameSetting`, keyed per `NameFormat`) |
| Approximate and unknown dates are first-class | `UIStrings.DateStatusYearApproximate_1` ("about {0}"), `DateStatusUnknown`, `DateStatusNotDefined`; rendered as "about 1755 (about 271 years)" in `screenshots/09-kinship-finder.png`'s picker |
| GEDCOM 5.5.1 import/export preserves unmodeled tags | `doc/dev/00-overview.md` and `doc/dev/subsystems/core-gedcom.md`: import/export "is built around preserving whatever isn't natively modeled rather than silently dropping it on a round-trip". Visible as the "Additional details" / "Family details" sections in `screenshots/05-biography.png`. **Scope this to tags** — see the round-trip exclusion below |
| Ambiguous GEDCOM charsets prompt rather than guess | `UIStrings.HintGedcomDeclaredCharset_1` + `TitleSelectEncodingDialog` (issue #121) |
| Attachments open in the OS's own app | `PersonPage.xaml.cs` / `GalleryPage.xaml.cs` call `attachment.OpenAsync` |
| Biographies are Markdown with inline media | `MarkdownView` over Markdig (`Markdig` package in `AppCommon.props`); `InlineMediaProvider` resolves embedded media |
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

- **"A GEDCOM round trip doesn't cost you data."** An earlier draft said this.
  Open issue #281: the three family media categories (`FamilyMainPhoto`,
  `FamilyPhoto`, `FamilyAttachment`) live in `NameData`, which `Core.Gedcom`
  never reads, so exporting and re-importing silently loses every family photo
  and attachment. Person media survives, because `GedcomExporter` reads
  `PersonData`. The listing therefore promises only what is true — unmodeled
  *tags* are carried through — and says nothing about media surviving the trip.
  Widen this again once #281 ships.
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
