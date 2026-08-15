# Subsystem: Tools.RelativesCli

`GT4.Tools.RelativesCli` (`Tools/GT4.Tools.RelativesCli/Cli.cs`) is a diagnostic console app for
inspecting a project database or GEDCOM file without launching the MAUI app — see
[shared-libraries.md](shared-libraries.md) for why it's able to reference `Core.Project`/
`Core.Gedcom` directly with no MAUI runtime involved at all.

## What it's for

Not a general-purpose GT4 CLI — a narrow set of commands built to answer specific questions while
debugging, each mirroring app-layer logic closely enough to be a useful cross-check rather than a
reimplementation:

- **`tree <personId>`** walks the full relative tree from a person and flags `Loop`/
  `MultipleConnections` — deliberately the same detection the app's relative-tree expansion applies
  at runtime, so a suspicious tree can be inspected outside the app with the same diagnosis.
- **`compare <first.ged> <second.ged>`** (`Cli.RunCompareAsync`, backed by `GedcomComparer.cs`) is
  the mechanism behind the GEDCOM round-trip functional harness's *fidelity* check — see
  [core-gedcom.md](core-gedcom.md) and
  [principles/testing-strategy.md](../principles/testing-strategy.md). It compares what two GEDCOM
  files' tags say, not their text, since xrefs and `FAM` records regenerate on every export. Exit
  code `2` means "the files differ" (distinct from `1`, every other failure), `0` means they agree
  — the encoding the round-trip script's pass/fail check relies on.
- **`find`**, **`relatives`**, **`export`** are lighter lookups/dumps over an opened project or a
  freshly-imported GEDCOM file, useful for eyeballing state during a debugging session.

## Non-goals

- Not a stable or scripted-automation-facing interface beyond the round-trip harness's use of
  `compare` — flags and output format can change as debugging needs change.
- Not a substitute for the app's own relative-tree/GEDCOM logic — it calls the same `Core.Project`/
  `Core.Gedcom` APIs the app does, it doesn't reimplement them.
