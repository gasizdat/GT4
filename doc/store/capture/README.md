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

**The window is placed at (0,0), and that is not cosmetic.** It is taller than a
1080p screen, and `click.ps1` aims via `SetCursorPos`, which silently clamps to
the desktop. Any offset pushes the same number of rows at the bottom out of
reach — at (40,40) nothing below image y=1039 can be clicked, which includes the
footer button every list page ends with. The click still reports success; it
just lands somewhere else.

## Traps

- **Release build only.** `PersonInfoView.CommonName` appends `" (Id: N)"` to
  every name under `#if DEBUG`.
- **The session has to be unlocked, and the failure is silent.** `PrintWindow`
  renders a window it does not own the foreground of, so `capture.ps1` keeps
  working with the lock screen up while every click lands on `LockApp` instead.
  Shots that need no input still come out right, which is what makes it look like
  a click-targeting problem. `GetForegroundWindow` naming `LockApp` is the tell.
  Posting `WM_LBUTTONDOWN` straight at the
  `Microsoft.UI.Content.DesktopChildSiteBridge` child does not get around it —
  WinUI 3 takes pointer input through the input site and ignores the legacy
  message.
- **A click that teleports onto a control is ignored** by the smaller targets —
  the side-menu buttons in particular. WinUI wants a hover first, so `click.ps1`
  moves near the target, pauses, moves onto it, pauses, then presses.
- **Park the cursor inside the window before shooting.** A hovered button
  repaints in its hover colour — `ControlFillHover`, a pale green wash with the
  border going `Primary` — and freezes that way in the shot. Moving the cursor
  *off* the window entirely does not help: the last hovered control keeps its
  hover state. `capture.ps1` parks on the title bar, which is outside the MAUI
  content.
- **A `CollectionView` row that is already selected does not re-navigate** —
  selection, not tap, is what drives it. Select a different row first, then the
  one you want.
- **The person picker needs its footer button.** Clicking a row only selects it;
  the footer changes from *Cancel* to *Ok*, and the choice is not committed
  until that is pressed. Clicking the next *Choose…* instead leaves you in the
  same picker with the wrong person selected.
- **The chevrons flanking a person's photo move between people, not photos.**
  They look like a carousel. Use the breadcrumb strip above the summary to get
  back, or the shot ends up on a different person than the caption claims.
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
$root = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'GT4\_shots'
dotnet Tools\GT4.Tools.RelativesCli\bin\Release\net10.0\GT4.Tools.RelativesCli.dll `
  --gedcom "$env:TEMP\Brontë Family (Demo).ged" `
  --out "$root\Brontë Family (Demo).gt4" find Charlotte
```

Resolve `MyDocuments` rather than assuming `$env:USERPROFILE\Documents`: that is
what `Storage.ProjectsRoot` does, and with OneDrive's folder backup on it lands
under `$env:USERPROFILE\OneDrive\Documents` instead. Writing to the literal path
puts the project somewhere the app never scans, and the list simply comes up
without it. The `_shots` subfolder is fine either way — `ProjectList` recurses.

**The CLI does not persist the project name**, so the list shows a
`DataException: There is no name stored in the project` card instead of a
project. Write the name yourself before opening it — `Metadata` is
`(Id TEXT PRIMARY KEY, Data BLOB)`:

```sql
INSERT OR REPLACE INTO Metadata (Id, Data) VALUES ('name', 'Brontë Family (Demo)');
```

**Write it as TEXT, not as bytes.** The column says `BLOB` and SQLite will store
whatever you hand it, but `TableMetadata.GetAsync` casts the value straight to
`string` — a UTF-8 `byte[]` gets you an `InvalidCastException` card in the
project list where the project should be. The SQL literal above is right; a
parameter bound to `Encoding.UTF8.GetBytes(name)` is not.

Fixing the row is not enough on its own: the project list caches what it read,
and its refresh button does not re-read the name. Restart the app.

Any SQLite client will do. From PowerShell, `Microsoft.Data.Sqlite.dll` plus the
`SQLitePCLRaw.*` assemblies from the CLI's output folder work, but the native
`e_sqlite3.dll` is not copied there — take it from a test project's output
(`Tests\GT4.Core.Project.Tests\bin\Release\...\win-x64\`), put all five files in
one directory, `cd` into it, and call `[SQLitePCL.Batteries_V2]::Init()` first.

Delete the project directory and its `%APPDATA%\{…}\.cache\<name>\` folder
afterwards, where `<name>` is the project's *file* name, not the name you wrote
into `Metadata`. Leave the sibling cache folders alone: the app touches every
one of them at startup while sweeping revisions, so a recent write time is not a
sign one belongs to this run.
