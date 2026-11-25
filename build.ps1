[CmdletBinding()]
param(
    [string] $EditorDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'App\PDF-XChange\PDF Editor'),
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [switch] $RunTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$editorPath = [IO.Path]::GetFullPath($EditorDirectory)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$corePath = Join-Path $editorPath 'PXCEditorCore.x64.dll'
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
$assemblerPath = Join-Path $frameworkPath 'ilasm.exe'
foreach ($requiredPath in @($corePath, $compilerPath, $assemblerPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build dependency is missing: $requiredPath"
    }
}

$objectPath = Join-Path $PSScriptRoot 'obj'
$stagePath = Join-Path $objectPath 'stage'
New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
$extractorPath = Join-Path $objectPath 'ExtractInterop.exe'
& $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
    "/out:$extractorPath" (Join-Path $PSScriptRoot 'tools\ExtractInterop.cs')
if ($LASTEXITCODE -ne 0) { throw 'Failed to compile the type-library extractor.' }
& $extractorPath $corePath $stagePath
if ($LASTEXITCODE -ne 0) { throw 'Failed to read the embedded editor type library.' }

$interopPath = Join-Path $stagePath 'PDFXEdit.dll'
$pluginPath = Join-Path $stagePath 'ClickCircle.Core.dll'
& $compilerPath /nologo /target:library /platform:x64 /optimize+ `
    "/out:$pluginPath" "/reference:$interopPath" `
    "/resource:$(Join-Path $PSScriptRoot 'tools\ocr-helper.ps1'),ClickCircle.Ocr.ps1" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    (Join-Path $PSScriptRoot 'src\Plugin.cs') `
    (Join-Path $PSScriptRoot 'src\StampPanel.cs') `
    (Join-Path $PSScriptRoot 'src\PickHint.cs') `
    (Join-Path $PSScriptRoot 'src\SubjectHistory.cs') `
    (Join-Path $PSScriptRoot 'src\ModeCursor.cs') `
    (Join-Path $PSScriptRoot 'src\RightClickRouting.cs') `
    (Join-Path $PSScriptRoot 'src\LeftReleaseRouting.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotationLabel.cs') `
    (Join-Path $PSScriptRoot 'src\CircleTool.cs') `
    (Join-Path $PSScriptRoot 'src\TextRegionPicker.cs') `
    (Join-Path $PSScriptRoot 'src\ShxTextReader.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotSelector.cs') `
    (Join-Path $PSScriptRoot 'src\OcrService.cs') `
    (Join-Path $PSScriptRoot 'src\OcrRegionRenderer.cs') `
    (Join-Path $PSScriptRoot 'src\OcrReviewDialog.cs') `
    (Join-Path $PSScriptRoot 'src\UpdateService.cs') `
    (Join-Path $PSScriptRoot 'src\HotkeyBinding.cs') `
    (Join-Path $PSScriptRoot 'src\HotkeyScope.cs') `
    (Join-Path $PSScriptRoot 'src\HotkeyService.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotStyle.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotStyler.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotWatcher.cs') `
    (Join-Path $PSScriptRoot 'src\AnnotStyleDialog.cs')
if ($LASTEXITCODE -ne 0) { throw 'Failed to compile the plugin.' }

& $assemblerPath /nologo /dll /x64 "/output:$(Join-Path $stagePath 'ClickCircle.pvp')" `
    (Join-Path $PSScriptRoot 'tools\ClickCircle.Bootstrap.il')
if ($LASTEXITCODE -ne 0) { throw 'Failed to assemble the native plugin entry point.' }

if ($RunTests) {
    $testPath = Join-Path $stagePath 'TextPickerTests.exe'
    & $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
        "/out:$testPath" "/reference:$pluginPath" "/reference:$interopPath" `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
        (Join-Path $PSScriptRoot 'tests\TextPickerTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile text-picking tests.' }
    & $testPath
    if ($LASTEXITCODE -ne 0) { throw 'Text-picking tests failed.' }

    $hotkeyTestPath = Join-Path $stagePath 'HotkeyTests.exe'
    & $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
        "/out:$hotkeyTestPath" "/reference:$pluginPath" "/reference:$interopPath" `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll `
        (Join-Path $PSScriptRoot 'tests\HotkeyBindingTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile hotkey tests.' }
    & $hotkeyTestPath
    if ($LASTEXITCODE -ne 0) { throw 'Hotkey tests failed.' }

    $panelTestPath = Join-Path $stagePath 'HotkeyPanelTests.exe'
    & $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
        "/out:$panelTestPath" "/reference:$pluginPath" "/reference:$interopPath" `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll `
        (Join-Path $PSScriptRoot 'tests\HotkeyPanelTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile hotkey panel tests.' }
    & $panelTestPath
    if ($LASTEXITCODE -ne 0) { throw 'Hotkey panel tests failed.' }

    $serviceTestPath = Join-Path $stagePath 'HotkeyServiceTests.exe'
    & $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
        "/out:$serviceTestPath" "/reference:$pluginPath" `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
        (Join-Path $PSScriptRoot 'src\HotkeyService.cs') `
        (Join-Path $PSScriptRoot 'tests\HotkeyServiceTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile hotkey routing tests.' }
    & $serviceTestPath
    if ($LASTEXITCODE -ne 0) { throw 'Hotkey routing tests failed.' }

    foreach ($testName in @('RegionPickerTests', 'SeparatorPanelTests', 'ShxTextTests', 'OcrTests', 'HistoryPreferenceTests', 'CursorTests', 'RightClickTests', 'LeftReleaseTests', 'PanelSizeTests', 'UpdateTests', 'AnnotStyleTests')) {
        $selectionTestPath = Join-Path $stagePath ($testName + '.exe')
        & $compilerPath /nologo /target:exe /platform:x64 /optimize+ `
            "/out:$selectionTestPath" "/reference:$pluginPath" "/reference:$interopPath" `
            /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll `
            (Join-Path $PSScriptRoot ('tests\' + $testName + '.cs'))
        if ($LASTEXITCODE -ne 0) { throw "Failed to compile $testName." }
        & $selectionTestPath
        if ($LASTEXITCODE -ne 0) { throw "$testName failed." }
    }
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
foreach ($name in @('ClickCircle.pvp', 'ClickCircle.Core.dll', 'PDFXEdit.dll')) {
    Copy-Item -LiteralPath (Join-Path $stagePath $name) -Destination (Join-Path $outputPath $name) -Force
}
Write-Host "Built x64 plugin: $outputPath"
Get-ChildItem -LiteralPath $outputPath -File |
    Where-Object Name -In @('ClickCircle.pvp', 'ClickCircle.Core.dll', 'PDFXEdit.dll') |
    Select-Object Name, Length

