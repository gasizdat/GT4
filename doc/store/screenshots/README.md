# Store screenshots

Thirteen 1920×1080 PNGs, captured from the **Release** Windows head
(`UI/App/AppWinOnly.csproj`), English, 100% text scale; light theme except `12`
and `13`. The data is the bundled Brontë demo tree
(`UI/App/Resources/Raw/demo.ged`) — every portrait in it is a public-domain
Wikimedia image, so nothing here is anyone's private research.

Release, not Debug, matters: `PersonInfoView.CommonName` appends `" (Id: N)"` to
every displayed name under `#if DEBUG`, which would put a stray id after each
name in the shots.

## Three of these still have to be re-shot

All thirteen come from `v4.0.652.0` (commit `24aa0264`). The package being
submitted is `v4.0.656.0` (commit `6d588c18`), and three shots do not survive the
gap:

- **`01-home.png` and `12-home-dark.png`.** The home screen prints the version,
  so a shot from any other build contradicts the package it ships with.
- **`04-person.png`.** It shows the caption stretching the photo pane to the
  width of its text, which #362 fixed; `656` is the first build without it.

The other ten are good as they are. The only code between the two tags is that
caption cap, and it reaches one pane: the photo on the person page's Relatives
tab. None of the ten can show it — the demo tree gives the family page no photos,
the biography tab is a different tab, and the gallery never uses the component.

Re-shoot all three in one pass on an **unlocked** session; `capture/README.md`
says why that is not optional even for the two that need no clicking.

## Suggested Partner Center order and captions

Microsoft Store takes up to 10 screenshots per device family, so the last three
below are spares.

| # | File | Caption |
|---|------|---------|
| 1 | `06-family-tree.png` | Ancestors and descendants around one person — click any relative to re-centre |
| 2 | `04-person.png` | Every person gets portraits, life dates and their whole circle of relatives |
| 3 | `05-biography.png` | Write a real biography: headings, lists, emphasis and links |
| 4 | `09-kinship-finder.png` | How are these two related? The answer, and the chain that connects them |
| 5 | `10-date-calendar.png` | Births, anniversaries and remembrances on any day of the year |
| 6 | `07-statistics.png` | Twenty-plus measures of your tree, including where the data is thin |
| 7 | `02-families.png` | Families at a glance, each with the people in it |
| 8 | `08-gallery.png` | Every photo and document in the project, linked to who it belongs to |
| 9 | `13-family-tree-dark.png` | Light, dark, or follow the system |
| 10 | `01-home.png` | Open a tree, or start one — no account, nothing to sign up for |
| — | `03-family.png` | *(spare)* One family's members with their life spans |
| — | `11-settings.png` | *(spare)* Text size, theme and date formats are yours to set |
| — | `12-home-dark.png` | *(spare)* Home in the dark palette |

## Choices behind these shots

Two of them are not what the app offers by default, and a re-shoot that skips
them comes out visibly worse:

- **`06-family-tree.png` and `13-family-tree-dark.png` have the *Relatives*
  toggle on.** Off — the default — Charlotte's tree is her two parents and four
  grandparents, because she had no children, and the lead Store screenshot ends
  up looking like the app cannot draw a family. On, it shows all six Brontë
  siblings and reads like a family tree.
- **`10-date-calendar.png` is April, not the current month.** The calendar opens
  on today, and most months of the demo tree are near-empty. April carries four
  dated entries plus the *"Sometime in April — exact day not recorded"* section,
  which is the only place the month-only-date handling is visible.

`09-kinship-finder.png` pairs Hugh Brunty with his granddaughter Charlotte —
far enough apart to show a connecting chain rather than a one-line answer, and
the demo tree's longest blood relationship.

## Still worth fixing

- **`08-gallery.png`** is honest but text-heavy: every row is a Wikimedia
  attribution line, because that is what the demo file's `TITL` tags carry. It
  reads more like a credits list than a gallery. Consider dropping it, or
  re-shooting the gallery against a tree with ordinary captions.
- **`04-person.png` carries two breadcrumbs**, Charlotte and Patrick Branwell,
  because the shot was taken after visiting both. One would be tidier. Harmless,
  and arguably shows the navigation history, but worth knowing it was not
  deliberate.

## Reproducing the set

`capture/README.md` has the recipe, the two scripts, and the traps that cost the
most time (synthetic clicks, hover colours, exact 1920×1080 framing).
