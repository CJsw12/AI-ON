$ErrorActionPreference = 'Stop'

$hrPacketSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRatePacket.cs'
$imuPacketSource = Join-Path $PSScriptRoot '../Assets/Scripts/WatchImuPacket.cs'
$imuTrackerSource = Join-Path $PSScriptRoot '../Assets/Scripts/WatchImuTracker.cs'
if (-not (Test-Path -LiteralPath $hrPacketSource)) { throw 'HeartRatePacket is not implemented.' }
if (-not (Test-Path -LiteralPath $imuPacketSource)) { throw 'WatchImuPacket is not implemented.' }
if (-not (Test-Path -LiteralPath $imuTrackerSource)) { throw 'WatchImuTracker is not implemented.' }

Add-Type -Path @($hrPacketSource, $imuPacketSource, $imuTrackerSource)

$script:passed = 0
function Check([bool]$value, [string]$message) {
    if (-not $value) { throw $message }
    $script:passed++
}

function MakeImu([long]$seq, [double]$peakAccel, [double]$peakGyro) {
    $p = New-Object WatchImuPacket
    $p.Seq = $seq
    $p.WatchTime = 1795650000000 + $seq * 100
    $p.Ax = 0.0
    $p.Ay = 9.81
    $p.Az = 0.0
    $p.Gx = 0.0
    $p.Gy = 0.0
    $p.Gz = 0.0
    $p.PeakAccel = $peakAccel
    $p.PeakGyro = $peakGyro
    $p.Samples = 5
    return $p
}

# --- WatchImuPacket.TryParse ---
$p = New-Object WatchImuPacket
$validImuJson = '{"v":1,"k":"imu","seq":7,"t":1795650000123,"a":[0.12,9.71,1.05],"g":[0.01,-0.32,0.05],"am":11.84,"gm":0.9,"n":5}'
$ok = [WatchImuPacket]::TryParse($validImuJson, [ref]$p)
Check $ok 'Valid IMU packet must parse.'
Check ($p.Seq -eq 7) 'Parsed IMU Seq must match.'
Check ($p.WatchTime -eq 1795650000123) 'Parsed IMU WatchTime must match.'
Check ([Math]::Abs($p.Ax - 0.12) -lt 1e-9 -and [Math]::Abs($p.Ay - 9.71) -lt 1e-9 -and [Math]::Abs($p.Az - 1.05) -lt 1e-9) 'Parsed accel vector must match.'
Check ([Math]::Abs($p.Gx - 0.01) -lt 1e-9 -and [Math]::Abs($p.Gy - (-0.32)) -lt 1e-9 -and [Math]::Abs($p.Gz - 0.05) -lt 1e-9) 'Parsed gyro vector must match.'
Check ([Math]::Abs($p.PeakAccel - 11.84) -lt 1e-9) 'Parsed PeakAccel must match.'
Check ([Math]::Abs($p.PeakGyro - 0.9) -lt 1e-9) 'Parsed PeakGyro must match.'
Check ($p.Samples -eq 5) 'Parsed Samples must match.'

# Reordered fields with whitespace
$p = New-Object WatchImuPacket
$ok = [WatchImuPacket]::TryParse('  { "n" : 1 , "gm" : 0 , "am" : 0 , "g" : [ 0 , 0 , 0 ] , "a" : [ 0 , 9.81 , 0 ] , "t" : 100 , "seq" : 2 , "k" : "imu" , "v" : 1 }  ', [ref]$p)
Check $ok 'Reordered IMU fields with whitespace and zero peak values must parse.'
Check ($p.Seq -eq 2 -and $p.Samples -eq 1 -and $p.PeakAccel -eq 0.0 -and $p.PeakGyro -eq 0.0) 'Reordered IMU values must populate.'

# Extra fields ignored (including nested objects/arrays)
$p = New-Object WatchImuPacket
$ok = [WatchImuPacket]::TryParse('{"v":1,"k":"imu","extra":{"k":"other","v":2,"n":0},"tags":["a",1,null],"seq":9,"t":200,"a":[0,9.81,0],"g":[0,0,0],"am":9.81,"gm":0.1,"n":3}', [ref]$p)
Check $ok 'Extra fields including nested structures must be ignored.'
Check ($p.Seq -eq 9 -and $p.Samples -eq 3) 'Nested extra keys must not affect top-level IMU fields.'

# Cross-classification safety: HR packet != IMU, IMU packet != HR
$hrJson = '{"v":1,"seq":42,"t":1795650000123,"bpm":72.0,"ok":true}'
$hrP = New-Object HeartRatePacket
Check (-not [WatchImuPacket]::TryParse($hrJson, [ref]$p)) 'Heart-rate packet must not parse as WatchImuPacket.'
Check (-not [HeartRatePacket]::TryParse($validImuJson, [ref]$hrP)) 'IMU packet must not parse as HeartRatePacket.'

# Rejections: k missing / wrong value / v != 1 / broken JSON
Check (-not [WatchImuPacket]::TryParse('{"v":1,"seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'Missing k field must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"hr","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'Wrong k value must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":123,"seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'Non-string k value must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":2,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'Version v:2 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0]', [ref]$p)) 'Truncated IMU JSON must be rejected.'

