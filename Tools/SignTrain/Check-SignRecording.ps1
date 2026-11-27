param([string]$Folder = (Join-Path $PSScriptRoot '../SignData/raw'), [string]$Person = '', [string]$Labels = '', [int]$Target = 15, [int]$MinFrames = 10, [switch]$RequireImages)
# 녹화 CSV가 학습에 쓸 수 있는지 점검한다. 좌표 값은 출력하지 않고 개수·판정만 보여 준다(사람·에이전트 공용).
# 종료 코드: 0 = 형식 정상이고 목표 충족, 1 = 파일 형식 오류, 2 = 형식은 정상이나 목표 미달.
# 손 이미지(<세션>_images/*.jpg)는 글자별 개수만 센다. -RequireImages 이면 이미지가 없는 글자도 목표 미달(2)로 본다.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not ('SignSample' -as [type])) { Add-Type -Path (Join-Path $repo 'Assets/Scripts/Sign/SignSample.cs') }
if ($Target -lt 1 -or $MinFrames -lt 1) { Write-Host '목표와 최소 프레임은 1 이상이어야 합니다.'; exit 1 }
if (!(Test-Path -LiteralPath $Folder -PathType Container)) { Write-Host "녹화 폴더가 없습니다: $Folder"; exit 1 }
$root = (Resolve-Path -LiteralPath $Folder).Path
$people = @(Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name | Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.csv' -File -Recurse).Count -gt 0 })
if ($Person) { $people = @($people | Where-Object Name -CEQ $Person) }
if ($people.Count -eq 0) { Write-Host '점검할 CSV가 없습니다. 폴더 구조는 <녹화 폴더>/<가명>/<세션>.csv 입니다.'; exit 1 }
$expected = @(); if ($Labels) { $expected = @($Labels.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
$errors = [System.Collections.Generic.List[string]]::new(); $short = 0; $warnings = [System.Collections.Generic.List[string]]::new()
$totalSummary = [System.Collections.Generic.List[string]]::new()
foreach ($p in $people) {
 $trials = @{}   # 키: 세션|글자|시도 -> 프레임 수와 시간 순서
 $hands = @{ Left = 0; Right = 0 }; $sessions = [System.Collections.Generic.List[string]]::new()
 $images = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::Ordinal)   # 글자 -> 이미지 수(대소문자 구분)
 $imgKeys = [System.Collections.Generic.List[string]]::new()
 $badImages = 0
 foreach ($file in @(Get-ChildItem -LiteralPath $p.FullName -Filter '*.csv' -File -Recurse | Sort-Object FullName)) {
  $rel = $file.FullName.Substring($root.Length).TrimStart('\','/')
  $base = [IO.Path]::GetFileNameWithoutExtension($file.Name); $sessions.Add($base)
  $imageDir = Join-Path $file.DirectoryName ($base + '_images')
  if (Test-Path -LiteralPath $imageDir -PathType Container) {
   foreach ($img in @(Get-ChildItem -LiteralPath $imageDir -Filter '*.jpg' -File)) {
    $m = [regex]::Match($img.Name, '^(\d+)-(\d+)-([LR])-(.+)\.jpg$')
    if ($m.Success) { $lab = $m.Groups[4].Value; if (-not $images.ContainsKey($lab)) { $images[$lab] = 0 }; $images[$lab]++; $imgKeys.Add($base + [char]0 + $lab + [char]0 + [int]$m.Groups[1].Value) } else { $badImages++ }
   }
  }
  $lines = [IO.File]::ReadAllLines($file.FullName)
  if ($lines.Count -eq 0 -or $lines[0].TrimStart([char]0xfeff) -cne [SignSample]::Header) { $errors.Add("${rel}: 헤더가 녹화 도구 형식과 다릅니다."); continue }
  for ($i = 1; $i -lt $lines.Length; $i++) {
   if ([string]::IsNullOrWhiteSpace($lines[$i])) { continue }
   $s = $null; $err = $null
   if (-not [SignSample]::TryParse($lines[$i], [ref]$s, [ref]$err)) { $errors.Add("${rel}:$($i + 1): 읽을 수 없는 줄입니다(열 개수·숫자·손 구분을 확인)."); continue }
   if ($s.Person -cne $p.Name) { $errors.Add("${rel}:$($i + 1): CSV 안의 사람 ID가 폴더 가명과 다릅니다."); continue }
   if ($s.Session -cne $base) { $errors.Add("${rel}:$($i + 1): CSV 안의 세션 ID가 파일 이름과 다릅니다."); continue }
   $hands[$s.Handedness]++
   $key = $s.Session + [char]0 + $s.Label + [char]0 + $s.Trial
   if (-not $trials.ContainsKey($key)) { $trials[$key] = @{ Label = $s.Label; Frames = 0; Last = [long]::MinValue; Backwards = $false } }
   $t = $trials[$key]; $t.Frames++
   if ($s.TimestampMs -lt $t.Last) { $t.Backwards = $true }
   $t.Last = $s.TimestampMs
  }
 }
 $orphans = 0
 foreach ($k in $imgKeys) { if (-not $trials.ContainsKey($k)) { $orphans++ } }
 $byLabel = @{}
 foreach ($t in $trials.Values) {
  if (-not $byLabel.ContainsKey($t.Label)) { $byLabel[$t.Label] = @{ Valid = 0; Short = 0; Frames = 0 } }
  $b = $byLabel[$t.Label]; $b.Frames += $t.Frames
  if ($t.Frames -ge $MinFrames) { $b.Valid++ } else { $b.Short++; $short++ }
  if ($t.Backwards) { $warnings.Add("$($p.Name): 시간 순서가 거꾸로인 시도가 있습니다(글자 $($t.Label)).") }
 }
 Write-Host ''
 Write-Host ("== {0}  (세션 {1}개: {2})" -f $p.Name, $sessions.Count, ($sessions -join ', '))
 Write-Host ('{0,-10}{1,10}{2,10}{3,10}{4,8}  {5}' -f '글자', '유효 시도', '짧은 시도', '프레임', '이미지', '판정')
 $labelsHere = @($byLabel.Keys | Sort-Object @{ Expression = { $_ -ceq '_other' } }, @{ Expression = { $_ } })   # _other 는 맨 아래
 foreach ($label in $labelsHere) {
  $b = $byLabel[$label]
  $verdict = if ($b.Valid -ge $Target) { 'OK' } else { "부족 ($($Target - $b.Valid)회 더)" }
  if ($b.Valid -lt $Target) { $totalSummary.Add("$($p.Name) / $label : 유효 시도 $($b.Valid) / $Target") }
  $imgCount = if ($images.ContainsKey($label)) { $images[$label] } else { 0 }
  if ($RequireImages -and $imgCount -eq 0 -and $b.Valid -gt 0) { $totalSummary.Add("$($p.Name) / $label : 손 이미지 없음"); $verdict += ' · 이미지 없음' }
  Write-Host ('{0,-10}{1,10}{2,10}{3,10}{4,8}  {5}' -f $label, $b.Valid, $b.Short, $b.Frames, $imgCount, $verdict)
 }
 if ($labelsHere -cnotcontains '_other' -and $expected -cnotcontains '_other') { $totalSummary.Add("$($p.Name) / _other : 글자 목록에 없음(선정 글자가 아닌 손 모양도 녹화해야 합니다)"); Write-Host '_other 가 없습니다. 선정 글자가 아닌 손 모양도 녹화하세요.' }
 foreach ($e in $expected) { if ($labelsHere -cnotcontains $e) { $totalSummary.Add("$($p.Name) / $e : 녹화 없음"); Write-Host "예상 글자 $e 의 녹화가 없습니다." } }
 foreach ($l in $labelsHere) { if ($expected.Count -gt 0 -and $expected -cnotcontains $l) { $warnings.Add("$($p.Name): 예상 목록에 없는 글자 '$l' 이(가) 있습니다. 이름을 잘못 입력했는지 확인하세요.") } }
 if ($images.Count -eq 0) { $warnings.Add("$($p.Name): 손 이미지가 없습니다. 이미지 학습(GPU CNN)에는 녹화 도구의 '손 이미지도 함께 저장'을 켜고 다시 녹화해야 합니다.") }
 if ($orphans -gt 0) { $warnings.Add("$($p.Name): CSV에 없는 시도의 이미지 $orphans 장이 있습니다(지운 시도의 이미지가 남았을 수 있습니다).") }
 if ($badImages -gt 0) { $warnings.Add("$($p.Name): 이름을 해석할 수 없는 이미지 $badImages 개가 있습니다(파일 이름을 바꾸지 마세요).") }
 if ($sessions.Count -lt 2) { $warnings.Add("$($p.Name): 세션이 1개뿐입니다. 다른 날·다른 조명의 세션을 한 번 더 녹화하세요(권장).") }
 if ($hands.Left -gt 0 -and $hands.Right -gt 0) { $warnings.Add("$($p.Name): 왼손과 오른손이 섞여 있습니다(왼손 $($hands.Left)프레임, 오른손 $($hands.Right)프레임). 한 손으로 통일했는지 확인하세요.") }
}
Write-Host ''
if ($errors.Count -gt 0) {
 Write-Host "[형식 오류 $($errors.Count)건] 이 파일들은 학습에 쓸 수 없습니다. 값은 보여 주지 않고 위치만 알려 줍니다."
 $errors | Select-Object -First 20 | ForEach-Object { Write-Host "  - $_" }
 if ($errors.Count -gt 20) { Write-Host "  ... 외 $($errors.Count - 20)건" }
 exit 1
}
if ($warnings.Count -gt 0) { Write-Host '[주의]'; $warnings | Select-Object -Unique | ForEach-Object { Write-Host "  - $_" } }
if ($short -gt 0) { Write-Host "[짧은 시도 $short 개] 유효 프레임이 $MinFrames 미만이라 세지 않았습니다. 손을 더 오래 유지해 다시 녹화하세요." }
if ($totalSummary.Count -gt 0) { Write-Host '[목표 미달]'; $totalSummary | ForEach-Object { Write-Host "  - $_" }; exit 2 }
Write-Host "결과: 형식 정상, 모든 글자가 목표($Target 회) 이상입니다."
exit 0
