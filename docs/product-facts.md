# Renumber — product facts

Plain-text source of truth for **all** product copy: the website, a future
Autodesk App Store listing, and anything else. No HTML, no store-specific
formatting — each consumer styles this its own way.

- When a fact changes, change it **here first**, then re-derive the rest.
- Technical facts here are read from the code, not written from memory:
  `version.json`, `branding.props`, `Renumber/App.cs`,
  `Renumber/Core/RenumberPlanner.cs`, `Renumber/Core/NumberAllocator.cs`,
  `Renumber/Core/NumberFormat.cs`, `Renumber/Core/NumberPattern.cs`,
  `Renumber/Forms/RenumberWindow.cs`, `Installer/Product.wxs`.

---

## Identity

- **Name:** Renumber
- **Publisher:** Vixeldorf
- **Category:** Revit add-in — documentation / tagging, any discipline
- **Current version:** 1.0.0
- **Platform:** Autodesk Revit 2024 through 2027 (Windows, 64-bit)
- **Interface language:** English
- **Website:** https://www.vixeldorf.com
- **Support:** yoav@vixeldorf.com, answered within 5 business days
- **Source:** https://github.com/ioavant/Renumber

## One-liner

Mark every fan, unit and fixture in one pass — F-01, F-02, F-03 — with a preview
before anything is written.

## Short description (one sentence)

Renumber writes sequential marks such as F-01, F-02 into a text parameter of
every family instance in a category, fills only the empty ones when you add
equipment later, and can switch existing marks to a new format (F02 → F-02)
without touching their numbers.

## What it does

Pick a category — Mechanical Equipment and Mark by default — tick the families,
and type the format by example: prefix `F-`, start `01`, step `1`. The window
lists every element in scope with its current value and the value it is about to
get, so the whole run can be checked before a single parameter changes. One
button writes it, in one transaction: one Ctrl+Z undoes all of it.

Projects grow. When new equipment is added, Renumber does not have to reshuffle
the marks that are already on the drawings: "only empty" modes number just the
new elements — after the highest number in use, or into the gaps left by deleted
ones. A copy-pasted element that carries its original's mark is caught as a
duplicate and gets a number of its own; the original keeps its mark.

Marks already typed in another format do not have to be retyped. Renumber reads
the formats in use straight off the model — "F## ×24, AHU-# ×3" — and changes
one of them into another while every element keeps its number: F02 becomes
F-02, F17 becomes F-17, AHU-3 stays as it is.

## Feature bullets

- **Any category, any text parameter** — Mark by default; any writable text
  instance parameter of the chosen families is offered
- **Format by example** — prefix, start, step, suffix; start `01` pads to two
  digits (F-01 … F-99, F-100), start `1` does not pad
- **Live preview** — current value, new value and status for every element,
  colour-coded: new, unchanged, skipped, conflict
- **Four modes:**
  - Renumber all — every element in scope, from the start, in order
  - Only empty — continue after the last number
  - Only empty — fill gaps first
  - Change format — keep the numbers (F02 → F-02)
- **Detects the formats already in use** — one click takes "F##" as the format
  to number in, or as the old format to replace
- **Duplicates handled** — the oldest element keeps a repeated number, copies
  can be renumbered
- **Reads marks the way people do** — F-7, f-07 and F-07 are the same mark
- **Four orders** — creation order; current number (closes gaps without
  reshuffling); by level, then rows; by level, then columns
- **Three scopes** — whole project, visible in the active view, current
  selection (preselected automatically when something is selected)
- **Conflicts flagged, not hidden** — a new mark that repeats one kept by an
  element outside the scope or in an unticked family is shown in orange
- **Safe in workshared models** — elements borrowed by another user are
  skipped and named, never forced
- **One Undo step** for the whole run
- **Remembers the last settings** per Windows user

## How it works (the mechanic worth explaining once)

Revit checks Mark for duplicates across a whole category, so Renumber does too:
the numbers counted as taken come from every instance of the category, not just
the families being numbered. Numbers are then handed out on the grid
start, start + step, start + 2·step … skipping every taken one. Which part of the
grid is used depends on the mode: from the start, after the highest number in
use, or the first free slots.

Changing the format is a separate path that never renumbers: each value is read
in the old format, and the same number is written in the new one. Values that
do not match the old format — empty ones, or another series such as AHU-1 — are
left exactly as they are.

## Who it is for

MEP and architectural teams who mark equipment, fixtures, doors or anything else
on drawings and in schedules, and keep doing so as the model changes: numbering
the first time, numbering what was added since, and cleaning up after a
standard changes from F01 to F-01.

## Requirements

- Revit 2024, 2025, 2026 or 2027, 64-bit Windows
- A project document (not the Family Editor)

## Where data lives

- Marks are written into the chosen parameter of each element — nothing else is
  added to the model: no shared parameters, no Extensible Storage
- The last settings used are kept per Windows user in
  `%AppData%\Vixeldorf\Renumber\settings.json`

## What it never does

- Writes nothing until "Renumber" is pressed
- Never touches values in another format in the "only empty" and "change
  format" modes
- Never numbers nested shared components separately — they belong to their host
- Never edits elements borrowed by another user

## Install / uninstall

The MSI installs the add-in to `%ProgramData%\Vixeldorf\Renumber\` (Revit 2024
loads the .NET Framework build, 2025 and later the .NET 8 build from the `net8`
subfolder) and registers it for each Revit version ticked in the installer:
`%ProgramData%\Autodesk\Revit\Addins\<year>` for 2024–2026, and
`%ProgramFiles%\Autodesk\Revit\Addins\2027` for 2027, which no longer reads
ProgramData. The command appears on the **Vixeldorf** tab, **Renumber** panel.

Uninstall from Windows Settings → Apps (or Programs and Features); that removes
the add-in and every manifest.

## Known limitations

- Members of model groups: Revit may refuse to change a parameter of a group
  member outside group editing. Such elements are reported as failures; the
  rest of the run is still written.
- In "Renumber all", a conflict with a mark kept outside the scope is written as
  shown; Revit will then show its own duplicate-mark warning.
- The number is the last run of digits in a value: in "AHU-1-03" it is 3, and
  the prefix is "AHU-1-".
- Change format keeps duplicates as duplicates — fix them afterwards with an
  "only empty" mode and "Treat duplicates as empty".

## Assets

- Ribbon icons: `Renumber/Resources/renumber_16.png`, `renumber_32.png`
- Large icon for the site / store: `Renumber/Resources/renumber_256.png`

## Version history

- **1.0.0** — First release. Numbering by category, family and text parameter
  with live preview; renumber all / continue after the last / fill gaps; format
  detection and format change that keeps the numbers (F02 → F-02); ribbon
  button on the Vixeldorf tab; update notice in the window; MSI for Revit
  2024–2027.
