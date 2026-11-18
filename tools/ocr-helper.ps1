param(
    [Parameter(Mandatory = $true)][string] $Path,
    [string] $Language = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$stream = $null
$bitmap = $null
function Encode([string] $value) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value))
}

try {
    [void][Reflection.Assembly]::LoadWithPartialName('System.Runtime.WindowsRuntime')
    [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime] | Out-Null
    [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null
    [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics, ContentType = WindowsRuntime] | Out-Null
    [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime] | Out-Null

    $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    })[0]
    function Await($operation, $type) {
        $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
        $task.GetAwaiter().GetResult()
    }
    "CCOCR`t1"
    $availableLanguages = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages)
    foreach ($available in $availableLanguages) {
        "LANG`t$(Encode $available.LanguageTag)`t$(Encode $available.DisplayName)"
    }
    $engines = @()
    if ($Language) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new($Language))
        if (-not $engine) { throw 'LANGUAGE_UNAVAILABLE' }
        $engines += $engine
    }
    else {
        # Compare installed engines for engineering identifiers; do not correct their text.
        $ordered = @($availableLanguages | Sort-Object @{ Expression = {
            if ($_.LanguageTag -like 'en*') { 0 } else { 1 }
        } })
        foreach ($available in ($ordered | Select-Object -First 8)) {
            $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($available)
            if ($engine) { $engines += $engine }
        }
        if ($engines.Count -eq 0) { throw 'NO_LANGUAGE' }
    }
    $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Path)) ([Windows.Storage.StorageFile])
    $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    if ($bitmap.PixelWidth -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or
        $bitmap.PixelHeight -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension) {
        throw 'IMAGE_TOO_LARGE'
    }
    if ($bitmap.BitmapPixelFormat -ne [Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8) {
        $converted = [Windows.Graphics.Imaging.SoftwareBitmap]::Convert($bitmap,
            [Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,
            [Windows.Graphics.Imaging.BitmapAlphaMode]::Ignore)
        $bitmap.Dispose()
        $bitmap = $converted
    }
    $bestScore = [double]::NegativeInfinity
    $result = $null
    $usedLanguage = ''
    foreach ($engine in $engines) {
        try {
            $candidate = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        }
        catch {
            if ($Language) { throw }
            continue
        }
        $score = 0.0
        foreach ($line in $candidate.Lines) {
            foreach ($word in $line.Words) {
                $text = $word.Text
                $score += [regex]::Matches($text, '[A-Za-z0-9]').Count * 2.0
                $score += [regex]::Matches($text, '[-/._#]').Count * 0.25
                $score -= [regex]::Matches($text, '[^\x00-\x7F]').Count * 0.5
                if ($text -match '[A-Za-z]{1,8}-[0-9]{2,}') { $score += 6.0 }
            }
        }
        if ($score -gt $bestScore) {
            $bestScore = $score
            $result = $candidate
            $usedLanguage = $engine.RecognizerLanguage.LanguageTag
        }
    }
    if ($null -eq $result) { throw 'All installed OCR engines failed.' }
    "USED`t$(Encode $usedLanguage)"
    $lineIndex = 0
    foreach ($line in $result.Lines) {
        foreach ($word in $line.Words) {
            $r = $word.BoundingRect
            [string]::Format([Globalization.CultureInfo]::InvariantCulture,
                "WORD`t{0}`t{1}`t{2}`t{3}`t{4}`t{5}", $lineIndex,
                (Encode $word.Text), $r.X, $r.Y, $r.Width, $r.Height)
        }
        $lineIndex++
    }
    'END'
}
catch {
    "ERROR`t$(Encode $_.Exception.Message)"
    exit 1
}
finally {
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}
