# Cutting a release and submitting it to the Store

The process was previously undocumented and had to be reconstructed from
`.github/workflows/release.yml` and the shape of the `release/rc-aug-15` branch.
This is that reconstruction, plus the identity rules the Store enforces.

## How a release happens

`release.yml` is `workflow_dispatch` only and refuses to run unless
`github.ref` starts with `refs/heads/release/`. It does everything else itself:
reads the version back out of MSBuild, publishes the Windows head twice (a plain
`win-x64` folder and an MSIX), mints a throwaway signing certificate, and creates
the GitHub release with three assets — `.zip`, `.msix`, `.cer`.

So a release branch carries no build configuration of its own. `release/rc-aug-15`
was byte-for-byte the master tip it was cut from.

Keep it that way where you can. The build number is `git rev-list --count HEAD`,
so every commit on a release branch raises the version over the master commit
being shipped: v4.0.656.0 carries the code of a master tip whose own count is
650, because this `doc/store` directory and its corrections sit on the branch.
The version therefore names a release-branch commit, not a master one — and two
release branches cut from the same master tip, each with a commit of its own,
would claim one version for two different trees. If that ever gets in the way,
move these documents to master.

```powershell
git fetch origin
git checkout --no-track -b release/rc-<month>-<day> origin/master
git push -u origin release/rc-<month>-<day>
gh workflow run release.yml --ref release/rc-<month>-<day>
gh run watch (gh run list --workflow release.yml --limit 1 --json databaseId -q '.[0].databaseId')
```

Cut the branch only from a master commit whose CI is green — the workflow builds
and publishes but runs no tests.

## Version

`UI/App/AppCommon.props`, target `SetBuildNumber`:

```
Version = 4.0.<git rev-list --count HEAD>.0
```

**`$(Version)` is not what the MSIX carries.** MAUI composes the package identity
version as `$(ApplicationDisplayVersion)`, padded to three fields, followed by
`$(ApplicationVersion)` — so `4.0` plus a commit count of 646 gives `4.0.0.646`,
regardless of what `$(Version)` says. The props file therefore sets
`ApplicationDisplayVersion` to `4.0.<count>` and `ApplicationVersion` to `0` on
the Windows head only; Android keeps `4.0` and a version code of the count.

Read the answer off the package, never off MSBuild's log line — they disagreed
here, and the log line was the one that looked right. Reading
`GT4-4.0.656.0-win-x64.msix` back out of the release confirms the layout holds:
`Identity/@Version` is `4.0.656.0`, fourth field free.

