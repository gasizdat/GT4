# Capturing store screenshots

`capture.ps1` shoots the app window to a PNG; `click.ps1` clicks a point in that
PNG's coordinate space. Together they let you drive the real app and shoot it,
without a UI-automation framework: capture, look at the image, click what you
see, capture again.

```powershell
dotnet build UI\App\AppWinOnly.csproj -c Release
$exe = 'UI\App\bin\Release\net10.0-windows10.0.19041.0\win-x64\AppWinOnly.exe'
$p = Start-Process $exe -PassThru; Start-Sleep 14
.\doc\store\capture\capture.ps1 -ProcessId $p.Id -Out shot.png -Width 1934 -Height 1087
.\doc\store\capture\click.ps1   -ProcessId $p.Id -X 960 -Y 1039 -WaitMs 5000 -Out next.png
```

## What the numbers mean

**`-Width 1934 -Height 1087` gives an exactly 1920×1080 PNG.** `PrintWindow`
renders from the window rect, but the shot is cropped to the DWM *extended frame
bounds*, which is inset by 7px left, 7px right and 7px bottom — so the frame is
`(width - 14) × (height - 7)`. The window ends up larger than the 1920×1080
screen; that is fine, `PrintWindow` re-renders rather than screen-scraping, so
nothing is clipped. Position it at x = -7 to line the visible frame up with the
screen edge.

**Click coordinates are window-rect coordinates, not frame coordinates.** The
captured image starts at the window origin, so image (x, y) maps to screen
(windowRect.Left + x, windowRect.Top + y). Using the DWM frame origin here puts
every click 7px right of where you aimed.

## Traps

- **Release build only.** `PersonInfoView.CommonName` appends `" (Id: N)"` to
  every name under `#if DEBUG`.
- **A click that teleports onto a control is ignored** by the smaller targets —
  the side-menu buttons in particular. WinUI wants a hover first, so `click.ps1`
  moves near the target, pauses, moves onto it, pauses, then presses.
- **Park the cursor inside the window before shooting.** A hovered button
  repaints in its hover colour (currently a stock-MAUI orange on the footer
  buttons) and freezes that way in the shot. Moving the cursor *off* the window
  entirely does not help: the last hovered control keeps its hover state.
  `capture.ps1` parks on the title bar, which is outside the MAUI content.
- **A `CollectionView` row that is already selected does not re-navigate** —
  selection, not tap, is what drives it. Select a different row first, then the
  one you want.
- **The side-menu icons are unlabelled**, so their meaning comes from the order
  the page declares its `PageMenuItem`s in. Read the page's XAML, don't guess.
- **Settings live in `%APPDATA%\{067F098F-…}\.config\appconfig.json`** — language,
  theme, font scale, date formats. Editing it before launch is the reliable way
  to set up a shot. Back it up and restore it: it is the developer's own config.

## Getting a demo project without the file picker

The GEDCOM import button opens a native file dialog that synthetic clicks cannot
drive, and the built-in demo tree is only offered when the project list is
*empty*. Build the project file directly instead:

```powershell
dotnet build Tools\GT4.Tools.RelativesCli\GT4.Tools.RelativesCli.csproj -c Release
Copy-Item UI\App\Resources\Raw\demo.ged "$env:TEMP\Brontë Family (Demo).ged"
dotnet Tools\GT4.Tools.RelativesCli\bin\Release\net10.0\GT4.Tools.RelativesCli.dll `
  --gedcom "$env:TEMP\Brontë Family (Demo).ged" `
  --out "$env:USERPROFILE\Documents\GT4\_shots\Brontë Family (Demo).gt4" find Charlotte
```

**The CLI does not persist the project name**, so the list shows a
`DataException: There is no name stored in the project` card instead of a
project. Write the name yourself before opening it — `Metadata` is
`(Id TEXT PRIMARY KEY, Data BLOB)`:

```sql
INSERT OR REPLACE INTO Metadata (Id, Data) VALUES ('name', 'Brontë Family (Demo)');
```

Any SQLite client will do. From PowerShell, `Microsoft.Data.Sqlite.dll` plus the
`SQLitePCLRaw.*` assemblies from the CLI's output folder work, but the native
`e_sqlite3.dll` is not copied there — take it from a test project's output
(`Tests\GT4.Core.Project.Tests\bin\Release\...\win-x64\`), put all five files in
one directory, `cd` into it, and call `[SQLitePCL.Batteries_V2]::Init()` first.

Delete the project directory and its `%APPDATA%\{…}\.cache\<name>\` folder
afterwards.