# Rejections: array lengths (2, 4) and non-numeric elements
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'a array of length 2 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0,1],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'a array of length 4 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":["0",9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'a array containing string must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'g array of length 2 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'g array of length 4 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,"x",0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'g array containing string must be rejected.'

# Rejections: NaN / Infinity / negative am/gm / n < 1
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[NaN,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$p)) 'NaN in a must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":1e309,"gm":0.1,"n":5}', [ref]$p)) 'Overflow Infinity in am must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":Infinity,"gm":0.1,"n":5}', [ref]$p)) 'Infinity token in am must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":-0.1,"gm":0.1,"n":5}', [ref]$p)) 'Negative am must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":-0.01,"n":5}', [ref]$p)) 'Negative gm must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":0}', [ref]$p)) 'n:0 must be rejected.'
Check (-not [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":7,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":-2}', [ref]$p)) 'Negative n must be rejected.'

# Length boundary: exact 512 accepted, 513 rejected
$prefix = '{"v":1,"k":"imu","seq":1,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5,"pad":"'
$suffix = '"}'
$padLen = 512 - $prefix.Length - $suffix.Length
$imu512 = $prefix + ('z' * $padLen) + $suffix
$imu513 = $prefix + ('z' * ($padLen + 1)) + $suffix
Check ($imu512.Length -eq 512 -and [WatchImuPacket]::TryParse($imu512, [ref]$p)) 'IMU packet of exact length 512 must be accepted.'
Check ($imu513.Length -eq 513 -and (-not [WatchImuPacket]::TryParse($imu513, [ref]$p))) 'IMU packet exceeding 512 chars must be rejected.'

# --- WatchImuTracker ---
$tracker = New-Object WatchImuTracker
Check (-not $tracker.HasSignal(0.0)) 'Fresh IMU tracker must not have signal.'
Check ($tracker.Motion(0.0) -eq 0.0) 'Motion with no signal must be 0.'
Check ($tracker.Received -eq 0) 'Fresh IMU tracker Received must be 0.'

# HasSignal 1.5s boundary
$tracker.Feed((MakeImu 1 9.81 0.02), 10.0)
Check ($tracker.HasSignal(10.0)) 'HasSignal must be true at packet time.'
Check ($tracker.HasSignal(11.5)) 'HasSignal must be true at exactly 1.5s.'
Check (-not $tracker.HasSignal(11.51)) 'HasSignal must be false after 1.5s.'
Check ($tracker.Motion(11.51) -eq 0.0) 'Motion after signal loss must be 0.'

# Motion: still (~0), moderate (~0.5), strong (~1.0 clamped)
$tracker.Reset()
for ($i = 0; $i -lt 5; $i++) {
    $tracker.Feed((MakeImu $i 9.81 0.01), 20.0 + ($i * 0.1))
}
Check ([Math]::Abs($tracker.Motion(20.4)) -lt 1e-6) 'Stationary IMU (PeakAccel=9.81) must yield Motion near 0.'

# Strong motion: +8.0 or more above 9.81 -> clamped to 1.0
$tracker.Reset()
for ($i = 0; $i -lt 5; $i++) {
    $tracker.Feed((MakeImu $i 18.5 2.0), 30.0 + ($i * 0.1))
}
Check ([Math]::Abs($tracker.Motion(30.4) - 1.0) -lt 1e-6) 'Strong motion (PeakAccel=18.5) must clamp Motion to 1.0.'

# Moderate motion: |13.81 - 9.81| / 8.0 = 0.5
$tracker.Reset()
$tracker.Feed((MakeImu 1 13.81 0.5), 40.0)
Check ([Math]::Abs($tracker.Motion(40.0) - 0.5) -lt 1e-6) 'Delta 4.0 m/s^2 must yield Motion 0.5.'

# 1-second window excludes older strong samples
$tracker.Feed((MakeImu 2 9.81 0.0), 41.2)
Check ([Math]::Abs($tracker.Motion(41.2)) -lt 1e-6) 'Samples older than 1 second must not affect Motion.'

# Ring buffer overflow (> 40 samples)
$tracker.Reset()
for ($i = 0; $i -lt 60; $i++) {
    $t = 100.0 + ($i * 0.1)
    # First 49 samples (t=100.0..104.8) are strong motion, last 11 (t=104.9..105.9) are still
    $am = if ($i -lt 49) { 20.0 } else { 9.81 }
    $tracker.Feed((MakeImu $i $am 0.1), $t)
    if ($i -eq 48) {
        Check ([Math]::Abs($tracker.Motion($t) - 1.0) -lt 1e-6) 'Strong motion before quiet window must yield Motion 1.0.'
    }
}
Check ($tracker.Received -eq 60) 'Received must count all 60 packets across ring overflow.'
Check ([Math]::Abs($tracker.PeakAccel - 9.81) -lt 1e-9) 'Latest PeakAccel must reflect newest packet.'
Check ([Math]::Abs($tracker.Motion(105.9)) -lt 1e-6) 'After ring overflow, recent 1s window must reflect only newest stationary samples.'

# Reset clears state
$tracker.Reset()
Check (-not $tracker.HasSignal(105.9)) 'Reset must clear HasSignal.'
Check ($tracker.Received -eq 0) 'Reset must clear Received.'
Check ($tracker.Motion(105.9) -eq 0.0) 'Reset must return Motion 0.'

"WatchImu: $script:passed behavioral assertions passed."
