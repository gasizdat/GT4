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

`release/rc-sep-04` is the first to carry a commit of its own — this `doc/store`
directory. Note the consequence: the build number is `git rev-list --count HEAD`,
so that extra commit raises it by one over the master commit being shipped. Two
release branches cut from the same master tip with a commit each would therefore
claim the same version for different content. If that ever matters, put the
documents on master instead.

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
here, and the log line was the one that looked right.

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

Then run the [Windows App Certification Kit](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit)
against the same package. It catches the manifest and packaging faults that
otherwise come back as a certification failure days later.

If you package locally to check any of this, delete
`obj\AppWinOnly\Release\...\resizetizer\m\Package.appxmanifest` first. Its
up-to-date check watches the source manifest, not the derived version, so an
incremental build happily re-stamps a package with the version from the last
one. CI is unaffected — it starts from an empty `obj`.

## Submitting to the Store

Partner Center walks five sections. Ours, and what still needs a decision:

**Pricing and availability** — free; markets; visibility. No blockers.

**Properties**
- Category: Lifestyle (or Productivity). Pick one and keep it.
- Privacy policy URL: [privacy-policy.md](privacy-policy.md) is the text; it needs
  a public URL before it can go in the form.
- Support contact: an address the developer is willing to publish.
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

The package declares **`EN-US` only** — read out of a locally built
`AppWinOnly_4.0.646.0_x64.msix`. The manifest asks for
`<Resource Language="x-generate" />`, and the PRI build finds one language,
because the five `UIStrings*.resx` files become .NET satellite assemblies, which
MRT does not see. The app really does offer Russian, German, Spanish and French —
the user picks them in Settings — but the Store will list English only unless
package-level language resources are added. Say so in the listing copy (it does)
rather than expecting Partner Center to show all five.

**Store listings** — copy from
[microsoft-store-listing-en.md](microsoft-store-listing-en.md); screenshots and
their captions and order from [screenshots/README.md](screenshots/README.md).
Every claim in the copy is traced in [claim-sources.md](claim-sources.md); fix
both files together when behaviour changes.

## Known at the time of this release

- **Issue #281** — a GEDCOM export followed by a re-import silently loses family
  photos and attachments (person media survives). The listing copy is worded to
  avoid promising otherwise; see the exclusion in `claim-sources.md`.
- **The dark-theme screenshots predate the palette.** `PrimaryDark` was the stock
  MAUI purple until this release; buttons are green now, so
  `12-home-dark.png` and `13-family-tree-dark.png` no longer show the shipping
  app. `01-home.png` and `12-home-dark.png` additionally print the version on
  screen in the old `4.0.0.645` layout. Re-shoot or omit those three.
- **Still template purple**, at far lower visibility: `Secondary` /
  `SecondaryDarkText` behind the adorner buttons' hover state, and `Tertiary` on
  the emoji adorners. The orange button hover (`PrimaryButtonHover` `#EE5511`)
  is *not* a leftover — commit `775dcab1` added it deliberately.
- **Android's `colorPrimaryDark`** is the same purple, in
  `Platforms/Android/Resources/values/colors.xml`. It is the status-bar colour,
  where the name means "a darker Primary", not "Primary's dark-theme sibling" —
  so it wants a *darker* green, not the value used here. Untouched: this release
  is Windows-only and it cannot be verified without a device.
