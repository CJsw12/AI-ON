$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sources = @('SignFeatures.cs', 'SignRuleClassifier.cs') | ForEach-Object { Join-Path $repo "Assets/Scripts/Sign/$_" }
Add-Type -Path $sources

function Assert-Label($context, $expected, $actual) {
    if ($expected -ceq $actual) { return }
    if ($null -eq $expected) { $expected = 'null' }
    if ($null -eq $actual) { $actual = 'null' }
    $failures.Add("$context 기대값=$expected 실제값=$actual")
}

function Rotate-Landmarks([float[]]$landmarks, [int]$width, [int]$height, [double]$degrees, [int]$pivot = 0, [int[]]$joints = (1..20)) {
    $radians = $degrees * [Math]::PI / 180
    $cosine = [Math]::Cos($radians)
    $sine = [Math]::Sin($radians)
    $rotated = [float[]]$landmarks.Clone()

    $pivotOffset = 3 * $pivot
    foreach ($joint in $joints) {
        $offset = 3 * $joint
        $x = ([double]$landmarks[$offset] - $landmarks[$pivotOffset]) * $width
        $y = ([double]$landmarks[$offset + 1] - $landmarks[$pivotOffset + 1]) * $height
        $rotated[$offset] = [float]($landmarks[$pivotOffset] + ($x * $cosine - $y * $sine) / $width)
        $rotated[$offset + 1] = [float]($landmarks[$pivotOffset + 1] + ($x * $sine + $y * $cosine) / $height)
    }

    return ,$rotated
}

$frames = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Fixtures/sign-rule-frames.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($frames.Count -ne 183) { throw "프레임 수 기대값=183 실제값=$($frames.Count)" }

$failures = [System.Collections.Generic.List[string]]::new()
$totals = @{}
$correct = @{}
$heldGroups = [SignRuleClassifier]::Letters + @('n', 'm')
foreach ($group in $heldGroups) {
    $totals[$group] = 0
    $correct[$group] = 0
}
$mirroredCount = 0
$rotationCount = 0
$siblingCount = 0
$validFeatures = $null

