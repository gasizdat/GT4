# Store screenshots

Thirteen 1920×1080 PNGs, captured from the **Release** Windows head
(`UI/App/AppWinOnly.csproj`) at commit `7816ec9d`, in English, light theme, 100%
text scale. The data is the bundled Brontë demo tree
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

## The whole set predates the shipping palette

Re-shoot all thirteen before submitting any of them; do not pick survivors out
of this set. Two PRs have landed on the palette since `7816ec9d` — #358 and
#360 — and #360 re-tinted the entire neutral ramp warm (`Gray100` `#E1E1E1` →
`#F1EEE7`, and so on down to `Gray950`), which reaches the text and borders of
every screen.

Two of the changes are large enough that a reader comparing an old shot to the
app would notice them first:

- **The tab strips**, in `04-person.png` and `05-biography.png` — Partner
  Center slots 2 and 3. Both show the strip `PersonPage` declared inline, whose
  active tab is a solid `Primary` pill with white text. #360 moved it into
  `UI/App/Components/TabItemView.xaml`, where the active tab is a pale
  `ControlFillHover` fill with dark `PrimaryDarkText` ink and a `Primary`
  outline — near enough an inversion.
- **The dark-theme shots.** `PrimaryDark` was the stock MAUI purple; it is the
  app's green now (`#7FC0A4`), so `12-home-dark.png` and
  `13-family-tree-dark.png` show a button colour the app no longer has.

The light-theme `Accent` `#8B6F4E` did *not* move, so `07-statistics.png`'s
decade bars are the one prominent block of colour here that still matches.

## Also worth checking when you re-shoot

- **`01-home.png` and `12-home-dark.png` print the version on screen**, centred
  under the title. They read `(4.0.0.645)`, from the layout that shipped before
  the Store fix moved the commit count into the build field. A re-shoot picks
  the current number up on its own — just confirm it looks like `4.0.<count>.0`.
- **`08-gallery.png`** is honest but text-heavy: every row is a Wikimedia
  attribution line, because that is what the demo file's `TITL` tags carry. It
  reads more like a credits list than a gallery. Consider dropping it, or
  re-shooting the gallery against a tree with ordinary captions.

## Reproducing the set

`capture/README.md` has the recipe, the two scripts, and the traps that cost the
most time (synthetic clicks, hover colours, exact 1920×1080 framing).
