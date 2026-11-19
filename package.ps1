[CmdletBinding()]
param(
    [string] $Version = '1.7.1',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'release'),
    [switch] $IncludeDrawingDemo
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$') {
    throw 'Version must be a plain semantic version, for example 1.0.0.'
}

$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$archivePath = Join-Path $outputPath ("ClickCircle-{0}-x64.zip" -f $Version)
if (Test-Path -LiteralPath $archivePath) {
    throw "Release archive already exists: $archivePath"
}

$requiredFiles = @(
    'dist\ClickCircle.pvp',
    'dist\ClickCircle.Core.dll',
    'dist\PDFXEdit.dll',
    'src\Plugin.cs',
    'src\CircleTool.cs',
    'src\TextRegionPicker.cs',
    'src\ShxTextReader.cs',
    'src\AnnotSelector.cs',
    'src\OcrService.cs',
    'src\OcrRegionRenderer.cs',
    'src\OcrReviewDialog.cs',
    'src\StampPanel.cs',
    'src\SubjectHistory.cs',
    'src\ModeCursor.cs',
    'src\RightClickRouting.cs',
    'src\UpdateService.cs',
    'src\HotkeyBinding.cs',
    'src\HotkeyScope.cs',
    'src\HotkeyService.cs',
    'tools\ExtractInterop.cs',
    'tools\ClickCircle.Bootstrap.il',
    'tools\PanelPreview.cs',
    'tools\OcrReviewPreview.cs',
    'tools\ocr-helper.ps1',
    'build.ps1',
    'install.cmd',
    'uninstall.cmd',
    'package.ps1',
    'README.md',
    'VALIDATION.md',
    'tests\TextPickerTests.cs',
    'tests\RegionPickerTests.cs',
    'tests\ShxTextTests.cs',
    'tests\ShxHostTests.cs',
    'tests\OcrTests.cs',
    'tests\HistoryPreferenceTests.cs',
    'tests\CursorTests.cs',
    'tests\RightClickTests.cs',
    'tests\PanelSizeTests.cs',
    'tests\UpdateTests.cs',
    'tests\OcrHostTests.cs',
    'tests\run-ocr-host-tests.ps1',
    'tests\create_ocr_fixture.py',
    'tests\ClickCircle_ocr_selection.pdf',
    'tests\run-shx-host-tests.ps1',
    'tests\create_shx_fixture.py',
    'tests\ClickCircle_shx_selection.pdf',
    'tests\SeparatorPanelTests.cs',
    'tests\HotkeyBindingTests.cs',
    'tests\HotkeyPanelTests.cs',
    'tests\HotkeyServiceTests.cs',
    'tests\verify_output.py',
    'tests\create_fixture.py',
    'tests\create_box_fixture.py',
    'tests\ClickCircle_box_selection.pdf',
    'tests\ClickCircle_acceptance.pdf'
)
$optionalFiles = @('output\ClickCircle_acceptance_result.pdf',
    'output\ClickCircle_shx_result.pdf', 'output\shx-host-tests.txt',
    'output\ClickCircle_ocr_selection.pdf',
    'output\ocr-validation\ocr-host-tests.txt', 'output\ocr-validation\ocr-live-result.txt')
if ($IncludeDrawingDemo) {
    $requiredFiles += 'output\Lighting_LI-282_demo.pdf'
}
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $relativePath) -PathType Leaf)) {
        throw "Required release file is missing: $relativePath"
    }
}
$files = @($requiredFiles)
foreach ($relativePath in $optionalFiles) {
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot $relativePath) -PathType Leaf) {
        $files += $relativePath
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$archive = $null
try {
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    foreach ($relativePath in $files) {
        $entryName = 'CircleStampPlugin/' + $relativePath.Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, (Join-Path $PSScriptRoot $relativePath), $entryName,
            [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    if ($null -ne $archive) { $archive.Dispose() }
    else { $stream.Dispose() }
}
Write-Host "Created release package: $archivePath"
Get-Item -LiteralPath $archivePath | Select-Object FullName, Length


