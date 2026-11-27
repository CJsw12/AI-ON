[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 11126,
    [ValidateRange(1, 86400)]
    [int]$Seconds = 600,
    [string]$OutDir = 'Logs/WatchSensors'
)

$ErrorActionPreference = 'Stop'

$hrPacketSource = Join-Path $PSScriptRoot '../../Assets/Scripts/HeartRatePacket.cs'
$imuPacketSource = Join-Path $PSScriptRoot '../../Assets/Scripts/WatchImuPacket.cs'
$diagSource = Join-Path $PSScriptRoot '../../Assets/Scripts/HeartRateDiagnostics.cs'
if (-not (Test-Path -LiteralPath $hrPacketSource)) { throw 'HeartRatePacket.cs not found.' }
if (-not (Test-Path -LiteralPath $imuPacketSource)) { throw 'WatchImuPacket.cs not found.' }
if (-not (Test-Path -LiteralPath $diagSource)) { throw 'HeartRateDiagnostics.cs not found.' }

if (-not ('HeartRateDiagnostics' -as [type])) {
    if (-not ('HeartRatePacket' -as [type])) {
        Add-Type -Path @($hrPacketSource, $imuPacketSource, $diagSource)
    }
    else {
        $refs = @([HeartRatePacket].Assembly.Location, [WatchImuPacket].Assembly.Location) | Select-Object -Unique
        Add-Type -Path $diagSource -ReferencedAssemblies $refs
    }
}

if (-not (Test-Path -LiteralPath $OutDir)) {
    [void](New-Item -ItemType Directory -Force -Path $OutDir)
}

$stamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$outPath = Join-Path $OutDir ("watch-sensors-{0}.csv" -f $stamp)

$epoch = New-Object System.DateTime(1970, 1, 1, 0, 0, 0, [System.DateTimeKind]::Utc)
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$writer = New-Object System.IO.StreamWriter($outPath, $false, $utf8NoBom)
$writer.AutoFlush = $true

$udp = New-Object System.Net.Sockets.UdpClient
$udp.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket, [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)
$udp.Client.Bind((New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, $Port)))
$udp.Client.ReceiveTimeout = 200

$diag = New-Object HeartRateDiagnostics
$hrCount = 0
$imuCount = 0
$badCount = 0
$lastBpm = [double]::NaN
$elapsedSec = 0.0

$logEvent = [Action[string]]{
    param([string]$line)
    Write-Output ("[event] {0}" -f $line)
}

try {
    $writer.WriteLine('# 이 기록은 Git에 올리지 말 것 (심박·움직임 건강 정보 포함)')
    $writer.WriteLine('pc_epoch_ms,kind,raw_json')

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $limitMs = [long]$Seconds * 1000L
    $nextStatusSec = 1.0

    while ($stopwatch.ElapsedMilliseconds -lt $limitMs) {
        try {
            $ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $bytes = $udp.Receive([ref]$ep)
            if ($null -ne $bytes -and $bytes.Length -gt 0) {
                $nowSec = $stopwatch.Elapsed.TotalSeconds
                $pcEpochMs = [long][Math]::Floor(([DateTime]::UtcNow - $epoch).TotalMilliseconds)
                $raw = [System.Text.Encoding]::UTF8.GetString($bytes)

                $imuPacket = New-Object WatchImuPacket
                $hrPacket = New-Object HeartRatePacket
                $imuReason = $null
                $hrReason = $null

                if ([WatchImuPacket]::TryParse($raw, [ref]$imuPacket, [ref]$imuReason)) {
                    $kind = 'imu'
                    $imuCount++
                    $diag.OnImu($imuPacket, $nowSec)
                }
                elseif ([HeartRatePacket]::TryParse($raw, [ref]$hrPacket, [ref]$hrReason)) {
                    $kind = 'hr'
                    $hrCount++
                    if ($hrPacket.Ok) {
                        $lastBpm = $hrPacket.Bpm
                    }
                    $diag.OnHeartRate($hrPacket, $nowSec)
                }
                else {
                    $kind = 'bad'
                    $badCount++
                    $reason = if ($raw.IndexOf('"k"', [System.StringComparison]::Ordinal) -ge 0) { $imuReason } else { $hrReason }
                    $diag.OnRejected($reason, $nowSec)
                }

                $escaped = $raw.Replace("`r", '\r').Replace("`n", '\n').Replace('"', '""')
                $writer.WriteLine(('{0},{1},"{2}"' -f $pcEpochMs, $kind, $escaped))
            }
        }
        catch [System.Net.Sockets.SocketException] {
            if ($_.Exception.SocketErrorCode -ne [System.Net.Sockets.SocketError]::TimedOut -and
                $_.Exception.SocketErrorCode -ne [System.Net.Sockets.SocketError]::WouldBlock) {
                throw
            }
        }

        $nowSec = $stopwatch.Elapsed.TotalSeconds
        $diag.FlushSignalEvents($nowSec, $logEvent)

        if ($nowSec -ge $nextStatusSec) {
            $hrRate = $diag.Hr.PerSecond($nowSec)
            $imuRate = $diag.Imu.PerSecond($nowSec)
            $bpmText = if ([double]::IsNaN($lastBpm)) { '--' } else { [string]::Format($invariant, '{0:0.0}', $lastBpm) }
            $lostTotal = $diag.Hr.Lost + $diag.Imu.Lost
            $statusLine = [string]::Format(
                $invariant,
                '[{0,4:0}s] hr={1:0.0}/s imu={2:0.0}/s bpm={3} lost={4}(hr={5},imu={6}) maxgap={7:0.0}s bad={8}',
                [Math]::Floor($nowSec),
                $hrRate,
                $imuRate,
                $bpmText,
                $lostTotal,
                $diag.Hr.Lost,
                $diag.Imu.Lost,
                $diag.Hr.MaxGapSeconds,
                $badCount
            )
            Write-Output $statusLine
            while ($nextStatusSec -le $nowSec) {
                $nextStatusSec += 1.0
            }
        }
    }

    $elapsedSec = $stopwatch.Elapsed.TotalSeconds
    $diag.FlushSignalEvents($elapsedSec, $logEvent)
}
finally {
    $udp.Close()
    $writer.Dispose()
}

$finalBpmText = if ([double]::IsNaN($lastBpm)) { '--' } else { [string]::Format($invariant, '{0:0.0}', $lastBpm) }
Write-Output '=== Watch Sensors Summary ==='
Write-Output ([string]::Format($invariant, 'Elapsed  : {0:0.0}s', $elapsedSec))
Write-Output ("Summary  : {0}" -f $diag.Summary($elapsedSec))
Write-Output ("Last BPM : {0}" -f $finalBpmText)
Write-Output ([string]::Format($invariant, 'HR       : received={0} lost={1} maxgap={2:0.0}s unavail={3} restarts={4}', $diag.Hr.Received, $diag.Hr.Lost, $diag.Hr.MaxGapSeconds, $diag.UnavailableCount, $diag.Hr.Restarts))
Write-Output ([string]::Format($invariant, 'IMU      : received={0} lost={1} maxgap={2:0.0}s restarts={3}', $diag.Imu.Received, $diag.Imu.Lost, $diag.Imu.MaxGapSeconds, $diag.Imu.Restarts))
Write-Output ("Saved    : {0} (hr={1}, imu={2}, bad={3})" -f $outPath, $hrCount, $imuCount, $badCount)