foreach ($frame in $frames) {
    $context = 'clip={0} t={1}' -f $frame.clip, $frame.t
    $landmarks = [float[]]$frame.landmarks
    $isLeft = [SignHandedness]::IsLeft($frame.handedness)
    $features = $null
    if (-not [SignFeatures]::TryCompute($landmarks, $frame.width, $frame.height, $isLeft, [ref]$features)) {
        $failures.Add("$context 특징 계산 기대값=True 실제값=False")
        continue
    }

    $actual = [SignRuleClassifier]::Classify($features)
    $mirrored = $null
    if ([SignFeatures]::TryCompute($landmarks, $frame.width, $frame.height, (-not $isLeft), [ref]$mirrored)) {
        $mirroredCount++
        Assert-Label "$context 좌우 반전" $actual ([SignRuleClassifier]::Classify($mirrored))
        $gapDifference = [SignRuleClassifier]::IndexMiddleGapDegrees($features) - [SignRuleClassifier]::IndexMiddleGapDegrees($mirrored)
        $spreadDifference = [SignRuleClassifier]::ThumbSpreadDegrees($features) - [SignRuleClassifier]::ThumbSpreadDegrees($mirrored)
        if ([Math]::Abs($gapDifference) -gt 1e-6 -or [Math]::Abs($spreadDifference) -gt 1e-6) {
            $failures.Add("$context 좌우 반전 각도 차이 검지-중지=$gapDifference 엄지=$spreadDifference")
        }
    } else {
        $failures.Add("$context 좌우 반전 특징 계산 기대값=True 실제값=False")
    }

    if (-not $frame.held) { continue }

    $group = $frame.expected
    if ($null -eq $group) { $group = $frame.clip }
    $totals[$group]++
    if ($frame.expected -ceq $actual) { $correct[$group]++ }
    Assert-Label "$context 유지 구간" $frame.expected $actual
    if ($null -eq $frame.expected) { continue }
    if ($frame.expected -ceq $actual) { $validFeatures = $features }

    foreach ($degrees in -20, 20) {
        $rotated = Rotate-Landmarks $landmarks $frame.width $frame.height $degrees
        $rotatedFeatures = $null
        if ([SignFeatures]::TryCompute($rotated, $frame.width, $frame.height, $isLeft, [ref]$rotatedFeatures)) {
            $rotationCount++
            Assert-Label "$context 회전 ${degrees}도" $frame.expected ([SignRuleClassifier]::Classify($rotatedFeatures))
        } else {
            $failures.Add("$context 회전 ${degrees}도 특징 계산 기대값=True 실제값=False")
        }
    }

    if ($frame.expected -ceq 'ㄹ') {
        $maxGap = 0.0
        $siblingFeatures = $null
        foreach ($degrees in -25, 25) {
            $points = Rotate-Landmarks $landmarks $frame.width $frame.height $degrees 5 (6..8)
            $candidate = $null
            if ([SignFeatures]::TryCompute($points, $frame.width, $frame.height, $isLeft, [ref]$candidate)) {
                $gap = [SignRuleClassifier]::IndexMiddleGapDegrees($candidate)
                if ($gap -gt $maxGap) { $maxGap = $gap; $siblingFeatures = $candidate }
            }
        }
        $siblingCount++
        if ($maxGap -lt [SignRuleClassifier]::RMaxIndexMiddleDegrees) { $failures.Add("$context ㅌ 모사 검지-중지 각도=$maxGap") }
        Assert-Label "$context ㅌ 모사" $null ([SignRuleClassifier]::Classify($siblingFeatures))
    }

    if ($frame.expected -ceq 'ㅅ') {
        $siblingFeatures = $null
        for ($degrees = 5; $degrees -le 180 -and $null -eq $siblingFeatures; $degrees += 5) {
            foreach ($direction in -1, 1) {
                $points = Rotate-Landmarks $landmarks $frame.width $frame.height ($degrees * $direction) 2 (3..4)
                $candidate = $null
                if ([SignFeatures]::TryCompute($points, $frame.width, $frame.height, $isLeft, [ref]$candidate) -and
                    [SignRuleClassifier]::ThumbSpreadDegrees($candidate) -ge 75) {
                    $siblingFeatures = $candidate
                    break
                }
            }
        }
        $siblingCount++
        if ($null -eq $siblingFeatures) { $failures.Add("$context ㅈ 모사 엄지 각도 75도 미만") }
        Assert-Label "$context ㅈ 모사" $null ([SignRuleClassifier]::Classify($siblingFeatures))
    }
}

foreach ($group in $heldGroups) {
    if ($totals[$group] -eq 0) { $failures.Add("$group 유지 프레임 수 기대값=1 이상 실제값=0") }
}
Assert-Label 'null 특징' $null ([SignRuleClassifier]::Classify($null))
Assert-Label '짧은 특징' $null ([SignRuleClassifier]::Classify([float[]]::new([SignFeatures]::FeatureCount - 1)))
Assert-Label '긴 특징' $null ([SignRuleClassifier]::Classify([float[]]::new([SignFeatures]::FeatureCount + 1)))
if ($null -ne $validFeatures) {
    foreach ($invalid in [float]::NaN, [float]::PositiveInfinity, [float]::NegativeInfinity) {
        $invalidFeatures = [float[]]$validFeatures.Clone()
        $invalidFeatures[0] = $invalid
        Assert-Label "비유한 특징 ($invalid)" $null ([SignRuleClassifier]::Classify($invalidFeatures))
    }
}

$counts = $heldGroups | ForEach-Object { '{0} {1}/{2}' -f $_, $correct[$_], $totals[$_] }
Write-Host ('SignRule 유지 {0} | 좌우 반전 {1}프레임 | ±20° 회전 {2}건 | ㅌ·ㅈ 모사 {3}건 | 실패 {4}건' -f ($counts -join ', '), $mirroredCount, $rotationCount, $siblingCount, $failures.Count)
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    exit 1
}
exit 0
