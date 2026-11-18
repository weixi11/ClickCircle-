[CmdletBinding()]
param(
    [string] $EditorDirectory = (Join-Path $PSScriptRoot '..\box-test-host\App\PDF-XChange\PDF Editor'),
    [string] $DrawingPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editorPath = [IO.Path]::GetFullPath($EditorDirectory)
$mainEditorPath = [IO.Path]::GetFullPath((Join-Path $projectPath '..\App\PDF-XChange\PDF Editor'))
if ($editorPath -eq $mainEditorPath) { throw 'Use an isolated editor copy for host tests.' }
$executablePath = Join-Path $editorPath 'PXCEditor.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'Isolated editor executable is missing.' }
$running = @(Get-Process -Name PXCEditor -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executablePath })
if ($running.Count -ne 0) { throw 'Close the isolated test editor before preparing its plugins.' }
$pluginPath = Join-Path $editorPath 'Plugins.x64'
$stagePath = Join-Path $projectPath 'obj\shx-host'
New-Item -ItemType Directory -Path $pluginPath, $stagePath -Force | Out-Null
foreach ($name in @('ClickCircle.pvp', 'ClickCircle.Core.dll', 'PDFXEdit.dll')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "dist\$name") -Destination $pluginPath -Force
}
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $frameworkPath 'csc.exe') /nologo /target:library /platform:x64 /optimize+ `
    "/out:$(Join-Path $pluginPath 'ShxHostTests.dll')" `
    "/reference:$(Join-Path $pluginPath 'PDFXEdit.dll')" `
    "/reference:$(Join-Path $pluginPath 'ClickCircle.Core.dll')" `
    /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'ShxHostTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Failed to compile host tests.' }
$bootstrap = [IO.File]::ReadAllText((Join-Path $projectPath 'tools\ClickCircle.Bootstrap.il')).
    Replace('ClickCircle.Bootstrap', 'ShxHostTests.Bootstrap').
    Replace('ClickCircle.pvp', 'ShxHostTests.pvp').
    Replace('ClickCircleBootstrap', 'ShxHostTestsBootstrap').
    Replace('ClickCircle.Core.dll', 'ShxHostTests.dll').
    Replace('ClickCircle.Plugin', 'ClickCircleTests.HostPlugin')
$bootstrapPath = Join-Path $stagePath 'ShxHostTests.il'
[IO.File]::WriteAllText($bootstrapPath, $bootstrap)
& (Join-Path $frameworkPath 'ilasm.exe') /nologo /dll /x64 `
    "/output:$(Join-Path $pluginPath 'ShxHostTests.pvp')" $bootstrapPath
if ($LASTEXITCODE -ne 0) { throw 'Failed to assemble the test-only plugin.' }
$testDataPath = Join-Path $projectPath 'output\shx-test-data'
New-Item -ItemType Directory -Path $testDataPath -Force | Out-Null
$oldData = $env:CLICKCIRCLE_DATA_DIR
$oldRoot = $env:CLICKCIRCLE_SHX_TEST_ROOT
$oldDrawing = $env:CLICKCIRCLE_SHX_TEST_DRAWING
try {
    $env:CLICKCIRCLE_DATA_DIR = $testDataPath
    $env:CLICKCIRCLE_SHX_TEST_ROOT = $projectPath
    $env:CLICKCIRCLE_SHX_TEST_DRAWING = $DrawingPath
    [IO.File]::WriteAllText((Join-Path $projectPath 'output\shx-host-tests.txt'),
        ('PENDING started {0:o}' -f (Get-Date)))
    $process = Start-Process -FilePath $executablePath -WindowStyle Hidden -PassThru `
        -ArgumentList ('"{0}"' -f (Join-Path $PSScriptRoot 'ClickCircle_shx_selection.pdf'))
    Write-Host "Started isolated test editor PID=$($process.Id)"
    Write-Host "Host result: $(Join-Path $projectPath 'output\shx-host-tests.txt')"
}
finally {
    $env:CLICKCIRCLE_DATA_DIR = $oldData
    $env:CLICKCIRCLE_SHX_TEST_ROOT = $oldRoot
    $env:CLICKCIRCLE_SHX_TEST_DRAWING = $oldDrawing
}
