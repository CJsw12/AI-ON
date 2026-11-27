[CmdletBinding()]
param(
    [string]$HostName = '127.0.0.1',
    [ValidateRange(1, 65535)]
    [int]$Port = 11126,
    [ValidateRange(30, 230)]
    [double]$Bpm = 72,
    [ValidateSet('flat', 'ramp', 'dropout')]
    [string]$Pattern = 'flat',
    [ValidateRange(1, 86400)]
    [int]$Seconds = 60,
    [switch]$WithImu
)

$ErrorActionPreference = 'Stop'

$epoch = New-Object System.DateTime(1970, 1, 1, 0, 0, 0, [System.DateTimeKind]::Utc)
$rng = New-Object System.Random
$udp = New-Object System.Net.Sockets.UdpClient
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

$dropStart = [int][Math]::Floor(($Seconds - 5) / 2.0)
if ($dropStart -lt 0) { $dropStart = 0 }
$dropEnd = $dropStart + 5

$motionStart = if ($Pattern -eq 'ramp') { 35 } else { [int][Math]::Floor($Seconds / 3.0) }
$motionEnd = if ($Pattern -eq 'ramp') { $Seconds } else { [int][Math]::Ceiling(($Seconds * 2.0) / 3.0) }

function Send-HrPacket([int]$seq) {
    $epochMs = [long][Math]::Floor(([DateTime]::UtcNow - $epoch).TotalMilliseconds)
    $ok = $true
    $value = $Bpm

    if ($Pattern -eq 'flat') {
        $jitter = ($rng.NextDouble() * 2.0) - 1.0
        $value = $Bpm + $jitter
    }
    elseif ($Pattern -eq 'ramp') {
        if ($seq -ge 35) {
            $progress = [Math]::Min(1.0, ($seq - 35 + 1) / 25.0)
            $value = $Bpm + (30.0 * $progress)
        }
    }
    elseif ($Pattern -eq 'dropout') {
        if ($seq -ge $dropStart -and $seq -lt $dropEnd) {
            $ok = $false
        }
        else {
            $jitter = ($rng.NextDouble() * 2.0) - 1.0
            $value = $Bpm + $jitter
        }
    }

    if ($ok) {
        if ($value -lt 30.0) { $value = 30.0 }
        if ($value -gt 230.0) { $value = 230.0 }
        $value = [Math]::Round($value, 1)
        $json = [string]::Format($invariant, '{{"v":1,"seq":{0},"t":{1},"bpm":{2:0.0},"ok":true}}', $seq, $epochMs, $value)
    }
    else {
        $json = [string]::Format($invariant, '{{"v":1,"seq":{0},"t":{1},"ok":false}}', $seq, $epochMs)
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    [void]$udp.Send($bytes, $bytes.Length, $HostName, $Port)
    Write-Output $json
}

function Send-ImuPacket([int]$imuSeq, [int]$secIndex) {
    $epochMs = [long][Math]::Floor(([DateTime]::UtcNow - $epoch).TotalMilliseconds)
    $moving = ($secIndex -ge $motionStart -and $secIndex -lt $motionEnd)

    if ($moving) {
        $ax = [Math]::Round(($rng.NextDouble() * 8.0) - 4.0, 3)
        $ay = [Math]::Round(9.81 + (($rng.NextDouble() * 5.0) - 2.0), 3)
        $az = [Math]::Round(($rng.NextDouble() * 8.0) - 4.0, 3)
        $gx = [Math]::Round(($rng.NextDouble() * 3.0) - 1.5, 3)
        $gy = [Math]::Round(($rng.NextDouble() * 3.0) - 1.5, 3)
        $gz = [Math]::Round(($rng.NextDouble() * 3.0) - 1.5, 3)
        $am = [Math]::Round(15.5 + ($rng.NextDouble() * 3.5), 3)
        $gm = [Math]::Round(1.2 + ($rng.NextDouble() * 1.5), 3)
    }
    else {
        $ax = [Math]::Round(($rng.NextDouble() * 0.1) - 0.05, 3)
        $ay = [Math]::Round(9.81 + (($rng.NextDouble() * 0.1) - 0.05), 3)
        $az = [Math]::Round(($rng.NextDouble() * 0.1) - 0.05, 3)
        $gx = [Math]::Round(($rng.NextDouble() * 0.04) - 0.02, 3)
        $gy = [Math]::Round(($rng.NextDouble() * 0.04) - 0.02, 3)
        $gz = [Math]::Round(($rng.NextDouble() * 0.04) - 0.02, 3)
        $mag = [Math]::Sqrt($ax * $ax + $ay * $ay + $az * $az)
        $am = [Math]::Round($mag + ($rng.NextDouble() * 0.05), 3)
        $gmag = [Math]::Sqrt($gx * $gx + $gy * $gy + $gz * $gz)
        $gm = [Math]::Round($gmag + ($rng.NextDouble() * 0.02), 3)
    }

    $json = [string]::Format(
        $invariant,
        '{{"v":1,"k":"imu","seq":{0},"t":{1},"a":[{2:0.###},{3:0.###},{4:0.###}],"g":[{5:0.###},{6:0.###},{7:0.###}],"am":{8:0.###},"gm":{9:0.###},"n":5}}',
        $imuSeq, $epochMs, $ax, $ay, $az, $gx, $gy, $gz, $am, $gm
    )
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    [void]$udp.Send($bytes, $bytes.Length, $HostName, $Port)
    Write-Output $json
}

try {
    $runClock = [System.Diagnostics.Stopwatch]::StartNew()
    if (-not $WithImu) {
        for ($seq = 0; $seq -lt $Seconds; $seq++) {
            Send-HrPacket $seq
            if ($seq -lt ($Seconds - 1)) {
                $targetMs = ($seq + 1) * 1000
                $waitMs = $targetMs - [int]$runClock.ElapsedMilliseconds
                if ($waitMs -gt 0) {
                    Start-Sleep -Milliseconds $waitMs
                }
            }
        }
    }
    else {
        $totalTicks = $Seconds * 10
        for ($tick = 0; $tick -lt $totalTicks; $tick++) {
            $secIndex = [int][Math]::Floor($tick / 10.0)
            if (($tick % 10) -eq 0) {
                Send-HrPacket $secIndex
            }
            Send-ImuPacket $tick $secIndex
            if ($tick -lt ($totalTicks - 1)) {
                $targetMs = ($tick + 1) * 100
                $waitMs = $targetMs - [int]$runClock.ElapsedMilliseconds
                if ($waitMs -gt 0) {
                    Start-Sleep -Milliseconds $waitMs
                }
            }
        }
    }
}
finally {
    $udp.Close()
}
