[CmdletBinding()]
param(
    [string] $EditorDirectory = (Join-Path $PSScriptRoot '..\test-host\App\PDF-XChange\PDF Editor')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editorPath = [IO.Path]::GetFullPath($EditorDirectory)
$mainPath = [IO.Path]::GetFullPath((Join-Path $projectPath '..\App\PDF-XChange\PDF Editor'))
if ($editorPath -eq $mainPath) { throw 'Use an isolated editor copy for host tests.' }
$executable = Join-Path $editorPath 'PXCEditor.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Isolated test editor is missing.' }
if (@(Get-Process -Name PXCEditor -ErrorAction SilentlyContinue |
    Where-Object Path -EQ $executable).Count -ne 0) {
    throw 'Close the isolated test editor before preparing plugins.'
}
$plugins = Join-Path $editorPath 'Plugins.x64'
$stage = Join-Path $projectPath 'obj\ocr-host'
New-Item -ItemType Directory -Path $plugins, $stage,
    (Join-Path $projectPath 'output\ocr-validation') -Force | Out-Null
foreach ($name in @('ClickCircle.Core.dll', 'PDFXEdit.dll', 'ClickCircle.pvp')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "dist\$name") -Destination $plugins -Force
}
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $framework 'csc.exe') /nologo /target:library /platform:x64 /optimize+ `
    "/out:$(Join-Path $plugins 'OcrHostTests.dll')" `
    "/reference:$(Join-Path $plugins 'PDFXEdit.dll')" `
    "/reference:$(Join-Path $plugins 'ClickCircle.Core.dll')" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    (Join-Path $PSScriptRoot 'OcrHostTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Host test compilation failed.' }
$bootstrap = [IO.File]::ReadAllText((Join-Path $projectPath 'tools\ClickCircle.Bootstrap.il')).
    Replace('ClickCircle.Bootstrap', 'OcrHostTests.Bootstrap').
    Replace('ClickCircle.pvp', 'OcrHostTests.pvp').
    Replace('ClickCircleBootstrap', 'OcrHostTestsBootstrap').
    Replace('ClickCircle.Core.dll', 'OcrHostTests.dll').
    Replace('ClickCircle.Plugin', 'ClickCircleOcrTests.HostPlugin')
$bootstrapPath = Join-Path $stage 'OcrHostTests.il'
[IO.File]::WriteAllText($bootstrapPath, $bootstrap)
& (Join-Path $framework 'ilasm.exe') /nologo /dll /x64 `
    "/output:$(Join-Path $plugins 'OcrHostTests.pvp')" $bootstrapPath
if ($LASTEXITCODE -ne 0) { throw 'Host test bootstrap assembly failed.' }
$oldData = $env:CLICKCIRCLE_DATA_DIR
$oldRoot = $env:CLICKCIRCLE_OCR_TEST_ROOT
try {
    $env:CLICKCIRCLE_DATA_DIR = Join-Path $projectPath 'output\ocr-validation\data'
    $env:CLICKCIRCLE_OCR_TEST_ROOT = $projectPath
    [IO.File]::WriteAllText((Join-Path $projectPath 'output\ocr-validation\ocr-host-tests.txt'), 'PENDING')
    $process = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru `
        -ArgumentList ('"{0}"' -f (Join-Path $PSScriptRoot 'ClickCircle_ocr_selection.pdf'))
    Write-Host "Started isolated OCR host PID=$($process.Id)"
}
finally {
    $env:CLICKCIRCLE_DATA_DIR = $oldData
    $env:CLICKCIRCLE_OCR_TEST_ROOT = $oldRoot
}
