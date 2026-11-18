[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $EditorDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'App\PDF-XChange\PDF Editor'),
    [string] $SourceDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$editorPath = [IO.Path]::GetFullPath($EditorDirectory)
$sourcePath = [IO.Path]::GetFullPath($SourceDirectory)
$executablePath = Join-Path $editorPath 'PXCEditor.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Editor executable not found: $executablePath"
}
$pluginPath = Join-Path $editorPath 'Plugins.x64'
$manifestPath = Join-Path $pluginPath 'ClickCircle.install.json'
$names = @('ClickCircle.pvp', 'ClickCircle.Core.dll', 'PDFXEdit.dll')
$oldManifest = $null
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    $oldManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($oldManifest.Product -ne 'ClickCircle') { throw 'Unexpected install manifest.' }
}

$entries = @()
$changes = @()
foreach ($name in $names) {
    $sourceFile = Join-Path $sourcePath $name
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Build output not found: $sourceFile"
    }
    $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
    $destinationFile = Join-Path $pluginPath $name
    if (Test-Path -LiteralPath $destinationFile -PathType Leaf) {
        $destinationHash = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            $ownedEntry = @()
            if ($null -ne $oldManifest) {
                $ownedEntry = @($oldManifest.Files | Where-Object { $_.Name -eq $name -and $_.SHA256 -eq $destinationHash })
            }
            if ($ownedEntry.Count -ne 1) {
                throw "Existing file is not a matching ClickCircle install; nothing changed: $destinationFile"
            }
            $changes += $name
        }
    }
    else { $changes += $name }
    $entries += [ordered]@{ Name = $name; SHA256 = $sourceHash }
}

if ($changes.Count -gt 0) {
    $running = @(Get-Process -Name PXCEditor -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $executablePath })
    if ($running.Count -gt 0) {
        throw 'Save your PDFs and close this editor before installing. No process was stopped and no files were changed.'
    }
}

if ($PSCmdlet.ShouldProcess($pluginPath, 'Install the three ClickCircle plugin files')) {
    New-Item -ItemType Directory -Path $pluginPath -Force | Out-Null
    foreach ($name in $changes) {
        Copy-Item -LiteralPath (Join-Path $sourcePath $name) -Destination (Join-Path $pluginPath $name) -Force
    }
    [ordered]@{
        Product = 'ClickCircle'
        InstalledAt = (Get-Date).ToString('o')
        Files = $entries
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    Write-Host "ClickCircle installed: $pluginPath"
    Write-Host 'Open or restart the editor after saving your work to load the plugin.'
}
