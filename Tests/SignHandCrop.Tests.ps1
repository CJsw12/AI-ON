$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Add-Type -Path (Join-Path $repo 'Assets/Scripts/Sign/SignHandCrop.cs')
function Assert($ok, $message) { if (!$ok) { throw $message } }
function Near([double]$a, [double]$b, [double]$tol) { return [Math]::Abs($a - $b) -le $tol }
# 손 21점: 중심 (cx,cy), 가로 w, 세로 h 범위(0~1)에 흩뿌린다.
function Hand([double]$cx, [double]$cy, [double]$w, [double]$h) {
 $l = New-Object 'float[]' 63
 for ($i = 0; $i -lt 21; $i++) { $t = $i / 20.0; $l[3*$i] = [float]($cx - $w/2 + $w*$t); $l[3*$i+1] = [float]($cy - $h/2 + $h*(1-$t)); $l[3*$i+2] = 0 }
 return $l
}
$x = 0; $y = 0; $s = 0
# 1. 가운데 손: 가로 0.2·세로 0.3(640×480 → 128×144px), 여유 25% → 한 변 144×1.5=216
Assert ([SignHandCrop]::TryRect((Hand .5 .5 .2 .3), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '가운데 손 자르기'
Assert ((Near $s 216 1) -and (Near $x 212 1) -and (Near $y 132 1)) "가운데 손 사각형: $x,$y,$s"
# 2. 모서리 손은 영상 안쪽으로 밀린다
Assert ([SignHandCrop]::TryRect((Hand .05 .05 .2 .3), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '모서리 손'
Assert (($x -ge 0) -and ($y -ge 0) -and ($x + $s -le 640) -and ($y + $s -le 480)) "영상 밖으로 나감: $x,$y,$s"
Assert ([SignHandCrop]::TryRect((Hand .95 .95 .2 .3), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '반대 모서리 손'
Assert (($x -ge 0) -and ($y -ge 0) -and ($x + $s -le 640) -and ($y + $s -le 480)) "반대 모서리: $x,$y,$s"
# 3. 영상보다 큰 손은 짧은 변에 맞춘다
Assert ([SignHandCrop]::TryRect((Hand .5 .5 .9 .95), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '큰 손'
Assert (($s -eq 480) -and ($y -eq 0)) "큰 손 크기: $s"
# 4. 너무 작은 손, NaN, 길이 오류는 거절
Assert (-not [SignHandCrop]::TryRect((Hand .5 .5 .005 .005), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '너무 작은 손 거절'
$bad = Hand .5 .5 .2 .3; $bad[7] = [float]::NaN
Assert (-not [SignHandCrop]::TryRect($bad, 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) 'NaN 거절'
Assert (-not [SignHandCrop]::TryRect((New-Object 'float[]' 10), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '길이 오류 거절'
# 5. 방향 변환: WebCamTexture 배열(좌하단 원점)로
$sx = 0; $sy = 0
[SignHandCrop]::ToSource(1, 2, 8, 6, $false, $false, [ref]$sx, [ref]$sy); Assert (($sx -eq 1) -and ($sy -eq 2)) 'flip 없음'
[SignHandCrop]::ToSource(1, 2, 8, 6, $true, $false, [ref]$sx, [ref]$sy); Assert (($sx -eq 6) -and ($sy -eq 2)) 'flipH'
[SignHandCrop]::ToSource(1, 2, 8, 6, $false, $true, [ref]$sx, [ref]$sy); Assert (($sx -eq 1) -and ($sy -eq 3)) 'flipV(기본값)'
[SignHandCrop]::ToSource(0, 0, 8, 6, $true, $true, [ref]$sx, [ref]$sy); Assert (($sx -eq 7) -and ($sy -eq 5)) 'flipH+flipV'
# 6. CropRgb: 8×6 영상, 픽셀 색이 (원본 칸 좌표)를 담는다. 6×6 사각형을 그대로(축소 없이) 자르면 변환과 정확히 일치해야 한다.
$w = 8; $h = 6; $rgba = New-Object 'byte[]' ($w * $h * 4)
for ($py = 0; $py -lt $h; $py++) { for ($px = 0; $px -lt $w; $px++) { $i = ($py * $w + $px) * 4; $rgba[$i] = [byte]($px * 30); $rgba[$i+1] = [byte]($py * 40); $rgba[$i+2] = 7; $rgba[$i+3] = 255 } }
foreach ($flipH in @($false, $true)) { foreach ($flipV in @($false, $true)) {
 $rgb = [SignHandCrop]::CropRgb($rgba, $w, $h, $flipH, $flipV, 1, 0, 6, 6)
 Assert ($rgb.Length -eq 108) '출력 크기'
 foreach ($p in @(@(0,0), @(5,0), @(0,5), @(5,5), @(2,3))) {
  $ox = $p[0]; $oy = $p[1]; [SignHandCrop]::ToSource(1 + $ox, $oy, $w, $h, $flipH, $flipV, [ref]$sx, [ref]$sy)
  $o = ($oy * 6 + $ox) * 3
  Assert (($rgb[$o] -eq $sx * 30) -and ($rgb[$o+1] -eq $sy * 40) -and ($rgb[$o+2] -eq 7)) "픽셀 불일치 flipH=$flipH flipV=$flipV ($ox,$oy)"
 } } }
# 7. 축소는 이웃 평균(값 범위 안, 순서 유지)
$small = [SignHandCrop]::CropRgb($rgba, $w, $h, $false, $false, 0, 0, 6, 3)
Assert (($small.Length -eq 27) -and ($small[0] -lt $small[(2 * 3)])) '축소 보간: 왼쪽에서 오른쪽으로 R이 커져야 함'
# 8. 줄 뒤집기
$f = [SignHandCrop]::FlipRows([byte[]](1,2,3, 4,5,6, 7,8,9, 10,11,12), 2)
Assert (($f[0] -eq 7) -and ($f[3] -eq 10) -and ($f[6] -eq 1) -and ($f[9] -eq 4)) '줄 뒤집기'
# 9. 입력 오류는 예외
$threw = $false; try { [void][SignHandCrop]::CropRgb($rgba, $w, $h, $false, $false, 4, 0, 6, 6) } catch { $threw = $true }
Assert $threw '영상 밖 사각형은 예외'
# 10. 캡처 간격: 시작 후 300ms 전은 건너뛰고, 그 뒤 150ms 간격, 최대 10장
Assert (-not [SignHandCrop]::ShouldCapture(0, -1, 0)) '시작 직후는 건너뜀'
Assert (-not [SignHandCrop]::ShouldCapture(299, -1, 0)) '300ms 전'
Assert ([SignHandCrop]::ShouldCapture(300, -1, 0)) '300ms부터 첫 장'
Assert (-not [SignHandCrop]::ShouldCapture(400, 300, 1)) '간격 부족'
Assert ([SignHandCrop]::ShouldCapture(450, 300, 1)) '간격 충족'
Assert (-not [SignHandCrop]::ShouldCapture(5000, 300, 10)) '최대 10장'
# 10-2. 너무 작은 손은 거절(최소 한 변 48px)
Assert (-not [SignHandCrop]::TryRect((Hand .5 .5 .04 .04), 640, 480, 0.25, [ref]$x, [ref]$y, [ref]$s)) '48px 미만 거절'
# 11. 파일 이름 왕복, 밑줄 글자, 왼손, 잘못된 이름
$n = [SignHandCrop]::FileName(12, 3, $false, '_other'); Assert ($n -ceq '012-03-R-_other.jpg') "이름: $n"
$t = 0; $i = 0; $left = $false; $label = $null
Assert ([SignHandCrop]::TryParseFileName($n, [ref]$t, [ref]$i, [ref]$left, [ref]$label)) '해석'
Assert (($t -eq 12) -and ($i -eq 3) -and (-not $left) -and ($label -ceq '_other')) '해석 값'
$n2 = [SignHandCrop]::FileName(7, 10, $true, [string][char]0xBBF0)  # 한글 글자
Assert ([SignHandCrop]::TryParseFileName($n2, [ref]$t, [ref]$i, [ref]$left, [ref]$label)) '한글 글자 해석'
Assert (($t -eq 7) -and ($i -eq 10) -and $left -and ($label -ceq [string][char]0xBBF0)) '한글 값'
foreach ($badName in @('x.jpg', '012-03-X-a.jpg', '012-03-R-.jpg', 'a-b-R-c.jpg', '012-03-R-ㅂ.png', '')) {
 Assert (-not [SignHandCrop]::TryParseFileName($badName, [ref]$t, [ref]$i, [ref]$left, [ref]$label)) "잘못된 이름 거절: $badName"
}
Assert ([SignHandCrop]::IsSafeLabel('_other') -and [SignHandCrop]::IsSafeLabel('ㅂ') -and -not [SignHandCrop]::IsSafeLabel('a/b') -and -not [SignHandCrop]::IsSafeLabel('a-b') -and -not [SignHandCrop]::IsSafeLabel('')) '글자 이름 검사'
Write-Host 'SignHandCrop tests passed'
