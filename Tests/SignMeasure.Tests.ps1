$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$measure = Join-Path $repo 'Tools/SignTrain/Measure-SignKnn.ps1'
if (-not (Test-Path $measure)) { throw '측정 도구가 없습니다.' }
Add-Type -Path (Join-Path $repo 'Assets/Scripts/Sign/SignSample.cs')
$fixture = (Get-Content (Join-Path $PSScriptRoot 'Fixtures/sign-features.json') -Raw -Encoding UTF8 | ConvertFrom-Json)[0]
$root = Join-Path ([IO.Path]::GetTempPath()) ('sign-measure-test-' + [guid]::NewGuid().ToString('N'))
function Assert($ok,$message) { if (!$ok) { throw $message } }
function BuildData([bool]$confuse,[bool]$otherLikeA) {
 foreach ($person in @('p01','p02','p03','sealed')) {
  $dir=Join-Path $root $person; New-Item -ItemType Directory -Force $dir | Out-Null
  $rows=[System.Collections.Generic.List[string]]::new(); $rows.Add([SignSample]::Header)
  $rng=[Random]::new(100 + [int][char]$person[$person.Length-1])
  foreach ($label in @('A','B','C','_other')) { for ($trial=1;$trial -le 3;$trial++) { $noise=$rng.NextDouble()*0.00001; for ($frame=0;$frame -le 12;$frame++) {
   $s=[SignSample]::new(); $s.Person=$person; $s.Session='s01'; $s.Trial="$trial"; $s.Label=$label; $s.Frame=$frame; $s.TimestampMs=$frame*100; $s.Width=640; $s.Height=480; $s.Handedness='Right'; $s.Landmarks=[float[]]$fixture.landmarks.Clone()
   $shape=switch ($label) { 'A' {0} 'B' {if($confuse){0}else{1}} 'C' {2} default {if($otherLikeA -and $person -eq 'p01'){0}else{3}} }
   $s.Landmarks[3]=[float]($s.Landmarks[3] + 0.08*$shape + $noise)
   $rows.Add($s.ToCsvLine())
  } } }
  [IO.File]::WriteAllLines((Join-Path $dir 's01.csv'),$rows,[Text.UTF8Encoding]::new($false))
 }
}
try {
 BuildData $false $false
 $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $measure -Folder $root -Sealed sealed -K 1 2>&1
 Assert ($LASTEXITCODE -eq 0) ('잘 분리된 데이터 실행 실패: '+($output -join ' '))
 $reports=Join-Path $repo 'Tools/SignData/reports'; $latest=Get-ChildItem $reports -Filter '*.md' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 $text=Get-Content -LiteralPath $latest.FullName -Raw -Encoding UTF8
 Assert ($text -match 'A \| 100.0%' -and $text -match 'B \| 100.0%' -and $text -match 'C \| 100.0%') '재현율'
 Assert ($text -notmatch 'sealed \|') '봉인 평가자 제외'
 Write-Host ($text -split "`r?`n" | Select-Object -First 25 | Out-String)
 $output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $measure -Folder $root -Sealed sealed -K 1 -Final 2>&1
 Assert ($LASTEXITCODE -eq 0) '봉인 최종 평가 실행'
 $latest=Get-ChildItem $reports -Filter '*.md' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; $finalText=Get-Content $latest.FullName -Raw -Encoding UTF8
 Assert ($finalText -match '봉인 최종 평가는 한 번만' -and $finalText -match 'sealed \|' -and $finalText -notmatch 'p01 \|') '봉인 최종 평가 분리'
 BuildData $true $false
 $output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $measure -Folder $root -Sealed sealed -K 1 2>&1
 Assert ($LASTEXITCODE -eq 0) '혼동 데이터 실행'
 $latest=Get-ChildItem $reports -Filter '*.md' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; $text=Get-Content $latest.FullName -Raw -Encoding UTF8
 Assert ($text -match 'A / B') '혼동 쌍'
 BuildData $false $true
 $output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $measure -Folder $root -Sealed sealed -K 1 2>&1
 Assert ($LASTEXITCODE -eq 0) '그 외 데이터 실행'
 $latest=Get-ChildItem $reports -Filter '*.md' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; $text=Get-Content $latest.FullName -Raw -Encoding UTF8
 Assert ($text -match '그 외 오인식률: (?!0.0%)') '그 외 오인식률'
 Remove-Item -LiteralPath (Join-Path $root 'p02'),(Join-Path $root 'p03') -Recurse -Force
 $ErrorActionPreference='Continue'
 $output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $measure -Folder $root -Sealed sealed 2>&1
 $ErrorActionPreference='Stop'
 Assert ($LASTEXITCODE -ne 0) '한 사람 오류'
 Write-Host 'SignMeasure tests passed'
} finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
