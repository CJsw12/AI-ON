$ErrorActionPreference = 'Stop'

$hrPacketSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRatePacket.cs'
$hrTrackerSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRateTracker.cs'
$imuPacketSource = Join-Path $PSScriptRoot '../Assets/Scripts/WatchImuPacket.cs'
$imuTrackerSource = Join-Path $PSScriptRoot '../Assets/Scripts/WatchImuTracker.cs'
$diagSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRateDiagnostics.cs'
$receiverSource = Join-Path $PSScriptRoot '../Assets/Scripts/HeartRateUdpReceiver.cs'

foreach ($src in @($hrPacketSource, $hrTrackerSource, $imuPacketSource, $imuTrackerSource, $diagSource, $receiverSource)) {
    if (-not (Test-Path -LiteralPath $src)) { throw "Missing source file: $src" }
}

Add-Type -Path @($hrPacketSource, $hrTrackerSource, $imuPacketSource, $imuTrackerSource, $diagSource, $receiverSource)

$script:passed = 0
function Check([bool]$value, [string]$message) {
    if (-not $value) { throw $message }
    $script:passed++
}

function MakeHr([long]$seq, [double]$bpm, [bool]$ok) {
    $p = New-Object HeartRatePacket
    $p.Seq = $seq
    $p.WatchTime = 1795650000000 + $seq * 1000
    $p.Bpm = $bpm
    $p.Ok = $ok
    return $p
}

function MakeImu([long]$seq) {
    $p = New-Object WatchImuPacket
    $p.Seq = $seq
    $p.WatchTime = 1795650000000 + $seq * 100
    $p.Ax = 0.0
    $p.Ay = 9.81
    $p.Az = 0.0
    $p.Gx = 0.0
    $p.Gy = 0.0
    $p.Gz = 0.0
    $p.PeakAccel = 9.81
    $p.PeakGyro = 0.05
    $p.Samples = 5
    return $p
}

# --- 1. Fresh state & normal flow ---
$diag = New-Object HeartRateDiagnostics
Check ($diag.Hr.Received -eq 0 -and $diag.Imu.Received -eq 0) 'Fresh diagnostics must start with 0 received.'
Check ($diag.Hr.Lost -eq 0 -and $diag.Imu.Lost -eq 0) 'Fresh diagnostics must start with 0 lost.'
Check ($diag.Restarts -eq 0 -and $diag.UnavailableCount -eq 0 -and $diag.RejectedTotal -eq 0) 'Fresh counters must be 0.'
Check ($diag.Hr.PerSecond(0.0) -eq 0.0 -and $diag.Imu.PerSecond(0.0) -eq 0.0) 'Fresh PerSecond must be 0.'
Check ([double]::IsNaN($diag.Hr.SecondsSinceLast(0.0)) -and [double]::IsNaN($diag.Imu.SecondsSinceLast(0.0))) 'Fresh SecondsSinceLast must be NaN.'
Check ($diag.Hr.MaxGapSeconds -eq 0.0 -and $diag.Imu.MaxGapSeconds -eq 0.0) 'Fresh MaxGapSeconds must be 0.'

for ($s = 0; $s -lt 5; $s++) {
    $diag.OnHeartRate((MakeHr $s (70.0 + $s) $true), [double]$s)
    for ($k = 0; $k -lt 10; $k++) {
        $imuSeq = $s * 10 + $k
        $t = [double]$s + ($k * 0.1)
        $diag.OnImu((MakeImu $imuSeq), $t)
    }
}
Check ($diag.Hr.Received -eq 5) 'Normal HR flow must count 5 packets.'
Check ($diag.Imu.Received -eq 50) 'Normal IMU flow must count 50 packets.'
Check ($diag.Hr.Lost -eq 0 -and $diag.Imu.Lost -eq 0) 'Consecutive seq must yield 0 lost.'
Check ($diag.Restarts -eq 0) 'Consecutive seq must yield 0 restarts.'
Check ([Math]::Abs($diag.Hr.SecondsSinceLast(4.5) - 0.5) -lt 1e-9) 'HR SecondsSinceLast must reflect elapsed time.'
Check ([Math]::Abs($diag.Imu.SecondsSinceLast(5.2) - 0.3) -lt 1e-9) 'IMU SecondsSinceLast must reflect elapsed time.'

