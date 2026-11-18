[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $EditorDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'App\PDF-XChange\PDF Editor')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$editorPath = [IO.Path]::GetFullPath($EditorDirectory)
$executablePath = Join-Path $editorPath 'PXCEditor.exe'
$pluginPath = Join-Path $editorPath 'Plugins.x64'
$manifestPath = Join-Path $pluginPath 'ClickCircle.install.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'No ClickCircle install manifest was found. Nothing was removed.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Product -ne 'ClickCircle') { throw 'Unexpected install manifest.' }
$allowedNames = @('ClickCircle.pvp', 'ClickCircle.Core.dll', 'PDFXEdit.dll')
$filesToRemove = @()
foreach ($entry in $manifest.Files) {
    if ($entry.Name -notin $allowedNames) { throw 'Unexpected filename in install manifest.' }
    $filePath = Join-Path $pluginPath $entry.Name
    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "Installed file changed; nothing removed: $filePath"
        }
        $filesToRemove += $filePath
    }
}
$running = @(Get-Process -Name PXCEditor -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $executablePath })
if ($running.Count -gt 0) {
    throw 'Save your PDFs and close this editor before uninstalling. No process was stopped.'
}
if ($PSCmdlet.ShouldProcess($pluginPath, 'Remove the ClickCircle plugin files and install manifest')) {
    foreach ($filePath in $filesToRemove) { Remove-Item -LiteralPath $filePath }
    Remove-Item -LiteralPath $manifestPath
    Write-Host 'ClickCircle removed. Saved size settings and PDF documents were preserved.'
}
