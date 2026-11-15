# Google Play listing — Genealogy Tree (en-GB)

The default store listing language is English (United Kingdom), `en-GB`, which
Play picked when the app was created; the copy's British spelling ("centred",
"humour") matches it. The Play counterpart of [microsoft-store-listing-en.md](microsoft-store-listing-en.md).
Every claim in the copy is traced in [claim-sources.md](claim-sources.md); the
Android-specific limits that shaped it are listed under "What the copy leaves
out" below. Images live in [play/](play/README.md).

Play renders no Markdown or rich text in these fields, so the copy below is
plain text exactly as it is pasted: the capitalised lines are the section
headings, and the bullets are the literal `•` character.

## App details

**App name** (30 max): `Genealogy Tree` (14)

The launcher label under the icon on a phone is still `GT4`
(`ApplicationTitle` in `UI/App/AppCommon.props` is overridden to "Genealogy
Tree" for the Windows head only). Play doesn't require the two to match.

**Short description** (80 max, 80 used):

```
Your family tree, offline and private. GEDCOM in and out, photos, kinship tools.
```

**Full description** (4,000 max, 3,686 used):

```
Genealogy Tree keeps your family history where it belongs: on your own device. There is no account to create, no server to sync with and no subscription. A project is a single file you can copy, back up or move like any other document, and the app never sends anything about your family anywhere.

New here? Open the built-in demo tree, a small, real genealogy of the Brontë family with portraits and biographies, and look around before you type a single name. Already have research elsewhere? Import a GEDCOM file and your tree is there.

SEE HOW THE FAMILY FITS TOGETHER

The family tree draws ancestors and descendants around one person as a tidy pedigree: children centred under their parents, each family drawn once. Tap any relative to re-centre the tree on them. Pull in more generations upwards or downwards, switch siblings and cousins on or off, pinch to zoom from a whole-tree overview down to a single branch, and drag to pan.

The fan chart puts one person at the centre and each generation of ancestors in a ring around them, up to ten generations, drawn as a half, three-quarter or full circle. Press and hold someone on either chart to see a card with their photo, name and life dates.

A PAGE FOR EVERY PERSON

• Photos: a main portrait plus as many more as you like, each with its own caption, in a full-screen viewer.
• Biography: rich text with headings, lists and links, written with a formatting toolbar. Put photos from the project straight into the text.
• Attachments: scans, certificates, PDFs, audio, anything, each with its own caption.
• Relatives: an expandable tree of everyone connected to this person.

TOOLS THAT SAVE THE AFTERNOON

• Kinship finder: pick any two people and get the answer ("grand aunt", "cousin once removed") and the full chain of relatives that connects them.
• Date calendar: who was born, who married and who is remembered on any day of the year, with round anniversaries flagged as milestones.
• Date tools: convert a date between the Gregorian, Julian, Hebrew and French Republican calendars, or measure the time between two dates.
• Statistics: over twenty measures of your project, from lifespans and births by decade to the most common first names and where your data is thin.
• Filters: narrow any list by name (with * and ? wildcards), by sex or by year.
• Gallery: every photo and attachment in the project in one place, each linked back to its person or family.

BUILT FOR REAL GENEALOGICAL DATA

• Dates you actually have: "about 1816" and "unknown" are first-class values, not blanks you have to fake.
• GEDCOM 5.5.1 import and export. Tags the app doesn't model are carried through the round trip rather than dropped, and so are family photos and attachments.
• Share it as a website: export the whole project as a self-contained HTML site that opens in any browser without the app.
• Dates in your calendar: view every date in the Gregorian, Julian, Hebrew or French Republican calendar, and enter a date from an old record in its original calendar.
• Multiple projects, and restore points that keep earlier working copies of each.

YOURS TO READ COMFORTABLY

Light, dark or follow-the-system theme. Pinch to scale every piece of text in the app. Mark one person as the project's main person and the app opens straight to them next time. Read-only mode hides every editing action behind a single toggle. Available in English, Russian, German, Spanish and French.

YOUR DATA, YOUR DEVICE

No account system, no cloud sync, no analytics and no ads. Projects live in the GT4 folder of your Documents. The only time the app reaches the internet is when you put a web link or a web image into a biography and then open it.
```

### What the copy leaves out, and why

It is the Microsoft Store Description cut to fit 4,000 characters, with these
Android-specific changes:

- **No photo enhancement**, and no model download in the network sentence: the
  enhancements are registered under `#if WINDOWS` only (see the network section
  of [claim-sources.md](claim-sources.md)).
- **No right-click claims** (copying text, hiding someone from the tree): both
  are desktop context flyouts with no Android gesture.
- **Pinch instead of Ctrl + / − / 0**, and "press and hold" instead of hover for
  the person card.
- **No "your computer"** framing; "your own device" instead.
- **No arranging the tree by dragging people**: not checked on a phone.

## Graphics

| Asset | Play's rule | File |
|---|---|---|
| App icon | 512 × 512, 32-bit PNG, ≤ 1 MB | [play/icon-512.png](play/icon-512.png) |
| Feature graphic | 1024 × 500, PNG or JPEG, no alpha | [play/feature-graphic.png](play/feature-graphic.png) |
| Phone screenshots | 2–8, each side 320–3840 px, long side ≤ 2 × short side | `play/phone-*.png`, see [play/README.md](play/README.md) |

## Main store listing contacts

- Email: `gasizdat@gmail.com` (the address the privacy policy already publishes)
- Website: `https://gasizdat.github.io/GT4/`
- Privacy policy: `https://gasizdat.github.io/GT4/privacy.html`

## Play Console answers ("App content")

The honest answers, derived from the manifest and the privacy policy rather
than from what similar apps declare. Re-check them whenever the manifest's
permissions or the network claim in [claim-sources.md](claim-sources.md) change.

**Privacy policy** — the URL above. Its "On Android" section was rewritten for
this release (no storage permission, PR #621).

**Sign-in details** (formerly "App access") — all functionality is available
without any access restrictions. No account or sign-in exists.

**Ads** — "No, my app does not contain ads."

**Content rating** (IARC questionnaire) — category "All other app types". No
violence, fear, sexual content, crude humour, profanity, drugs, alcohol, tobacco
or gambling. Users cannot talk to or share content with other users inside the
app; an export goes to the system share sheet, not to other users of the app.
No location sharing, no digital purchases, no built-in web browser (a biography
link opens the system browser), not primarily news or education. The ratings
issued on 15 November 2026: ESRB Everyone, PEGI 3, USK all ages, ClassInd all
ages, 3+ elsewhere, no content descriptors — matching the Store's 3+.

**Target audience and content** — 13–15, 16–17 and 18 and over. The app suits
any age, but naming an under-13 group brings the app under Play's Families
policy, which adds review requirements and buys nothing here. 18 and over alone
is not simpler: the form warns that it can restrict availability, which would
shut out a teenager doing a family-history project. With no under-13 group, Play
skips the rest of the form.

**Data safety** — "Does your app collect or share any of the required user data
types?" **No.** Play counts data as collected when the app transmits it off the
device. GT4 never does: projects, settings and crash logs stay local, and
nothing uploads them. The two network paths on Android are both started by the
user and go straight to the address the user put into a biography: a web image
fetched for display and a link handed to the browser. Neither carries project
data. (The Windows-only model download doesn't exist on Android.)

**Advertising ID** — "No." The manifest declares no `AD_ID` permission (read
back from the bundle with bundletool).

**Government apps, financial features, health, news** — none apply.

**Store settings** — category **Books & Reference**, matching the Store's
Books + reference. Tags: Books & reference, Lifestyle, Notebook (Play has no
genealogy tag; Privacy & security would file the app with VPNs and password
managers). Contact details as above, with no phone number, since Play shows it
publicly.

## Releases

**Build and upload**: see "Google Play" in
[release-and-submission.md](release-and-submission.md).

**Release notes** (500 max) for the first release. The tag must name a store
listing language: 855 went out tagged `en-US`, which the `en-GB`-only listing
doesn't have, so use `en-GB` from the next release on:

```
<en-GB>
First release on Google Play. Build your family tree on your phone, explore the Brontë demo tree, import and export GEDCOM, and keep every project as a file on your own device. No ads, no payments, no account — just your family history.
</en-GB>
```

Two warnings appear on every release and need nothing: no deobfuscation file
(the build doesn't run R8, so there is no mapping to upload) and no native debug
symbols (the native code is the .NET runtime's, and .NET produces no symbols
file in the form Play wants).

**Testing before production.** A personal developer account created after
13 November 2023 must run a closed test with at least 12 testers who stay opted
in for 14 consecutive days before it can apply for production access. Internal
testing (up to 100 testers) is available immediately but doesn't count toward
that.

The closed test ("Closed testing - Alpha") is open in all countries and draws
its testers from the Google Group `gt4-testers@googlegroups.com`: anyone can
join, only owners can post, only managers can see the member list. Feedback
goes to `https://github.com/gasizdat/GT4/issues`. A tester joins the group with
the account their phone's Play Store uses, then opens the track's opt-in link,
which works only once Google has approved the release. Version code 855 went to
review for it on 15 November 2026.

**Play App Signing.** On the first upload, accept the Google-generated app
signing key. The key that signs the bundle locally is then only the upload key;
if it is lost, Play Console support can register a new one. Both certificates
are under **Protected with Play → Play Store protection → Play app signing**:
the upload key certificate's SHA-256 is
`E4:34:28:87:B7:E4:B2:C6:21:B9:0C:A1:09:90:7B:E1:16:B9:6C:99:AB:D4:DC:6A:A3:9A:AD:9F:AF:55:97:ED`
(`gt4-upload.jks`), and the app signing key's, the one installed apps carry, is
`A0:A7:1B:BF:78:8D:56:EE:E8:6A:13:EF:8C:25:3B:0E:F2:65:32:60:52:B7:41:8A:A2:7E:05:9B:D1:B0:49:05`.
