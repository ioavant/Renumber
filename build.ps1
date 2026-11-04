<#
.SYNOPSIS
    Builds Vixeldorf Renumber (both target frameworks) and, unless -NoInstaller,
    the MSI.

.DESCRIPTION
    Two assemblies, not one per Revit year:

        net48           -> Revit 2024,  compiled against the installed Revit 2024 API
        net8.0-windows  -> Revit 2025+, compiled against the Revit 2025.0
                           reference assemblies from NuGet

    The net8 half deliberately does NOT reference the locally installed Revit
    2025: since 2025.5 that assembly is itself built against .NET 10, which a
    net8 project cannot reference (CS1705), and a DLL built against it would not
    load in 2025.0-2025.4.

    The installer needs full-framework MSBuild (WiX v3 does not build under
    dotnet). Its project reference only orders the net48 half, so the add-in is
    built for both frameworks first.

    The version comes from version.json; nothing else needs editing.

.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -Configuration Debug -NoInstaller
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoInstaller
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$brand = 'Vixeldorf'

$project = Join-Path $root 'Renumber\Renumber.csproj'
$installer = Join-Path $root 'Installer\Installer.wixproj'

Write-Host "Config : $Configuration" -ForegroundColor DarkGray

Write-Host "`n=== Add-in (net48 + net8.0-windows) ===" -ForegroundColor Cyan
& dotnet build $project -c $Configuration -v minimal --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$msi = $null
if (-not $NoInstaller) {
    Write-Host "`n=== Installer (WiX v3) ===" -ForegroundColor Cyan
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) { throw 'Full-framework MSBuild not found (Visual Studio or Build Tools are needed for WiX v3).' }

    & $msbuild $installer /p:Configuration=$Configuration /p:Platform=x64 /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed (exit $LASTEXITCODE)." }
    $msi = Join-Path $root "Installer\bin\$Configuration\$brand\Renumber_Setup.msi"
}

Write-Host "`n=== Output ===" -ForegroundColor Cyan
$bin = Join-Path $root "Renumber\bin\$Configuration\$brand"
foreach ($row in @(@('Revit 2024 ', 'net48', 'Renumber.dll'), @('Revit 2025+', 'net8 ', 'net8\Renumber.dll'))) {
    $dll = Join-Path $bin $row[2]
    Write-Host ("  {0}  {1}  {2,8:N0} bytes  {3}" -f $row[0], $row[1], (Get-Item $dll).Length, $dll)
}
if ($msi) {
    Write-Host ("  Installer          {0,8:N0} bytes  {1}" -f (Get-Item $msi).Length, $msi)
}
