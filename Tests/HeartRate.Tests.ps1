$ErrorActionPreference = 'Stop'

$packetSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRatePacket.cs'
$trackerSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRateTracker.cs'
if (-not (Test-Path -LiteralPath $packetSource)) { throw 'HeartRatePacket is not implemented.' }
if (-not (Test-Path -LiteralPath $trackerSource)) { throw 'HeartRateTracker is not implemented.' }

Add-Type -Path @($packetSource, $trackerSource)

$script:passed = 0
function Check([bool]$value, [string]$message) {
    if (-not $value) { throw $message }
    $script:passed++
}

function MakePacket([long]$seq, [double]$bpm, [bool]$ok) {
    $p = New-Object HeartRatePacket
    $p.Seq = $seq
    $p.WatchTime = 1795650000000 + $seq * 1000
    $p.Bpm = $bpm
    $p.Ok = $ok
    return $p
}

# --- HeartRatePacket.TryParse ---
$p = New-Object HeartRatePacket
$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":42,"t":1795650000123,"bpm":72.5,"ok":true}', [ref]$p)
Check $ok 'Valid heart-rate packet must parse.'
Check ($p.Seq -eq 42) 'Parsed Seq must match.'
Check ($p.WatchTime -eq 1795650000123) 'Parsed WatchTime must match.'
Check ([Math]::Abs($p.Bpm - 72.5) -lt 1e-9) 'Parsed Bpm must match.'
Check $p.Ok 'Parsed Ok must be true.'

$p = New-Object HeartRatePacket
$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":43,"t":1795650001123,"ok":false}', [ref]$p)
Check $ok 'Heartbeat packet with ok:false and no bpm must parse.'
Check ($p.Seq -eq 43) 'Heartbeat Seq must match.'
Check (-not $p.Ok) 'Heartbeat Ok must be false.'
Check ([double]::IsNaN($p.Bpm)) 'Heartbeat without bpm must leave Bpm as NaN.'

$p = New-Object HeartRatePacket
$ok = [HeartRatePacket]::TryParse('  { "ok" : true , "bpm" : 80 , "t" : 1795650002000 , "seq" : 7 , "v" : 1 }  ', [ref]$p)
Check $ok 'Reordered fields with whitespace must parse.'
Check ($p.Seq -eq 7 -and $p.Bpm -eq 80.0 -and $p.Ok) 'Reordered fields must populate all values.'

$p = New-Object HeartRatePacket
$ok = [HeartRatePacket]::TryParse('{"v":1,"extra_str":"hello \"world\"","nested":{"v":2,"bpm":999,"arr":[1,true,null]},"seq":8,"t":1795650003000,"bpm":65.0,"ok":true}', [ref]$p)
Check $ok 'Extra fields including nested objects must be ignored.'
Check ($p.Seq -eq 8 -and $p.Bpm -eq 65.0 -and $p.Ok) 'Nested extra keys must not override top-level fields.'