# Unavailable count & ratio (ok:false)
$diag.OnHeartRate((MakeHr 5 ([double]::NaN) $false), 5.0)
Check ($diag.Hr.Received -eq 6 -and $diag.UnavailableCount -eq 1) 'ok:false packet must increment Hr.Received and UnavailableCount.'
Check ([Math]::Abs($diag.UnavailableRatio - (1.0 / 6.0)) -lt 1e-9) 'UnavailableRatio must equal UnavailableCount / Hr.Received.'

# --- 2. Sequence jump loss & duplicate seq ---
$diag.Reset()
$diag.OnHeartRate((MakeHr 10 72.0 $true), 1.0)
$diag.OnHeartRate((MakeHr 11 72.0 $true), 2.0)
Check ($diag.Hr.Lost -eq 0) 'Adjacent seq 10->11 must report 0 lost.'
$diag.OnHeartRate((MakeHr 14 72.0 $true), 5.0) # missed 12, 13 -> +2
Check ($diag.Hr.Lost -eq 2) 'Seq jump 11->14 must add 2 lost packets.'
$diag.OnHeartRate((MakeHr 14 72.0 $true), 5.5) # duplicate -> +0 lost, +0 restarts
Check ($diag.Hr.Lost -eq 2 -and $diag.Hr.Restarts -eq 0) 'Duplicate seq must not change Lost or Restarts.'
$diag.OnHeartRate((MakeHr 18 72.0 $true), 9.5) # missed 15, 16, 17 -> +3 (total 5)
Check ($diag.Hr.Lost -eq 5) 'Seq jump 14->18 must accumulate to 5 lost packets.'

$diag.OnImu((MakeImu 100), 1.0)
$diag.OnImu((MakeImu 105), 1.5) # missed 101..104 -> +4
Check ($diag.Imu.Lost -eq 4) 'IMU seq jump 100->105 must add 4 lost packets.'

# --- 3. Sequence decrease -> Restarts (must NOT add to Lost) ---
$lostBeforeHr = $diag.Hr.Lost
$diag.OnHeartRate((MakeHr 0 71.0 $true), 11.0) # 18 -> 0: restart
Check ($diag.Hr.Restarts -eq 1 -and $diag.Restarts -eq 1) 'HR seq decrease must increment Restarts to 1.'
Check ($diag.Hr.Lost -eq $lostBeforeHr) 'HR seq decrease must NOT add to Lost.'
$diag.OnHeartRate((MakeHr 1 71.0 $true), 12.0)
Check ($diag.Hr.Lost -eq $lostBeforeHr -and $diag.Hr.Restarts -eq 1) 'Normal seq after restart must keep Lost and Restarts.'

$lostBeforeImu = $diag.Imu.Lost
$diag.OnImu((MakeImu 0), 2.0) # 105 -> 0: restart
Check ($diag.Imu.Restarts -eq 1 -and $diag.Restarts -eq 2) 'IMU seq decrease must increment Imu.Restarts and total Restarts.'
Check ($diag.Imu.Lost -eq $lostBeforeImu) 'IMU seq decrease must NOT add to Imu.Lost.'

# --- 4. 1-second PerSecond(now) boundary ---
$diag.Reset()
$diag.OnHeartRate((MakeHr 1 72.0 $true), 10.0)
Check ($diag.Hr.PerSecond(10.0) -eq 1.0) 'PerSecond at exact arrival time must be 1.'
Check ($diag.Hr.PerSecond(10.99) -eq 1.0) 'PerSecond at +0.99s must still include packet.'
Check ($diag.Hr.PerSecond(11.0) -eq 0.0) 'PerSecond at exact +1.0s boundary must exclude packet.'
Check ($diag.Hr.PerSecond(9.5) -eq 0.0) 'PerSecond before packet timestamp must be 0.'

# Steady 1Hz HR and 10Hz IMU -> exact 1.0/s and 10.0/s
$diag.Reset()
for ($i = 0; $i -le 3; $i++) {
    $diag.OnHeartRate((MakeHr $i 72.0 $true), [double]$i)
}
Check ($diag.Hr.PerSecond(3.0) -eq 1.0) 'Steady 1Hz HR stream must report 1.0/s.'

