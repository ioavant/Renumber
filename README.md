# Renumber

A Revit add-in by [Vixeldorf](https://www.vixeldorf.com) that numbers the family
instances of a category into a text parameter — `F-01`, `F-02`, … — with a live
preview of every element's new value before anything is written.

- **Any category, any text parameter** (Mark by default), chosen families only
- **Format by example**: prefix, start (`01` pads to two digits), step, suffix
- **Modes**: renumber all · only empty, after the last number · only empty, fill
  gaps first · **change format, keep the numbers** (`F02` → `F-02`)
- **Detects the formats already in use** (`F## ×24, AHU-# ×3`) and takes one with a click
- Duplicates, conflicts and elements borrowed by other users are shown, not hidden
- The whole run is one Undo step

Revit 2022–2027. Download the installer from the
[latest release](https://github.com/ioavant/Renumber/releases/latest).
The add-in appears on the **Vixeldorf** tab → **Renumber** panel.

Full product description: [docs/product-facts.md](docs/product-facts.md).

## Building

Needs Revit 2022 installed (the net48 half compiles against it), the .NET 8 SDK,
and for the installer Visual Studio / Build Tools with the WiX Toolset v3, plus
the shared `_branding` folder one level above this repository.

```powershell
.\build.ps1                     # Release DLLs + Installer\bin\Release\Vixeldorf\Renumber_Setup.msi
.\build.ps1 -Configuration Debug -NoInstaller
```

The version lives only in `version.json`.
