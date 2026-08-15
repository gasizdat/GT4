# Navigation

An index from "what are you trying to do" to the doc that covers it, plus a glossary of terms that
recur across the docs without being obvious from a type name alone. This page points; it doesn't
explain — follow the link for the actual content.

## By task shape

**Adding or changing a stored field on a person/family** →
[subsystems/core-project.md](subsystems/core-project.md) for the table/manager layer it lands in,
then [subsystems/core-gedcom.md](subsystems/core-gedcom.md) if the field should also round-trip
through GEDCOM import/export.

**Touching anything under a transaction, or writing code that runs concurrently with other
project access** → [subsystems/core-project.md](subsystems/core-project.md) (connection gate,
flow-affine transactions) is required reading before the change, not after.

**Adding a new kind of stored media, or changing how a photo/attachment is classified** →
[subsystems/core-gedcom.md](subsystems/core-gedcom.md) (photo vs. attachment classification on
import) and [subsystems/ui-app.md](subsystems/ui-app.md) (the same distinction on the display
side).

**Building a view that renders content whose source might be untrusted, remote, or simply not
fully known ahead of render time** →
[principles/rendering-inversion.md](principles/rendering-inversion.md) before writing the view;
`MarkdownView`/`InlineMediaProvider` is the worked example.

**Adding a new page, dialog, or a service other code needs to consume** →
[principles/dependency-injection.md](principles/dependency-injection.md) for how to declare the
dependency, [subsystems/ui-app.md](subsystems/ui-app.md) for the page-vs-dialog construction split.

**Writing background work, a lifecycle handler, or a command that can fail** →
[principles/async-and-teardown.md](principles/async-and-teardown.md) — `SafeTask`/`SafeCommand`
are required, not optional, for anything touching the project off the immediate call stack.

**Deciding where a new test belongs, or wondering why a test is slow/flaky** →
[principles/testing-strategy.md](principles/testing-strategy.md).

**Running a diagnostic against a project or GEDCOM file outside the app** →
[subsystems/tools-relativescli.md](subsystems/tools-relativescli.md).

**"Why are there three tiny utility projects instead of one?"** →
[subsystems/shared-libraries.md](subsystems/shared-libraries.md).

**How to actually make the change** (branch/commit conventions, verification commands, the
surgical-change discipline) → not this document set — see the repo's `.claude/skills/*` and
`CLAUDE.md`.

## Glossary

**Revision** — a per-project counter (`ProjectDocument.ProjectRevision`, carried on `ProjectInfo`)
incremented on every committed root transaction. Pages compare a snapshot of it against the current
value in `OnNavigatedTo` to decide whether to refresh; see
[subsystems/ui-app.md](subsystems/ui-app.md).

**`Person` / `PersonInfo` / `PersonFullInfo`** (`Core/Project/Dto/`) — three widening views of the
same person, each a record inheriting the previous: `Person` is the bare identity (id, dates,
sex — enough to key relative/data operations); `PersonInfo` adds `Names` and `MainPhoto` (enough
for a list row, `DisplayName`); `PersonFullInfo` adds everything else (additional photos,
relatives, biography, GEDCOM residue, attachments). Fetch the narrowest one that answers the
question — a full fetch materializes every blob for that person.

**`DataCategory`** (`Core/Project/Dto/DataCategory.cs`) — the tag on a stored `Data` row that
drives both GEDCOM classification (photo vs. attachment — see
[subsystems/core-gedcom.md](subsystems/core-gedcom.md)) and which `IDataConverter` handles it (see
[principles/dependency-injection.md](principles/dependency-injection.md)).

**Residue** — the verbatim, unmodeled remainder of a GEDCOM record, preserved so it survives an
import/export round-trip even though GT4's schema has no field for it. See
[subsystems/core-gedcom.md](subsystems/core-gedcom.md).

**Xref regeneration** — GEDCOM cross-reference ids (`@I1@`, `@F3@`, ...) are assigned fresh on
every export; nothing in GT4 preserves the original numbering. Anything that needs to find a
record again after re-export (residue lookup, comparison tooling) has to key on something other
than the xref — see the family-key discussion in
[subsystems/core-gedcom.md](subsystems/core-gedcom.md).

**`ElementId.NonCommittedId`** (`Core/Project/Dto/ElementId.cs`) — sentinel `Id` value (`0`)
meaning "not yet persisted," used on a `Data`/`Person`/etc. built in memory before its first write.

**Origin vs. cache file** — a project's on-disk file (`ProjectHost`) is opened by copying it to a
local cache; edits happen against the cache; the origin is only overwritten on close, and only if
the revision actually advanced. See [subsystems/core-project.md](subsystems/core-project.md).