The trailing `.0` is not decoration. Microsoft's
[app package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
are explicit: "the last (fourth) section of the version number is reserved for
Store use and must be left as 0 when you build your package", with the other
sections between 0 and 65535 and the first non-zero. So the commit count has to
sit in the build field. Before September 2026 it sat in the revision field
(`4.0.0.642`), which is why no earlier CI build could have been submitted.

`ApplicationVersion` and `VersionCode` still read `BuildNumber` on its own, so
Android's version code is unaffected by the layout.

Each submission must carry a strictly higher version than the last one accepted.
Since the number is the commit count, that is automatic as long as releases only
ever move forward on master.

## Package identity

Three values have to agree with what Partner Center shows under
**Product management → View app identity details**, or the upload is rejected.
Microsoft's own warning applies here: the values are case-sensitive and spaces
and punctuation must match exactly.

| Value | Where it lives | Current | Matches Partner Center |
|---|---|---|---|
| `Package/Identity/Name` | `UI/App/AppCommon.props` → `ApplicationId` (Windows condition) | `gasizdat.GenealogyTree` | yes |
| `Package/Properties/PublisherDisplayName` | `UI/App/Platforms/Windows/Package.appxmanifest` | `gasizdat` | yes |
| `Package/Identity/Publisher` | `UI/App/Platforms/Windows/Package.appxmanifest` | `CN=A98ED4EC-4C7B-44A1-8FF4-199B559BD849` | yes |

The publisher GUID looks like boilerplate and is not: the MAUI template ships
`Publisher="CN=User Name"`, so this value was set deliberately. It is checkable
without opening Partner Center, because the Package Family Name's suffix is a
hash of the publisher string — SHA-256 of the UTF-16LE bytes, first 8 bytes,
base32 over `0123456789abcdefghjkmnpqrstvwxyz`. That string hashes to
`25ksdz0mncfjg`, and Partner Center reports the PFN as
`gasizdat.GenealogyTree_25ksdz0mncfjg`. Re-run that check rather than trusting
the GUID's appearance if the identity is ever in doubt.

The rest of the identity page is reference data, not something the build sets:
Package Family Name `gasizdat.GenealogyTree_25ksdz0mncfjg`, Store ID
`9MV3J705L3QP`. The PFN is what `Get-AppxPackage` matches on when checking a
local install.

MAUI rewrites `Identity/@Name`, `Identity/@Version`, `Properties/DisplayName` and
the `VisualElements` display strings at packaging time — the `$placeholder$`
tokens and the placeholder identity name in the checked-in manifest are expected
and harmless. It does **not** touch `Identity/@Publisher`; that one ships exactly
as written. Confirm what actually ships by reading the generated manifest rather
than the source one:

```
UI\App\obj\AppWinOnly\Release\net10.0-windows10.0.19041.0\win-x64\resizetizer\m\Package.appxmanifest
```

**`Identity/@Publisher` and the signing certificate move together.** `release.yml`
mints a self-signed certificate whose `-Subject` must equal the manifest's
`Publisher` exactly, or the MSIX signing step fails and the release dispatch dies
part-way through. They agree today. If the publisher ever changes, `grep` the
repo for the GUID — it appears in the manifest, in `release.yml`, and in this
directory's docs.

The certificate is deliberately throwaway: the Store re-signs everything it
ingests, so it only has to produce a validly signed package. It is exported as a
release asset so that a direct sideload install can be trusted locally.

## Before uploading

Install the release's `.msix` and run the app once. Nothing in CI does this, and
MSIX redirects an app's `ApplicationData` writes into a per-package container —
which is where settings, the project cache and crash logs go. Projects
themselves live in Documents and are unaffected. An unpackaged run proves
nothing about the packaged one.

The signing certificate is self-signed, so Windows will not install the package
until it is trusted. Both the `.cer` and the `.msix` are release assets:

```powershell
gh release download v<version> --pattern "*.msix" --pattern "*.cer"
# elevated, once per certificate — each release mints a new one:
Import-Certificate -FilePath GT4-<version>-win-x64.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage -Path GT4-<version>-win-x64.msix
Get-AppxPackage gasizdat.GenealogyTree | Select-Object Name, Version, InstallLocation
```

Uninstall with `Remove-AppxPackage` before installing the next one. Note that
this removes the package container — settings and the project cache go with it,
though the `.gt4` files in Documents do not.

Then run the [Windows App Certification Kit](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit)
against the same package. It catches the manifest and packaging faults that
otherwise come back as a certification failure days later.

If you package locally to check any of this, delete
`obj\AppWinOnly\Release\...\resizetizer\m\Package.appxmanifest` first. Its
up-to-date check watches the source manifest, not the derived version, so an
incremental build happily re-stamps a package with the version from the last
one. CI is unaffected — it starts from an empty `obj`.

## Submitting to the Store

Partner Center walks five sections.

**Pricing and availability** — free; markets; visibility. No blockers.

**Properties**
- Category: Books + reference, subcategory Reference; secondary Productivity.
- Privacy policy: the repo has no public domain to host a URL at, so
  [privacy-policy.md](privacy-policy.md) goes in as policy text directly —
  Partner Center accepts that in place of a URL.
- Support contact: `gasizdat@gmail.com`.
- Website: optional; the GitHub repository works.

**Age ratings** — the IARC questionnaire. The honest answers are: no violence, no
sexual content, no gambling, no in-app purchases, no advertising, no data shared
with third parties, no user-to-user communication. Biographies are user-authored
but never leave the device, so there is no user-generated *content sharing*.
Expect 3+ / Everyone.

**Packages** — upload the `.msix` from the GitHub release. It does not need to be
signed by a trusted CA: the Store re-signs every MSIX it accepts. The
`runFullTrust` restricted capability is normal for a packaged desktop app; if the
form asks for justification, it is "packaged Win32/WinUI desktop application".

**Mandatory update** — a per-submission toggle on the Packages page, not a
property of the app in general; decide it fresh each time, not from what a
previous submission did. Default to leaving it off: most updates here are
features and non-crash bug fixes, and marking every submission mandatory just
trains the toggle to be ignored. Turn it on when a submission fixes a crash or
a data-loss/corruption bug that shipped in a previous submission — the
September 2026 release (this one, at the time of writing) qualifies: PR #372
fixes a `STATUS_STOWED_EXCEPTION` crash decoding photo thumbnails. Word it
precisely if you note the reason anywhere user-facing: PR #372 fixes that crash
on the one repro path it targeted (thumbnail decoding contending with WinUI's
UI-thread lock); issue #370 — the same crash class — stays open for other
triggers, so "fixes a crash" is accurate and "fixes the crash" is not.

The package declares **`EN-US` only** — read out of the shipped
`GT4-4.0.656.0-win-x64.msix`. The manifest asks for
`<Resource Language="x-generate" />`, and the PRI build finds one language,
because the five `UIStrings*.resx` files become .NET satellite assemblies, which
MRT does not see. The app really does offer Russian, German, Spanish and French —
the user picks them in Settings — but the Store will list English only unless
package-level language resources are added. Say so in the listing copy (it does)
rather than expecting Partner Center to show all five.

**Store listings** — upload [listing-data-en-us.csv](listing-data-en-us.csv)
via the Store listings page's **Import** button rather than retyping fields;
it carries Description, What's new, Features, Search terms and screenshot
captions in the exact plain-text style Partner Center's fields actually
render (see the note in
[microsoft-store-listing-en.md](microsoft-store-listing-en.md) — Markdown
doesn't render there). The screenshot-file rows still hold whatever asset URLs
were live in Partner Center at the last export; how the Import path treats
those cells when screenshots actually change hasn't been tested yet — verify
with one screenshot before trusting it for a full set, and update this note
with what's learned. Screenshots themselves, their captions and order are also
described in [screenshots/README.md](screenshots/README.md). Every claim in
the copy is traced in [claim-sources.md](claim-sources.md); fix the CSV, this
file's prose copy, and that table together when behaviour changes.

## Known at the time of this release

- **Issue #281 is closed** (PR #369) — family photos and attachments now
  survive a GEDCOM export/reimport round trip, via a GT4 extension record
  (`_FAML`). `claim-sources.md`'s exclusion for this has been widened
  accordingly; the listing copy now states it.
- **The `.gt4` MSIX file-association is confirmed working**, verified
  2026-09-19 against the real packaged build (`4.0.688.0`, installed via
  `install-release.ps1`): double-clicking a `.gt4` file offered "Genealogy
  Tree" in Windows' app picker and it opened the project correctly when
  chosen. (A second double-click opened SQLiteBrowser instead — expected,
  since the first pick was "Just once," not "Always," so Windows never set
  GT4 as the default handler; that's this dev machine's pre-existing default
  for `.gt4`, not a GT4 defect.) The listing's "double-click to open" and
  ".gt4 export/import" claims stand as written.
- **Double-clicking a second `.gt4` while the app is already running opens a
  second instance.** Deliberate, per PR #384 — single-instance redirection was
  out of scope.
- **A hand-authored `_FAML` photo `OBJE` with a sub-tag GT4 doesn't model (e.g.
  `TITL`) drops that sub-tag on import.** Deliberate gap noted in PR #369: GT4's
  own export never produces this shape, since family photo categories have no
  `*Tagged` counterpart to carry it.
- **Issue #370** (a Windows `STATUS_STOWED_EXCEPTION` crash on regaining focus)
  is only partly fixed. PR #372 fixed the one reproduction path it targeted
  (thumbnail decoding off the UI thread); the issue stays open for other
  triggers of the same crash class.

The screenshots are not among these — the set was fully re-shot for this
release (11 shots, home screens dropped); see
[screenshots/README.md](screenshots/README.md) for what changed and why.

The palette needs no inventory here any more. #358 and #360 cleared the
last of the MAUI template colours, and what replaced them is pinned by the
`*PaletteTests` in `Tests/GT4.UI.App.DeviceTests`: they resolve each token
against a real `UserAppTheme`, composite it over its ground — the palette is
authored as ink at an alpha, so a ratio taken from a token describes a colour
that never reaches the screen — and assert 4.5:1 on text, 3:1 on everything
else. A regression fails the device-test leg instead of waiting to be spotted in
a screenshot.