for ($i = 0; $i -le 14; $i++) {
    $diag.OnImu((MakeImu $i), $i * 0.1)
}
Check ($diag.Imu.PerSecond(1.0) -eq 10.0) 'Steady 10Hz IMU stream at t=1.0 must report 10.0/s.'
Check ($diag.Imu.PerSecond(1.4) -eq 10.0) 'Steady 10Hz IMU stream at t=1.4 (FP boundary) must report 10.0/s.'

# --- 5. MaxGapSeconds ---
$diag.Reset()
$diag.OnHeartRate((MakeHr 1 72.0 $true), 10.0)
Check ($diag.Hr.MaxGapSeconds -eq 0.0) 'Single packet must have MaxGapSeconds = 0.'
$diag.OnHeartRate((MakeHr 2 72.0 $true), 11.0)
Check ([Math]::Abs($diag.Hr.MaxGapSeconds - 1.0) -lt 1e-9) 'Gap 1.0s must set MaxGapSeconds to 1.0.'
$diag.OnHeartRate((MakeHr 3 72.0 $true), 13.1)
Check ([Math]::Abs($diag.Hr.MaxGapSeconds - 2.1) -lt 1e-9) 'Gap 2.1s must update MaxGapSeconds to 2.1.'
$diag.OnHeartRate((MakeHr 4 72.0 $true), 14.1)
Check ([Math]::Abs($diag.Hr.MaxGapSeconds - 2.1) -lt 1e-9) 'Shorter subsequent gap must preserve MaxGapSeconds 2.1.'

# --- 6. TryParse overload reasons & rejection counters ---
$diag.Reset()
$hrP = New-Object HeartRatePacket
$imuP = New-Object WatchImuPacket
$reason = $null

