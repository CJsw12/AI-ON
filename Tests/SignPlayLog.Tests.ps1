$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Add-Type -Path (Join-Path $repo 'Assets/Scripts/Sign/SignPlayLog.cs')

function Assert($condition, $message) { if (-not $condition) { throw $message } }

$directory = Join-Path ([IO.Path]::GetTempPath()) ('sign-play-' + [guid]::NewGuid().ToString('N'))
$localTime = [datetime]::new(2026, 11, 27, 14, 30, 45)
$log = $null
$secondLog = $null
$culture = [Threading.Thread]::CurrentThread.CurrentCulture
$nullText = [NullString]::Value
try {
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('de-DE')
    Assert ([SignPlayLog]::TryOpen($directory, $localTime, 1000, [ref]$log)) 'open play log'
    $landmarks = [float[]]::new(63)
    $landmarks[0] = 0.123456
    $landmarks[1] = -0.004566
    $landmarks[62] = 0.75
    Assert ($log.TryWriteFrame(2234, 'ㄹ', 640, 480, 'Right', 0.935, $landmarks, 'practice', 'rule', 'ㄹ', 'ㄹ')) 'write practice frame'
    $escaped = "라\벨\`"다" + [char]10 + [char]9 + [char]1
    Assert ($log.TryWriteFrame(3000, '_other', 640, 480, 'Left', 0.887, $landmarks, 'challenge', 'knn', 'ㅅ', $escaped)) 'write other frame'
    Assert ($log.TryWriteFrame(3001, $nullText, 640, 480, $nullText, 0, $null, 'challenge', 'knn', $nullText, $nullText)) 'write no-hand frame'
    Assert ($log.TryClose()) 'close play log'
    Assert (-not $log.TryWriteFrame(3002, $nullText, 640, 480, $nullText, 0, $null, 'challenge', 'knn', $nullText, $nullText)) 'reject write after close'

    $path = Join-Path $directory '20261127-143045.jsonl'
    $exclusive = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $exclusive.Dispose()
    $bytes = [IO.File]::ReadAllBytes($path)
    Assert ($bytes[0] -eq [byte][char]'{') 'UTF-8 without BOM'
    $lines = [IO.File]::ReadAllLines($path, [Text.UTF8Encoding]::new($false, $true))
    Assert ($lines.Length -eq 3) 'one JSON object per frame'
    $frames = @($lines | ForEach-Object { $_ | ConvertFrom-Json })
    $keys = 'clip,expected,held,t,width,height,handedness,handednessScore,landmarks,stage,mode,rule,confirmed'
    foreach ($frame in $frames) {
        Assert ((Compare-Object ($keys.Split(',')) @($frame.PSObject.Properties.Name) -CaseSensitive).Count -eq 0) 'JSON fields'
        Assert ($frame.clip -ceq '20261127-143045' -and $null -eq $frame.held -and $frame.width -eq 640 -and $frame.height -eq 480) 'shared fields'
    }
    $practice = $frames[0]
    Assert ($practice.expected -ceq 'ㄹ' -and $practice.handedness -ceq 'Right' -and $practice.handednessScore -eq 0.935 -and $practice.t -eq 1.234) 'practice values'
    Assert ($practice.stage -ceq 'practice' -and $practice.mode -ceq 'rule' -and $practice.rule -ceq 'ㄹ' -and $practice.confirmed -ceq 'ㄹ') 'practice stage and recognition'
    Assert ($practice.landmarks.Count -eq 63 -and $practice.landmarks[0] -eq 0.12346 -and $practice.landmarks[1] -eq -0.00457 -and $practice.landmarks[62] -eq 0.75) 'landmark values'
    Assert ($lines[0] -cmatch '"t":1\.234,' -and $lines[1] -cmatch '"t":2\.000,') 'three time decimals'
    Assert ($lines[0] -cmatch '"landmarks":\[-?\d+\.\d{5}(,-?\d+\.\d{5}){62}\]') 'five landmark decimals with invariant separator'
    Assert ($frames[1].expected -ceq '_other' -and $frames[1].stage -ceq 'challenge' -and $frames[1].mode -ceq 'knn' -and $frames[1].rule -ceq 'ㅅ' -and $frames[1].confirmed -ceq $escaped) 'k-NN and JSON escaping'
    $noHand = $frames[2]
    Assert ($null -eq $noHand.expected -and $null -eq $noHand.handedness -and $null -eq $noHand.handednessScore -and $null -eq $noHand.landmarks -and $null -eq $noHand.rule -and $null -eq $noHand.confirmed) 'no-hand null values'

    Assert ([SignPlayLog]::TryOpen($directory, $localTime, 1000, [ref]$secondLog)) 'open again in the same second'
    Assert ($secondLog.TryClose()) 'close second log'
    Assert ((Test-Path -LiteralPath (Join-Path $directory '20261127-143046.jsonl')) -and [IO.File]::ReadAllLines($path).Length -eq 3) 'separate file without overwriting'
    $failed = $null
    Assert (-not [SignPlayLog]::TryOpen($path, $localTime, 0, [ref]$failed) -and $null -eq $failed) 'bad directory returns failure'
    Write-Output 'SignPlayLog tests passed'
} finally {
    [Threading.Thread]::CurrentThread.CurrentCulture = $culture
    if ($null -ne $log) { [void]$log.TryClose() }
    if ($null -ne $secondLog) { [void]$secondLog.TryClose() }
    if (Test-Path -LiteralPath $directory) {
        Get-ChildItem -LiteralPath $directory -File | Remove-Item
        Remove-Item -LiteralPath $directory
    }
}
