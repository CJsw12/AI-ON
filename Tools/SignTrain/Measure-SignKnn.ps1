param([string]$Folder = (Join-Path $PSScriptRoot '../SignData/raw'), [Parameter(Mandatory=$true)][string]$Sealed, [int]$Seed = 1701, [int]$K = 5, [switch]$Final)
$ErrorActionPreference = 'Stop'
if ($K -lt 1) { throw 'k는 1 이상이어야 합니다.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = @('SignSample.cs','SignFeatures.cs','SignVote.cs','SignLabels.cs','KnnClassifier.cs','SignDataset.cs') | ForEach-Object { Join-Path $repo "Assets/Scripts/Sign/$_" }
Add-Type -Path $source
if (!(Test-Path -LiteralPath $Folder -PathType Container)) { throw '녹화 폴더가 없습니다.' }
$root = (Resolve-Path -LiteralPath $Folder).Path
$people = @(Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name | Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.csv' -File -Recurse).Count -gt 0 })
if (@($people | Where-Object Name -CEQ $Sealed).Count -ne 1) { throw '봉인 평가자 폴더가 없습니다.' }
$ordinary = @($people | Where-Object Name -CNE $Sealed)
if ($ordinary.Count -lt 2) { throw '봉인 평가자를 제외한 사람이 2명 이상 필요합니다.' }
$testPeople = if ($Final) { @($people | Where-Object Name -CEQ $Sealed) } else { $ordinary }
$report = [System.Collections.Generic.List[string]]::new()
function Line([string]$s) { $report.Add($s); Write-Host $s }
Line '# 게임과 다른 점(근사 항목)'
Line '- 손 없음 프레임은 저장되지 않아 미측정입니다. 손이 없으면 분류하지 않으며, 그 외 손 모양 오인식과 빈 화면·얼굴만 있는 경우의 실기 점검을 따로 확인하세요.'
Line '- 감지 사이 0.2초 이상은 투표 초기화로 근사합니다. 더 짧은 손 없음은 복원할 수 없습니다. 5초 성공률도 녹화 시작 기준 근사치입니다.'
Line '- 게임은 _other를 거절하므로 최종 혼동표에는 그 외 예측 대신 거절로 표시됩니다. 녹화 누락의 근거: SignRecorderWindow.cs:108-125, 게임 초기화: GameController.GazeHighlight.cs:140-157.'
if ($Final) { Line '경고: 봉인 최종 평가는 한 번만 수행하세요. 반복 실행 여부는 사람이 관리해야 합니다.' }
Line ("모드: {0}, 참여 인원: {1}, k: {2}, 시드: {3} (k-NN은 결정적이며 거리 표본 시드는 게임 고정값 1701)" -f $(if($Final){'최종'}else{'교차 검증'}), $testPeople.Count, $K, $Seed)
$matrix = @{}; $counts = @{}; $success5 = 0; $positive = 0
$temp = Join-Path ([IO.Path]::GetTempPath()) ('sign-measure-' + [guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory -Path $temp | Out-Null
 foreach ($person in $testPeople) {
  $train = Join-Path $temp 'train'; New-Item -ItemType Directory -Path $train | Out-Null
  $trainPeople = if ($Final) { $ordinary } else { @($ordinary | Where-Object Name -CNE $person.Name) }
  foreach ($p in $trainPeople) {
   foreach ($file in @(Get-ChildItem -LiteralPath $p.FullName -Filter '*.csv' -File -Recurse)) {
    $rows=[IO.File]::ReadAllLines($file.FullName)
    if ($rows.Count -eq 0 -or $rows[0].TrimStart([char]0xfeff) -cne [SignSample]::Header) { throw '학습 CSV 헤더가 잘못되었습니다.' }
    for ($j=1; $j -lt $rows.Length; $j++) { $row=$null; $err=$null; if (-not [SignSample]::TryParse($rows[$j],[ref]$row,[ref]$err) -or $row.Person -cne $p.Name) { throw '학습 CSV의 형식 또는 사람 ID가 폴더 가명과 일치하지 않습니다.' } }
   }
   Copy-Item -LiteralPath $p.FullName -Destination $train -Recurse
  }
  $data = [SignDataset]::Load($train)
  if ($data.IsEmpty -or $data.InvalidLines -gt 0) { throw '학습 CSV가 비었거나 유효하지 않은 행이 있습니다.' }
  $classifier = [KnnClassifier]::new($data.Samples,$data.Labels,$K,$data.SuggestedMaxDistance,1)
  Line ("평가자 {0}: 거리 상한 {1}, 최소 투표 마진 1" -f $person.Name,$data.SuggestedMaxDistance)
  $trials = @{}
  foreach ($file in @(Get-ChildItem -LiteralPath $person.FullName -Filter '*.csv' -File -Recurse | Sort-Object FullName)) {
   $lines = [IO.File]::ReadAllLines($file.FullName)
   if ($lines.Count -eq 0 -or $lines[0].TrimStart([char]0xfeff) -cne [SignSample]::Header) { throw 'CSV 헤더가 잘못되었습니다.' }
   for ($i=1; $i -lt $lines.Length; $i++) {
    $s=$null; $err=$null
    if (-not [SignSample]::TryParse($lines[$i],[ref]$s,[ref]$err)) { throw '평가 CSV에 유효하지 않은 행이 있습니다.' }
    if ($s.Person -cne $person.Name) { throw 'CSV의 사람 ID와 폴더 가명이 일치하지 않습니다.' }
    $key = $s.Session + "`0" + $s.Label + "`0" + $s.Trial
    if (!$trials.ContainsKey($key)) { $trials[$key] = [System.Collections.Generic.List[SignSample]]::new() }
    $trials[$key].Add($s)
   }
  }
  foreach ($key in @($trials.Keys | Sort-Object)) {
   $frames = @($trials[$key] | Sort-Object TimestampMs,Frame); $truth=$frames[0].Label; $vote=[SignVote]::new(); $prediction='거절'; $confirmedTime=-1
   foreach ($s in $frames) {
    $left=$false; $features=$null; $label=[SignVote]::NoHand
    if ([SignHandedness]::TryIsLeft($s.Handedness,$false,[ref]$left) -and [SignFeatures]::TryCompute($s.Landmarks,$s.Width,$s.Height,$left,[ref]$features)) { $label=$classifier.Classify($features).PredictedLabel }
    $v=$vote.Add($s.TimestampMs,$label)
    if ($v.Confirmed) { $prediction=$data.Labels[$v.Label]; $confirmedTime=$s.TimestampMs; break }
   }
   $mk=$truth + "`0" + $prediction; if (!$matrix.ContainsKey($mk)) { $matrix[$mk]=0 }; $matrix[$mk]++
   $ck=$person.Name + "`0" + $truth; if (!$counts.ContainsKey($ck)) { $counts[$ck]=0 }; $counts[$ck]++
   if ($truth -cne '_other') { $positive++; if ($prediction -ceq $truth -and $confirmedTime -le 5000) { $success5++ } }
  }
  Remove-Item -LiteralPath $train -Recurse -Force
 }
 Line '사람·글자별 시도 수:'
 foreach ($key in @($counts.Keys | Sort-Object)) { $a=$key.Split([char]0); Line ("{0} | {1} | {2}" -f $a[0],$a[1],$counts[$key]) }
 Line '혼동표 (실제 | 예측 | 시도 수):'
 foreach ($key in @($matrix.Keys | Sort-Object)) { $a=$key.Split([char]0); Line ("{0} | {1} | {2}" -f $a[0],$a[1],$matrix[$key]) }
 $labels=@($matrix.Keys | ForEach-Object { $_.Split([char]0)[0] } | Where-Object { $_ -cne '_other' } | Sort-Object -Unique)
 $rates=@(); Line '글자별 재현율:'
 foreach ($l in $labels) { $total=0; foreach ($key in $matrix.Keys) { if ($key.Split([char]0)[0] -ceq $l) { $total += $matrix[$key] } }; $correct=$matrix[$l+"`0"+$l]; $rate=100.0*$correct/$total; $rates += $rate; Line ("{0} | {1:N1}% ({2}/{3}) | {4}" -f $l,$rate,$correct,$total,$(if($rate -ge 80){'80% 이상'}else{'80% 미달'})) }
 $other=0; $falseCount=0; foreach ($key in $matrix.Keys) { $a=$key.Split([char]0); if ($a[0] -ceq '_other') { $other += $matrix[$key]; if ($a[1] -cne '거절') { $falseCount += $matrix[$key] } } }
 if ($other -gt 0) { Line ("그 외 오인식률: {0:N1}% ({1}/{2}); 손 없음 미측정이므로 전체 기준 판정 불가" -f (100.0*$falseCount/$other),$falseCount,$other) } else { Line '그 외 오인식률: 미측정 (시도 없음); 손 없음 미측정' }
 if ($positive -gt 0) { Line ("5초 안 성공률(근사): {0:N1}% ({1}/{2})" -f (100.0*$success5/$positive),$success5,$positive) }
 if ($rates.Count -gt 0) { $avg=($rates | Measure-Object -Average).Average; Line ("평균 재현율 {0:N1}%: {1}" -f $avg,$(if($avg -lt 60){'범위 축소 검토'}elseif($avg -lt 80){'혼동 쌍 제거 후 재측정'}else{'글자별 80% 이상 및 오인식률 10% 이하를 각각 확인'})) }
 Line '혼동이 큰 글자 쌍(상위 3):'
 $pairs=@{}; foreach ($key in $matrix.Keys) { $a=$key.Split([char]0); if ($a[0] -cne '_other' -and $a[1] -cne '거절' -and $a[0] -cne $a[1]) { $pair=(@($a[0],$a[1]) | Sort-Object) -join ' / '; if (!$pairs.ContainsKey($pair)) { $pairs[$pair]=0 }; $pairs[$pair]+=$matrix[$key] } }
 foreach ($pair in @($pairs.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3)) { Line ("{0} | {1}회" -f $pair.Key,$pair.Value) }
 $outdir=Join-Path $repo 'Tools/SignData/reports'; New-Item -ItemType Directory -Path $outdir -Force | Out-Null
 $path=Join-Path $outdir ((Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8) + '.md')
 [IO.File]::WriteAllLines($path,$report,[Text.UTF8Encoding]::new($true)); Write-Host "결과 저장: $path"
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