# Valid packets return null reason
Check ([HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":72,"ok":true}', [ref]$hrP, [ref]$reason) -and $null -eq $reason) 'Valid HR TryParse overload must succeed with null reason.'
Check ([WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":1,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$imuP, [ref]$reason) -and $null -eq $reason) 'Valid IMU TryParse overload must succeed with null reason.'

# Broken JSON -> bad_json
$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_json') 'Truncated HR JSON must report bad_json.'
$diag.OnRejected($reason, 1.0)

$ok = [WatchImuPacket]::TryParse('not json', [ref]$imuP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_json') 'Non-JSON IMU input must report bad_json.'
$diag.OnRejected($reason, 1.1)

# Version v:2 -> bad_version
$ok = [HeartRatePacket]::TryParse('{"v":2,"seq":1,"t":1,"bpm":72,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_version') 'HR v:2 must report bad_version.'
$diag.OnRejected($reason, 1.2)

$ok = [WatchImuPacket]::TryParse('{"v":2,"k":"imu","seq":1,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$imuP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_version') 'IMU v:2 must report bad_version.'
$diag.OnRejected($reason, 1.3)

# BPM range -> bad_bpm_range
$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":29,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_bpm_range') 'HR bpm=29 must report bad_bpm_range.'
$diag.OnRejected($reason, 1.4)

$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":231,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_bpm_range') 'HR bpm=231 must report bad_bpm_range.'
$diag.OnRejected($reason, 1.5)

$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"bpm":1e309,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_bpm_range') 'HR bpm=1e309 overflow must report bad_bpm_range.'
$diag.OnRejected($reason, 1.6)

# Missing / invalid fields -> bad_fields
$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"bpm":72,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_fields') 'HR missing t must report bad_fields.'
$diag.OnRejected($reason, 1.7)

$ok = [HeartRatePacket]::TryParse('{"v":1,"seq":1,"t":1,"ok":true}', [ref]$hrP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_fields') 'HR ok:true without bpm must report bad_fields.'
$diag.OnRejected($reason, 1.8)

$ok = [WatchImuPacket]::TryParse('{"v":1,"k":"imu","seq":1,"t":1,"a":[0,9.8],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5}', [ref]$imuP, [ref]$reason)
Check ((-not $ok) -and $reason -eq 'bad_fields') 'IMU short accel array must report bad_fields.'
$diag.OnRejected($reason, 1.9)

# Too long (> 512 chars) -> too_long
$prefix = '{"v":1,"seq":1,"t":1,"bpm":72,"ok":true,"pad":"'
$suffix = '"}'
$longHr = $prefix + ('a' * (513 - $prefix.Length - $suffix.Length)) + $suffix
$ok = [HeartRatePacket]::TryParse($longHr, [ref]$hrP, [ref]$reason)
Check ($longHr.Length -eq 513 -and (-not $ok) -and $reason -eq 'too_long') 'HR >512 chars must report too_long.'
$diag.OnRejected($reason, 2.0)

$imuPrefix = '{"v":1,"k":"imu","seq":1,"t":1,"a":[0,9.8,0],"g":[0,0,0],"am":9.8,"gm":0.1,"n":5,"pad":"'
$longImu = $imuPrefix + ('z' * (513 - $imuPrefix.Length - $suffix.Length)) + $suffix
$ok = [WatchImuPacket]::TryParse($longImu, [ref]$imuP, [ref]$reason)
Check ($longImu.Length -eq 513 -and (-not $ok) -and $reason -eq 'too_long') 'IMU >512 chars must report too_long.'
$diag.OnRejected($reason, 2.1)

# Other reason
$diag.OnRejected('unexpected_code', 2.2)

Check ($diag.BadJson -eq 2) 'BadJson counter must be 2.'
Check ($diag.BadVersion -eq 2) 'BadVersion counter must be 2.'
Check ($diag.BadBpmRange -eq 3) 'BadBpmRange counter must be 3.'
Check ($diag.BadFields -eq 3) 'BadFields counter must be 3.'
Check ($diag.TooLong -eq 2) 'TooLong counter must be 2.'
Check ($diag.Other -eq 1) 'Other counter must be 1.'
Check ($diag.RejectedTotal -eq 13) 'RejectedTotal must sum all 6 reason counters.'

# --- 7. Signal state change events (lost / regained) fire strictly once per transition ---
$diag.Reset()
$events = New-Object 'System.Collections.Generic.List[string]'
$capture = [Action[string]]{ param([string]$msg) $events.Add($msg) }

# Before any valid HR, advancing time must not emit lost
$diag.FlushSignalEvents(0.0, $capture)
$diag.FlushSignalEvents(20.0, $capture)
Check ($events.Count -eq 0 -and (-not $diag.IsHeartRateLost)) 'No lost event before first valid HR packet.'

# Receive valid HR at t=10.0 -> still active at t=15.0 (exact 5s), lost at t=15.01
$diag.OnHeartRate((MakeHr 1 72.0 $true), 10.0)
$diag.FlushSignalEvents(10.0, $capture)
$diag.FlushSignalEvents(15.0, $capture)
Check ($events.Count -eq 0 -and (-not $diag.IsHeartRateLost)) 'Must not emit lost at exact 5.0s boundary.'

$diag.FlushSignalEvents(15.01, $capture)
Check ($events.Count -eq 1 -and $events[0] -match 'lost' -and $diag.IsHeartRateLost) 'Must emit lost event once after >5s gap.'

# Repeated polls while lost must not duplicate lost event
$diag.FlushSignalEvents(16.0, $capture)
$diag.FlushSignalEvents(20.0, $capture)
$diag.OnHeartRate((MakeHr 2 ([double]::NaN) $false), 21.0)
$diag.FlushSignalEvents(21.0, $capture)
Check ($events.Count -eq 1 -and $diag.LostEvents -eq 1) 'Sustained loss and ok:false must not duplicate lost or emit regained.'

# Valid HR returns -> regained emitted once
$diag.OnHeartRate((MakeHr 3 74.0 $true), 22.0)
$diag.FlushSignalEvents(22.0, $capture)
Check ($events.Count -eq 2 -and $events[1] -match 'regained' -and (-not $diag.IsHeartRateLost) -and $diag.RegainedEvents -eq 1) 'Valid HR return must emit regained once.'

# Subsequent valid packets must not duplicate regained
$diag.OnHeartRate((MakeHr 4 75.0 $true), 23.0)
$diag.FlushSignalEvents(23.0, $capture)
Check ($events.Count -eq 2) 'Continued valid HR must not re-emit regained.'

# Dropout via ok:false packets lasting >5s (matches Send-FakeHeartRate -Pattern dropout)
for ($t = 24; $t -le 28; $t++) {
    $diag.OnHeartRate((MakeHr ($t - 19) ([double]::NaN) $false), [double]$t)
    $diag.FlushSignalEvents([double]$t, $capture)
}
Check ($events.Count -eq 2) 'At exactly 5.0s of ok:false (t=28 vs t=23), lost must not fire yet.'
$diag.FlushSignalEvents(28.1, $capture)
Check ($events.Count -eq 3 -and $events[2] -match 'lost') 'Over 5.0s of ok:false must fire lost once.'
$diag.OnHeartRate((MakeHr 10 73.0 $true), 29.0)
$diag.FlushSignalEvents(29.0, $capture)
Check ($events.Count -eq 4 -and $events[3] -match 'regained') 'Returning ok:true after ok:false dropout must fire regained once.'

# Null log callback must not throw or replay stale events later
$diag.FlushSignalEvents(35.0, $null)
Check ($diag.IsHeartRateLost -and $diag.LostEvents -eq 3) 'Null log callback must still update lost state safely.'
$diag.FlushSignalEvents(36.0, $capture)
Check ($events.Count -eq 4) 'Attaching logger later must not replay already-flushed lost event.'

# --- 8. Summary(now) string format ---
$diag.Reset()
$diag.OnHeartRate((MakeHr 1 72.0 $true), 0.0)
$diag.OnHeartRate((MakeHr 4 72.0 $true), 2.1) # lost=2, maxgap=2.1
$diag.OnHeartRate((MakeHr 5 ([double]::NaN) $false), 3.1) # unavail=1
for ($k = 0; $k -lt 10; $k++) {
    $diag.OnImu((MakeImu $k), 2.2 + ($k * 0.1))
}
$diag.OnRejected('bad_json', 3.1)

$summary = $diag.Summary(3.1)
$expected = 'hr 1.0/s total=3 lost=2 maxgap=2.1s unavail=1 | imu 10.0/s total=10 lost=0 | rejected=1(bad_json=1) restarts=0'
Check ($summary -eq $expected) "Summary format mismatch. Expected: [$expected] Actual: [$summary]"

# Zero rejections omits empty parentheses
$diag.Reset()
$diag.OnHeartRate((MakeHr 1 70.0 $true), 0.0)
$summaryClean = $diag.Summary(0.5)
Check ($summaryClean -eq 'hr 1.0/s total=1 lost=0 maxgap=0.0s unavail=0 | imu 0.0/s total=0 lost=0 | rejected=0 restarts=0') "Zero-rejection summary mismatch: [$summaryClean]"

# Multiple rejection reasons listed in order
$diag.OnRejected('bad_json', 0.5)
$diag.OnRejected('bad_version', 0.5)
$diag.OnRejected('bad_bpm_range', 0.5)
$diag.OnRejected('bad_fields', 0.5)
$diag.OnRejected('too_long', 0.5)
$diag.OnRejected('other', 0.5)
$summaryMulti = $diag.Summary(0.5)
Check ($summaryMulti -match 'rejected=6\(bad_json=1,bad_version=1,bad_bpm_range=1,bad_fields=1,too_long=1,other=1\) restarts=0$') "Multi-rejection summary mismatch: [$summaryMulti]"

# --- 9. HeartRateUdpReceiver.Log & Diagnostics 1-second throttling ---
$rx = New-Object HeartRateUdpReceiver(0)
Check ($null -ne $rx.Diagnostics) 'Receiver must expose Diagnostics property.'
$rx.Log = $null
$rx.Poll(0.0)
$rx.Poll(1.5)

$rxLogs = New-Object 'System.Collections.Generic.List[string]'
$rx.Log = [Action[string]]{ param([string]$line) $rxLogs.Add($line) }
$rx.Poll(2.0) # 0.5s since 1.5 -> no summary yet
$rx.Poll(2.4) # 0.9s since 1.5 -> no summary yet
Check ($rxLogs.Count -eq 0) 'Receiver must not emit summary before 1.0s interval.'
$rx.Poll(2.5) # 1.0s since 1.5 -> 1 summary
Check ($rxLogs.Count -eq 1 -and $rxLogs[0] -match '^hr ') 'Receiver must emit one summary at 1.0s interval.'
$rx.Poll(3.0)
$rx.Poll(3.49)
Check ($rxLogs.Count -eq 1) 'Receiver must not emit duplicate summaries within 1.0s.'
$rx.Poll(3.5)
Check ($rxLogs.Count -eq 2) 'Receiver must emit second summary at next 1.0s boundary.'
$rx.Dispose()

"HeartRateDiagnostics: $script:passed behavioral assertions passed."
