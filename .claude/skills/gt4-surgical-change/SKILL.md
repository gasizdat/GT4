---
name: gt4-surgical-change
description: GT4's additions to surgical-change - exact build and test commands, the comment sweep, and branch rules. Use with surgical-change before writing any change to product source in this repo.
---

# Surgical changes in GT4

Follow `surgical-change` for the method. This adds what's specific to GT4.

- The code path to read end-to-end runs UI → manager → table.

## Verify like CI does

- Tests: extend the existing test class for the touched page/component, reusing its harness helpers (`ReloadPersonsAsync`, `WaitForFamiliesAsync`, `ModalDialogHarness`).
- Comments: sweep the ones you added, scoped to the code you touched — classify and act per `comments-sweep`.
- Build both CI filters in Release (device tests refuse Debug): `dotnet build GT4.CI.slnf -c Release`, then `dotnet build GT4.CI.Windows.slnf -c Release --framework net10.0-windows10.0.19041.0`.
- Run the affected test classes first (`--filter "FullyQualifiedName~<Class>Tests"`), then every test project in `GT4.CI.slnf` with `dotnet test <project> --no-build -c Release`, and the device tests with `dotnet test Tests\GT4.UI.App.DeviceTests\GT4.UI.App.DeviceTests.csproj --no-build -c Release --framework net10.0-windows10.0.19041.0`. A nonzero device-test exit is the known WinUI host-teardown race only when the runner printed its completion line ("Test run complete." in CI's English output) and the TRX records no failures; otherwise the host died mid-run, so rerun. `ci.yml`'s device-test step applies the same rule.

## Branching

- `git fetch` first (local `master` goes stale), and never stack a branch on an unmerged one. If a change genuinely cannot sit on `master`, discuss it with the user before branching. A `refactor-sweep` sweep counts as one item.