$p = New-Object HeartRatePacket
Check ([HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":30,"ok":true}', [ref]$p)) 'Lower bound bpm=30 must be accepted.'
Check ([HeartRatePacket]::TryParse('{"v":1,"seq":2,"t":2,"bpm":230,"ok":true}', [ref]$p)) 'Upper bound bpm=230 must be accepted.'

# Rejections
Check (-not [HeartRatePacket]::TryParse('{"v":2,"seq":1,"t":1,"bpm":72,"ok":true}', [ref]$p)) 'Unsupported version v:2 must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":72,"ok":true', [ref]$p)) 'Truncated JSON must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":72,"ok":true} garbage', [ref]$p)) 'Trailing garbage after JSON must be rejected.'
Check (-not [HeartRatePacket]::TryParse('not json at all', [ref]$p)) 'Non-JSON input must be rejected.'
Check (-not [HeartRatePacket]::TryParse('', [ref]$p)) 'Empty string must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":29,"ok":true}', [ref]$p)) 'bpm below 30 must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":231,"ok":true}', [ref]$p)) 'bpm above 230 must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":NaN,"ok":true}', [ref]$p)) 'bpm NaN token must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":"NaN","ok":true}', [ref]$p)) 'bpm NaN string must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":1e309,"ok":true}', [ref]$p)) 'bpm overflow to Infinity must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"ok":true}', [ref]$p)) 'ok:true without bpm must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":null,"ok":false}', [ref]$p)) 'Non-numeric bpm field must be rejected.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1795650000123,"a":[0.12,9.71,1.05],"g":[0.01,-0.32,0.05],"am":11.84,"gm":0.9,"n":5}', [ref]$p)) 'IMU packet must not parse as HeartRatePacket.'
Check (-not [HeartRatePacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1795650000123,"bpm":72.0,"ok":true}', [ref]$p)) 'Packet carrying k field must not parse as HeartRatePacket.'

# Length boundary: exact 512 accepted, 513 rejected
$basePrefix = '{"v":1,"seq":1,"t":1,"bpm":72,"ok":true,"pad":"'
$baseSuffix = '"}'
$padLen512 = 512 - $basePrefix.Length - $baseSuffix.Length
$json512 = $basePrefix + ('a' * $padLen512) + $baseSuffix
$json513 = $basePrefix + ('a' * ($padLen512 + 1)) + $baseSuffix
Check ($json512.Length -eq 512 -and [HeartRatePacket]::TryParse($json512, [ref]$p)) 'Packet of exact length 512 must be accepted.'
Check ($json513.Length -eq 513 -and (-not [HeartRatePacket]::TryParse($json513, [ref]$p))) 'Packet exceeding 512 chars must be rejected.'

# --- HeartRateTracker ---
$tracker = New-Object HeartRateTracker
Check (-not $tracker.HasSignal(0)) 'Fresh tracker must not have signal.'
Check (-not $tracker.Valid(0)) 'Fresh tracker must not be valid.'
Check ([double]::IsNaN($tracker.Bpm)) 'Fresh tracker Bpm must be NaN.'
Check ($tracker.Level(0) -eq [HeartLevel]::Unknown) 'Fresh tracker Level must be Unknown.'

# 5-second signal timeout
$tracker.Feed((MakePacket 1 70.0 $true), 10.0)
Check ($tracker.HasSignal(10.0)) 'Signal must be present at packet timestamp.'
Check ($tracker.HasSignal(15.0)) 'Signal must still be present at exactly 5 seconds.'
Check ($tracker.Valid(15.0)) 'Valid must be true within 5 seconds for ok packet.'
Check (-not $tracker.HasSignal(15.01)) 'Signal must drop after 5 seconds without packets.'
Check (-not $tracker.Valid(15.01)) 'Valid must be false when signal drops.'

# Heartbeat (ok:false) -> HasSignal true, Valid false, Bpm retains last valid value
$tracker.Feed((MakePacket 2 ([double]::NaN) $false), 16.0)
Check ($tracker.HasSignal(18.0)) 'Heartbeat ok:false packet must keep HasSignal true within 5 seconds.'
Check (-not $tracker.Valid(18.0)) 'Heartbeat ok:false packet must make Valid false.'
Check ($tracker.Bpm -eq 70.0) 'Heartbeat must keep last valid Bpm.'
Check ($tracker.Level(18.0) -eq [HeartLevel]::Unknown) 'Level must be Unknown when Valid is false.'

# Baseline requires BOTH >= 30s since first valid sample AND >= 10 samples
$tracker.Reset()
# Case 1: 10 samples in 9 seconds (< 30s) -> still Unknown
for ($i = 0; $i -lt 10; $i++) {
    $tracker.Feed((MakePacket $i 70.0 $true), [double]$i)
}
Check ($tracker.Level(9.0) -eq [HeartLevel]::Unknown) '10 samples before 30 seconds must keep Level Unknown.'
Check (-not $tracker.HasBaseline) 'Baseline must not confirm before 30 seconds.'

# Case 2: 9 samples over 32 seconds (< 10 samples) -> still Unknown, 10th sample confirms
$tracker.Reset()
for ($i = 0; $i -lt 9; $i++) {
    $tracker.Feed((MakePacket $i 70.0 $true), [double]($i * 4))
}
Check ($tracker.Level(32.0) -eq [HeartLevel]::Unknown) '9 samples after 30 seconds must still be Unknown.'
Check (-not $tracker.HasBaseline) 'Baseline must not confirm with fewer than 10 samples.'
$tracker.Feed((MakePacket 9 70.0 $true), 33.0)
Check ($tracker.HasBaseline) '10th sample after 30 seconds must confirm baseline.'
Check ([Math]::Abs($tracker.Baseline - 70.0) -lt 1e-9) 'Confirmed baseline must equal sample median.'
Check ($tracker.Level(33.0) -eq [HeartLevel]::Calm) 'Steady heart rate at baseline must be Calm.'

# +8 / +20 boundary checks (7.9 -> Calm, 8 -> Elevated, 19.9 -> Elevated, 20 -> High)
$tracker.Reset()
for ($i = 0; $i -le 30; $i++) {
    $tracker.Feed((MakePacket $i 70.0 $true), [double]$i)
}
Check ($tracker.HasBaseline -and [Math]::Abs($tracker.Baseline - 70.0) -lt 1e-9) '31 samples over 30s at 70 bpm must confirm baseline=70.'

$tracker.Feed((MakePacket 31 77.9 $true), 36.0)
Check ($tracker.Level(36.0) -eq [HeartLevel]::Calm) 'Delta +7.9 must be Calm.'

$tracker.Feed((MakePacket 32 78.0 $true), 42.0)
Check ($tracker.Level(42.0) -eq [HeartLevel]::Elevated) 'Delta +8.0 must be Elevated.'

$tracker.Feed((MakePacket 33 89.9 $true), 48.0)
Check ($tracker.Level(48.0) -eq [HeartLevel]::Elevated) 'Delta +19.9 must be Elevated.'

$tracker.Feed((MakePacket 34 90.0 $true), 54.0)
Check ($tracker.Level(54.0) -eq [HeartLevel]::High) 'Delta +20.0 must be High.'

# Restart Seq -> 0 and duplicate Seq must be accepted
$beforeReceived = $tracker.Received
$tracker.Feed((MakePacket 0 72.0 $true), 60.0)
$tracker.Feed((MakePacket 0 73.0 $true), 61.0)
Check ($tracker.Received -eq ($beforeReceived + 2)) 'Seq reset to 0 and duplicate Seq must both be accepted.'
Check ($tracker.Bpm -eq 73.0) 'Bpm must update after Seq restart.'

# RejectBadPacket counter
$tracker.RejectBadPacket()
Check ($tracker.Rejected -eq 1) 'RejectBadPacket must increment Rejected count.'

# Rolling 5-minute baseline update & ring buffer > 400 samples
for ($t = 62; $t -le 500; $t++) {
    $tracker.Feed((MakePacket $t 85.0 $true), [double]$t)
}
Check ([Math]::Abs($tracker.Baseline - 85.0) -lt 1e-9) 'Baseline must adapt to recent 5-minute median across >400 samples.'
Check ($tracker.Level(500.0) -eq [HeartLevel]::Calm) 'Heart rate matching updated baseline must return Calm.'

# Reset() clears all state
$tracker.Reset()
Check (-not $tracker.HasSignal(500.0)) 'Reset must clear signal state.'
Check (-not $tracker.Valid(500.0)) 'Reset must clear valid state.'
Check (-not $tracker.HasBaseline) 'Reset must clear baseline confirmation.'
Check ([double]::IsNaN($tracker.Bpm)) 'Reset must clear Bpm to NaN.'
Check ($tracker.Level(500.0) -eq [HeartLevel]::Unknown) 'Reset must return Level to Unknown.'
Check ($tracker.Received -eq 0 -and $tracker.Rejected -eq 0) 'Reset must zero Received and Rejected counters.'

"HeartRate: $script:passed behavioral assertions passed."
