$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$check = Join-Path $repo 'Tools/SignTrain/Check-SignRecording.ps1'
if (-not (Test-Path $check)) { throw '녹화 점검 도구가 없습니다.' }
Add-Type -Path (Join-Path $repo 'Assets/Scripts/Sign/SignSample.cs')
$fixture = (Get-Content (Join-Path $PSScriptRoot 'Fixtures/sign-features.json') -Raw -Encoding UTF8 | ConvertFrom-Json)[0]
$root = Join-Path ([IO.Path]::GetTempPath()) ('sign-check-test-' + [guid]::NewGuid().ToString('N'))
function Assert($ok, $message) { if (!$ok) { throw $message } }
# 사람 폴더/세션 CSV 하나를 만든다. $labels = @{ 글자 = @(시도마다 프레임 수) }
function Build([string]$person, [string]$session, $labels, [string]$csvPerson = '', [string]$header = '', [string]$hand = 'Right') {
 $dir = Join-Path $root $person; New-Item -ItemType Directory -Force $dir | Out-Null
 $rows = [System.Collections.Generic.List[string]]::new(); $rows.Add($(if ($header) { $header } else { [SignSample]::Header }))
 foreach ($label in $labels.Keys) {
  $trial = 0
  foreach ($count in $labels[$label]) {
   $trial++
   for ($frame = 0; $frame -lt $count; $frame++) {
    $s = [SignSample]::new(); $s.Person = $(if ($csvPerson) { $csvPerson } else { $person }); $s.Session = $session; $s.Trial = "$trial"; $s.Label = $label
    $s.Frame = $frame; $s.TimestampMs = $frame * 100; $s.Width = 640; $s.Height = 480; $s.Handedness = $hand; $s.Landmarks = [float[]]$fixture.landmarks.Clone()
    $s.Landmarks[3] = [float]($s.Landmarks[3] + 0.0123456789 * $trial)
    $rows.Add($s.ToCsvLine())
   }
  }
 }
 [IO.File]::WriteAllLines((Join-Path $dir "$session.csv"), $rows, [Text.UTF8Encoding]::new($false))
}
function Run([string[]]$extra) {
 $ErrorActionPreference = 'Continue'
 $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $check -Folder $root @extra 2>&1 | Out-String
 $code = $LASTEXITCODE
 $ErrorActionPreference = 'Stop'
 return @{ Code = $code; Text = $out }
}
function Reset { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
$good = [ordered]@{ A = @(12, 12, 12); B = @(12, 12, 12); '_other' = @(12, 12, 12) }
try {
 # 1. 정상: 형식 OK, 목표 3회 충족 → 0, 좌표 값은 출력하지 않는다.
 Reset; Build 'p01' 's01' $good
 $r = Run @('-Target', '3', '-Labels', 'A,B,_other')
 Assert ($r.Code -eq 0) ('정상 데이터는 종료 코드 0이어야 합니다: ' + $r.Code + ' ' + $r.Text)
 Assert ($r.Text -match 'OK' -and $r.Text -match 'p01') '정상 판정 표시'
 Assert ($r.Text -notmatch '\d\.\d{6,}' -and $r.Text -notmatch 'x0|y0|z0') '좌표 값이 출력에 섞이면 안 됩니다'
 Assert ($r.Text -match '세션이 1개뿐') '세션 1개 경고'
 # 2. 목표 미달 → 2
 $r = Run @('-Target', '4')
 Assert ($r.Code -eq 2 -and $r.Text -match '부족') ('목표 미달은 2: ' + $r.Code)
 # 3. 짧은 시도(프레임 5개)는 세지 않는다 → 미달 2
 Reset; Build 'p01' 's01' ([ordered]@{ A = @(12, 12, 5); B = @(12, 12, 12); '_other' = @(12, 12, 12) })
 $r = Run @('-Target', '3')
 Assert ($r.Code -eq 2 -and $r.Text -match '짧은 시도') ('짧은 시도: ' + $r.Code + ' ' + $r.Text)
 # 4. CSV 안의 사람 ID가 폴더 가명과 다름 → 1, 줄 위치만 알려 준다
 Reset; Build 'p01' 's01' $good 'p99'
 $r = Run @()
 Assert ($r.Code -eq 1 -and $r.Text -match '사람 ID') ('사람 ID 불일치: ' + $r.Code)
 # 5. 헤더 오류 → 1
 Reset; Build 'p01' 's01' $good '' 'a,b,c'
 $r = Run @()
 Assert ($r.Code -eq 1 -and $r.Text -match '헤더') ('헤더 오류: ' + $r.Code)
 # 6. 예상 글자 누락 → 2
 Reset; Build 'p01' 's01' $good
 $r = Run @('-Target', '3', '-Labels', 'A,B,C,_other')
 Assert ($r.Code -eq 2 -and $r.Text -match '예상 글자 C') ('글자 누락: ' + $r.Code + ' ' + $r.Text)
 # 7. _other 없음 → 2
 Reset; Build 'p01' 's01' ([ordered]@{ A = @(12, 12, 12); B = @(12, 12, 12) })
 $r = Run @('-Target', '3')
 Assert ($r.Code -eq 2 -and $r.Text -match '_other') ('_other 없음: ' + $r.Code)
 # 8. 세션 2개 + 왼손 섞임 경고, -Person 은 그 사람만 점검
 Reset; Build 'p01' 's01' $good; Build 'p01' 's02' $good '' '' 'Left'; Build 'p02' 's01' $good 'p98'
 $r = Run @('-Target', '3', '-Person', 'p01')
 Assert ($r.Code -eq 0) ('-Person 은 다른 사람의 오류를 무시해야 합니다: ' + $r.Code + ' ' + $r.Text)
 Assert ($r.Text -match '왼손과 오른손이 섞여' -and $r.Text -notmatch '세션이 1개뿐') '세션 2개와 손 섞임'
 $r = Run @('-Target', '3')
 Assert ($r.Code -eq 1) '전체 점검은 p02 의 오류로 1'
 # 9. 폴더 없음 → 1
 Reset
 $r = Run @()
 Assert ($r.Code -eq 1) '폴더 없음'
 Write-Host 'SignRecordingCheck tests passed'
} finally { Reset }
