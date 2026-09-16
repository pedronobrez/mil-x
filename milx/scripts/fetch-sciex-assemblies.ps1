# Fetches the SCIEX WIFF Reader Distributable SDK assemblies (Clearcore2.*) that the native .wiff
# reader links against, on Windows. The same thing fetch-sciex-assemblies.sh does elsewhere.
#
# They are redistributed by the open-source alpharaw package (MannLabs) under SCIEX's "WIFF Reader
# Distributable Beta SDK" redistribution licence, which is copied next to them
# (SCIEX_LICENSE.txt). They are NOT in this repository, and they are not in any MIL-X release:
# reading that licence is the user's to do, not ours to do for them.
#
#   .\scripts\fetch-sciex-assemblies.ps1                     -> milx\vendor\sciex   (building from source)
#   .\scripts\fetch-sciex-assemblies.ps1 -Dest "C:\Program Files\MIL-X\plugins\sciex"
#                                                            -> an installed application
#   .\scripts\fetch-sciex-assemblies.ps1 -From D:\sdk         -> copy from a folder you already have
#
# An installed MIL-X reads .wiff natively as soon as these files are in its plugins\sciex
# folder; nothing else has to be installed, not Analyst and not ProteoWizard. Without them .wiff
# goes through msconvert and the application says so.
[CmdletBinding()]
param(
    [string] $Dest,
    [string] $From
)

$ErrorActionPreference = 'Stop'

if (-not $Dest) {
    $root = Split-Path -Parent $PSScriptRoot
    $Dest = Join-Path $root 'vendor\sciex'
}
New-Item -ItemType Directory -Force -Path $Dest | Out-Null

function Copy-Sdk([string] $source) {
    Copy-Item -Path (Join-Path $source '*.dll') -Destination $Dest -Force
    Get-ChildItem -Path $source -Filter '*.txt' -ErrorAction SilentlyContinue |
        Copy-Item -Destination $Dest -Force
}

if ($From -and (Test-Path (Join-Path $From 'Clearcore2.Data.dll'))) {
    Copy-Sdk $From
    Write-Host "[sciex] copied from $From"
}
else {
    $python = (Get-Command python -ErrorAction SilentlyContinue) ?? (Get-Command py -ErrorAction SilentlyContinue)
    if (-not $python) {
        throw "Python is needed to fetch the SDK (it is published as a Python wheel). Install it, or pass -From with a folder that already holds Clearcore2.Data.dll."
    }

    # an installed alpharaw already carries them
    $local = & $python.Source -c "import alpharaw, os; print(os.path.join(os.path.dirname(alpharaw.__file__), 'ext', 'sciex'))" 2>$null
    if ($local -and (Test-Path (Join-Path $local 'Clearcore2.Data.dll'))) {
        Copy-Sdk $local
        Write-Host "[sciex] copied from the installed alpharaw: $local"
    }
    else {
        $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("sciex-" + [System.Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null
        try {
            Write-Host '[sciex] downloading the alpharaw wheel (no dependencies) ...'
            & $python.Source -m pip download alpharaw --no-deps --only-binary=:all: -d $tmp | Out-Null
            $wheel = Get-ChildItem -Path $tmp -Filter 'alpharaw-*.whl' | Select-Object -First 1
            if (-not $wheel) { throw 'pip did not produce an alpharaw wheel.' }
            $unzipped = Join-Path $tmp 'unzipped'
            Expand-Archive -Path (($wheel.FullName -replace '\.whl$', '.zip') | ForEach-Object {
                Copy-Item $wheel.FullName $_ -Force; $_
            }) -DestinationPath $unzipped -Force
            Copy-Sdk (Join-Path $unzipped 'alpharaw\ext\sciex')
            Write-Host "[sciex] extracted from $($wheel.Name)"
        }
        finally {
            Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
        }
    }
}

$count = (Get-ChildItem -Path $Dest -File).Count
Write-Host "[sciex] $count files in $Dest"
Write-Host "[sciex] read the licence before redistributing: $(Join-Path $Dest 'SCIEX_LICENSE.txt')"
