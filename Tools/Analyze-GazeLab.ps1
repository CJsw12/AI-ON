param(
    [string]$Directory = (Join-Path $PSScriptRoot '../Logs/GazeLab'),
    [string]$Csv,
    [double]$WindowSeconds = 15,
    [int]$Iterations = 1,
    [int]$IntervalSeconds = 2
)
$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture
function Read-SharedText([string]$Path) {
    $sharing = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $sharing)
    $reader = [IO.StreamReader]::new($stream)
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function Number($Value) { [double]::Parse([string]$Value, $inv) }
function Metrics($Rows) {
    $errors = @($Rows | ForEach-Object { Number $_.error_px } | Sort-Object)
    if (!$errors.Count) { return @{samples=0; metrics_available=$false} }
    $sx=0.0; $sy=0.0; $sum=0.0; $sq=0.0; $rawSq=0.0; $rawN=0
    foreach ($r in $Rows) {
        $e=Number $r.error_px; $sum += $e; $sq += $e*$e
        $sx += Number $r.error_x; $sy += Number $r.error_y
        if ($r.has_raw -eq '1' -and $r.raw_x -ne '' -and $r.raw_y -ne '') {
            $dx=(Number $r.raw_x)-(Number $r.target_x); $dy=(Number $r.raw_y)-(Number $r.target_y)
            $rawSq += $dx*$dx+$dy*$dy; $rawN++
        }
    }
    $n=$errors.Count
    @{ samples=$n; metrics_available=$true; mean_px=[Math]::Round($sum/$n,2);
       rmse_px=[Math]::Round([Math]::Sqrt($sq/$n),2); p95_px=[Math]::Round($errors[[Math]::Ceiling(.95*$n)-1],2);
       bias_x_px=[Math]::Round($sx/$n,2); bias_y_px=[Math]::Round($sy/$n,2);
       raw_rmse_px=if ($rawN) {[Math]::Round([Math]::Sqrt($rawSq/$rawN),2)} else {$null} }
}
function Correlation($Rows,[string]$X,[string]$Y) {
    if ($Rows.Count -lt 10) { return $null }
    $sx=0.0; $sy=0.0; $sxx=0.0; $syy=0.0; $sxy=0.0; $n=0
    foreach ($r in $Rows) {
        if ($r.$X -eq '' -or $r.$Y -eq '') { continue }
        $xv=Number $r.$X; $yv=Number $r.$Y
        $sx+=$xv; $sy+=$yv; $sxx+=$xv*$xv; $syy+=$yv*$yv; $sxy+=$xv*$yv; $n++
    }
    $den=[Math]::Sqrt([Math]::Max(0,($n*$sxx-$sx*$sx)*($n*$syy-$sy*$sy)))
    if ($den -lt .00001) { return $null }
    [Math]::Round(($n*$sxy-$sx*$sy)/$den,3)
}
if ($Iterations -lt 1 -or $Iterations -gt 30) { throw 'Iterations must be 1..30.' }
for ($iteration=0; $iteration -lt $Iterations; $iteration++) {
    $meta=$null
    if (!$Csv) {
        $latest=Join-Path $Directory 'latest.json'
        if (!(Test-Path -LiteralPath $latest)) { throw 'No recording yet. Start a Gaze Lab recording in the Game view.' }
        $meta=(Read-SharedText $latest) | ConvertFrom-Json
        $csvPath=$meta.csv_path
    } else { $csvPath=[IO.Path]::GetFullPath($Csv) }
    $text=Read-SharedText $csvPath
    # A concurrently written final CSV row may be incomplete; retain complete lines only.
    $lastNewline=$text.LastIndexOf("`n")
    if ($lastNewline -lt 0) { throw 'CSV header has not been flushed yet.' }
    $rows=@($text.Substring(0,$lastNewline) | ConvertFrom-Csv)
    $end=if ($rows.Count) {Number $rows[-1].elapsed_s} else {0}
    $window=@($rows | Where-Object {(Number $_.elapsed_s) -ge $end-$WindowSeconds})
    $valid=@($window | Where-Object {$_.included -eq '1'})
    $unique=@($window | Where-Object {$_.new_sample -eq '1' -and $_.phase -eq 'tracking'})
    $regions=@{}
    foreach ($name in @('left','right','top','bottom','center')) {
        $area=@($valid | Where-Object {
            $x=Number $_.target_x; $y=Number $_.target_y
            switch ($name) { left {$x -lt 426.67} right {$x -gt 853.33} top {$y -lt 300} bottom {$y -gt 600}
                center {$x -ge 426.67 -and $x -le 853.33 -and $y -ge 300 -and $y -le 600} }
        })
        $regions[$name]=Metrics $area
    }
    $result=@{
        csv=$csvPath; updated_utc=[DateTime]::UtcNow.ToString('O'); source=$meta.source; revision=$meta.revision; state=$meta.state;
        elapsed_s=$end; window_s=$WindowSeconds; metrics=(Metrics $valid); regions=$regions;
        tracking_unique_samples=$unique.Count; invalid_samples=($unique.Count-$valid.Count);
        correlations=@{target_gaze_x=(Correlation $valid 'target_x' 'gaze_x'); target_gaze_y=(Correlation $valid 'target_y' 'gaze_y');
            head_error_x=(Correlation $valid 'head_x' 'error_x'); head_error_y=(Correlation $valid 'head_y' 'error_y');
            position_error_x=(Correlation $valid 'position_x' 'error_x'); position_error_y=(Correlation $valid 'position_y' 'error_y');
            distance_error_y=(Correlation $valid 'head_distance_m' 'error_y')};
        note='Canvas px, top-left origin. Raw means pre-clamp/pre-smoother mapped gaze. Correlation is diagnostic, not causation; moving-target error includes human pursuit delay.'
    }
    $result | ConvertTo-Json -Depth 6
    if ($iteration+1 -lt $Iterations) { Start-Sleep -Seconds ([Math]::Max(1,$IntervalSeconds)) }
}
