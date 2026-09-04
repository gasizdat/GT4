# Store screenshots

Thirteen 1920×1080 PNGs, captured from the **Release** Windows head
(`UI/App/AppWinOnly.csproj`) at commit `7816ec9d`, in English, light theme, 100%
text scale. The build they came from reported itself as `4.0.0.645`, under the
version layout that shipped before the Store fix; `01-home.png` and
`12-home-dark.png` print that string on screen, so re-shoot those two if either
is submitted. The data is the bundled Brontë demo tree
(`UI/App/Resources/Raw/demo.ged`) — every portrait in it is a public-domain
Wikimedia image, so nothing here is anyone's private research.

Release, not Debug, matters: `PersonInfoView.CommonName` appends `" (Id: N)"` to
every displayed name under `#if DEBUG`, which would put a stray id after each
name in the shots.

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

## Two things worth fixing before these go public

- **`13-family-tree-dark.png` is now stale**, not wrong-by-design: the pale
  purple "More ancestors" button was the stock MAUI `PrimaryDark` in
  `UI/App/Resources/Styles/Colors.xaml`, and that token is now the app's green
  (`#86C5AE`, pinned by `ButtonPaletteTests`). Every dark-theme shot therefore
  predates the palette — re-shoot `12-home-dark.png` and `13-family-tree-dark.png`
  or leave both out.
- **`08-gallery.png`** is honest but text-heavy: every row is a Wikimedia
  attribution line, because that is what the demo file's `TITL` tags carry. It
  reads more like a credits list than a gallery. Consider dropping it, or
  re-shooting the gallery against a tree with ordinary captions.

## Reproducing the set

`capture/README.md` has the recipe, the two scripts, and the traps that cost the
most time (synthetic clicks, hover colours, exact 1920×1080 framing).
