$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$geomPath = Join-Path $repo 'Assets/Scripts/Chemistry/MoleculeGeometry.cs'
Add-Type -Path $geomPath

function Assert($condition, $message) {
    if (-not $condition) { throw $message }
}

foreach ($id in [MoleculeGeometry]::AllIds) {
    $scene = [MoleculeGeometry]::BuildScene($id, $null, [MoleculeGeometry]::InitialYawDegrees($id), [MoleculeGeometry]::InitialTiltDegrees($id), 0, 0, 100, 6.5, 20, $false)
    for ($i = 0; $i -lt $scene.Atoms.Length; $i++) { for ($j = $i + 1; $j -lt $scene.Atoms.Length; $j++) {
        $a = $scene.Atoms[$i]; $b = $scene.Atoms[$j]
        $distance = [Math]::Sqrt([Math]::Pow($a.X-$b.X,2) + [Math]::Pow($a.Y-$b.Y,2))
        Assert ($distance -ge 0.9 * ($a.RadiusPx + $b.RadiusPx)) "initial atom overlap $id $i $j"
    } }
}
# Full auto-rotation (5 deg steps): atoms never collapse onto each other (linear molecules must not look like a single dot).
foreach ($id in [MoleculeGeometry]::AllIds) {
    $tilt = [double][MoleculeGeometry]::InitialTiltDegrees($id)
    for ($yaw = 0; $yaw -lt 360; $yaw += 5) {
        $scene = [MoleculeGeometry]::BuildScene($id, $null, [double]$yaw, $tilt, 0, 0, 100, 6.5, 20, $false)
        for ($i = 0; $i -lt $scene.Atoms.Length; $i++) { for ($j = $i + 1; $j -lt $scene.Atoms.Length; $j++) {
            $a = $scene.Atoms[$i]; $b = $scene.Atoms[$j]
            $distance = [Math]::Sqrt([Math]::Pow($a.X-$b.X,2) + [Math]::Pow($a.Y-$b.Y,2))
            Assert ($distance -ge 0.75 * ($a.RadiusPx + $b.RadiusPx)) "rotation atom overlap $id yaw $yaw $i $j"
        } }
    }
}

# 1. 10개 분자 규약(원자 수·순서·원소, 결합, 비공유 전자쌍, 표시 각) 검증
$expected = @(
    @{
        Id = 'H2O'; Atoms = @('O','H','H'); Bonds = @(@(0,1),@(0,2));
        LonePairsTotal = 2; LonePairsByAtom = @{ 0 = 2 };
        Angles = @(@{ Target = 104.5; Kind = [MoleculeGeometry+AngleKind]::Arc })
    },
    @{
        Id = 'NH3'; Atoms = @('N','H','H','H'); Bonds = @(@(0,1),@(0,2),@(0,3));
        LonePairsTotal = 1; LonePairsByAtom = @{ 0 = 1 };
        Angles = @(@{ Target = 107.0; Kind = [MoleculeGeometry+AngleKind]::Arc })
    },
    @{
        Id = 'CH4'; Atoms = @('C','H','H','H','H'); Bonds = @(@(0,1),@(0,2),@(0,3),@(0,4));
        LonePairsTotal = 0; LonePairsByAtom = @{};
        Angles = @(@{ Target = 109.5; Kind = [MoleculeGeometry+AngleKind]::Arc })
    },
    @{
        Id = 'CO2'; Atoms = @('C','O','O'); Bonds = @(@(0,1),@(0,2));
        LonePairsTotal = 4; LonePairsByAtom = @{ 1 = 2; 2 = 2 };
        Angles = @(@{ Target = 180.0; Kind = [MoleculeGeometry+AngleKind]::Straight })
    },
    @{
        Id = 'N2'; Atoms = @('N','N'); Bonds = @(,(@(0,1)));
        LonePairsTotal = 2; LonePairsByAtom = @{ 0 = 1; 1 = 1 };
        Angles = @()
    },
    @{
        Id = 'O2'; Atoms = @('O','O'); Bonds = @(,(@(0,1)));
        LonePairsTotal = 4; LonePairsByAtom = @{ 0 = 2; 1 = 2 };
        Angles = @()
    },
    @{
        Id = 'HCN'; Atoms = @('C','H','N'); Bonds = @(@(0,1),@(0,2));
        LonePairsTotal = 1; LonePairsByAtom = @{ 2 = 1 };
        Angles = @(@{ Target = 180.0; Kind = [MoleculeGeometry+AngleKind]::Straight })
    },
    @{
        Id = 'CH2O'; Atoms = @('C','O','H','H'); Bonds = @(@(0,1),@(0,2),@(0,3));
        LonePairsTotal = 2; LonePairsByAtom = @{ 1 = 2 };
        Angles = @(
            @{ Target = 121.9; Kind = [MoleculeGeometry+AngleKind]::Arc },
            @{ Target = 116.0; Kind = [MoleculeGeometry+AngleKind]::Arc }
        )
    },
    @{
        Id = 'C2H4'; Atoms = @('C','C','H','H','H','H'); Bonds = @(@(0,1),@(0,2),@(0,3),@(1,4),@(1,5));
        LonePairsTotal = 0; LonePairsByAtom = @{};
        Angles = @(
            @{ Target = 121.2; Kind = [MoleculeGeometry+AngleKind]::Arc },
            @{ Target = 117.6; Kind = [MoleculeGeometry+AngleKind]::Arc }
        )
    },
    @{
        Id = 'C2H2'; Atoms = @('C','C','H','H'); Bonds = @(@(0,1),@(0,2),@(1,3));
        LonePairsTotal = 0; LonePairsByAtom = @{};
        Angles = @(@{ Target = 180.0; Kind = [MoleculeGeometry+AngleKind]::Straight })
    }
)

$allIds = [MoleculeGeometry]::AllIds
Assert ($allIds.Length -eq 10) "Expected 10 molecules, got $($allIds.Length)"

foreach ($spec in $expected) {
    $id = $spec.Id
    $m = [MoleculeGeometry]::Get($id)

    # 원자 수·순서·원소
    Assert ($m.Atoms.Length -eq $spec.Atoms.Length) "$id atom count mismatch"
    for ($i = 0; $i -lt $spec.Atoms.Length; $i++) {
        Assert ($m.Atoms[$i].Element -eq $spec.Atoms[$i]) "$id atom[$i] element expected $($spec.Atoms[$i]) got $($m.Atoms[$i].Element)"
    }

    # 기준 원자(0번) 원점 확인
    $originDist = $m.Atoms[0].Position.Length()
    Assert ($originDist -lt 1e-9) "$id atom 0 not at origin ($originDist)"

    # 결합 관계 및 결합 길이 양수
    Assert ($m.Bonds.Length -eq $spec.Bonds.Length) "$id bond count mismatch"
    for ($b = 0; $b -lt $spec.Bonds.Length; $b++) {
        $bond = $m.Bonds[$b]
        Assert ($bond.AtomA -eq $spec.Bonds[$b][0] -and $bond.AtomB -eq $spec.Bonds[$b][1]) "$id bond[$b] endpoints mismatch"
        $len = $m.BondLength($b)
        Assert ($len -gt 0.5 -and $len -lt 2.0) "$id bond[$b] length out of Angstrom range: $len"
    }

    # 비공유 전자쌍 개수(총합·원자별) 및 단위 방향 벡터
    Assert ($m.LonePairs.Length -eq $spec.LonePairsTotal) "$id lone pair count expected $($spec.LonePairsTotal) got $($m.LonePairs.Length)"
    for ($a = 0; $a -lt $m.Atoms.Length; $a++) {
        $expectedLp = if ($spec.LonePairsByAtom.ContainsKey($a)) { $spec.LonePairsByAtom[$a] } else { 0 }
        $actualLp = $m.LonePairCountForAtom($a)
        Assert ($actualLp -eq $expectedLp) "$id atom[$a] lone pairs expected $expectedLp got $actualLp"
    }
    foreach ($lp in $m.LonePairs) {
        $dirLen = $lp.Direction.Length()
        Assert ([Math]::Abs($dirLen - 1.0) -lt 1e-6) "$id lone pair direction not unit length: $dirLen"
    }

    # 표시 각(표값 +-0.5 deg, 직선 180 deg, 이원자 없음)
    Assert ($m.Angles.Length -eq $spec.Angles.Length) "$id display angle count expected $($spec.Angles.Length) got $($m.Angles.Length)"
    for ($k = 0; $k -lt $spec.Angles.Length; $k++) {
        $angDef = $m.Angles[$k]
        $deg = $m.AngleDegrees($k)
        Assert ($angDef.Kind -eq $spec.Angles[$k].Kind) "$id angle[$k] kind mismatch"
        if ($spec.Angles[$k].Kind -eq [MoleculeGeometry+AngleKind]::Straight) {
            Assert ([Math]::Abs($deg - 180.0) -lt 1e-6) "$id straight angle expected 180 got $deg"
        } else {
            $diff = [Math]::Abs($deg - $spec.Angles[$k].Target)
            Assert ($diff -le 0.5) "$id angle[$k] expected $($spec.Angles[$k].Target) +-0.5 got $deg (diff=$diff)"
        }
    }

    # 회전 후 모든 원자 쌍 간 거리 보존
    foreach ($yaw in @(0.0, 37.5, 142.0, -73.25)) {
        $rotated = @()
        for ($i = 0; $i -lt $m.Atoms.Length; $i++) {
            $rotated += [MoleculeGeometry]::Rotate($m.Atoms[$i].Position, $yaw, 18.0)
        }
        for ($i = 0; $i -lt $m.Atoms.Length; $i++) {
            for ($j = $i + 1; $j -lt $m.Atoms.Length; $j++) {
                $dOrig = [MoleculeGeometry+Vec3]::Distance($m.Atoms[$i].Position, $m.Atoms[$j].Position)
                $dRot = [MoleculeGeometry+Vec3]::Distance($rotated[$i], $rotated[$j])
                Assert ([Math]::Abs($dOrig - $dRot) -lt 1e-9) "$id distance not preserved after yaw=$yaw ($dOrig vs $dRot)"
            }
        }
    }

    # 투영·깊이 정렬(먼 것 먼저)·결합 끝 자르기 검증
    $scene = [MoleculeGeometry]::BuildScene($id, $null, 35.0, 18.0, 300.0, 300.0, 110.0, 6.5, 22.0, $true)
    Assert ($scene.Atoms.Length -eq $m.Atoms.Length) "$id projected atom count mismatch"
    for ($i = 1; $i -lt $scene.Items.Length; $i++) {
        Assert ($scene.Items[$i - 1].Depth -ge $scene.Items[$i].Depth) "$id depth sort failed at item $i ($($scene.Items[$i - 1].Depth) < $($scene.Items[$i].Depth))"
    }

    # 결합 중점 깊이 및 양 끝 원자 반지름 자르기 검증 (기본 시야에서 기대 결합 수·모든 인덱스 존재 단언)
    $bondItems = @($scene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Bond })
    Assert ($bondItems.Length -eq $m.Bonds.Length) "$id expected $($m.Bonds.Length) visible bonds in default view, got $($bondItems.Length)"
    $seenBonds = @{}
    foreach ($bi in $bondItems) {
        Assert (-not $seenBonds.ContainsKey($bi.BondIndex)) "$id duplicate bond index $($bi.BondIndex)"
        $seenBonds[$bi.BondIndex] = $true

        $pa = $scene.Atoms[$bi.AtomA]
        $pb = $scene.Atoms[$bi.AtomB]
        $expectedDepth = ($pa.Depth + $pb.Depth) * 0.5
        Assert ([Math]::Abs($bi.Depth - $expectedDepth) -lt 1e-9) "$id bond midpoint depth mismatch"

        $centerDist = [Math]::Sqrt([Math]::Pow($pb.X - $pa.X, 2) + [Math]::Pow($pb.Y - $pa.Y, 2))
        $segLen = [Math]::Sqrt([Math]::Pow($bi.EndX - $bi.StartX, 2) + [Math]::Pow($bi.EndY - $bi.StartY, 2))
        $expectedClip = $centerDist - $pa.RadiusPx - $pb.RadiusPx
        Assert ([Math]::Abs($segLen - $expectedClip) -lt 1e-6) "$id bond clipped length mismatch ($segLen vs $expectedClip)"
        Assert ([Math]::Abs($bi.ClippedLength - $expectedClip) -lt 1e-6) "$id ClippedLength property mismatch"

        $startDist = [Math]::Sqrt([Math]::Pow($bi.StartX - $pa.X, 2) + [Math]::Pow($bi.StartY - $pa.Y, 2))
        $endDist = [Math]::Sqrt([Math]::Pow($bi.EndX - $pb.X, 2) + [Math]::Pow($bi.EndY - $pb.Y, 2))
        Assert ([Math]::Abs($startDist - $pa.RadiusPx) -lt 1e-6) "$id bond start trim != radiusA"
        Assert ([Math]::Abs($endDist - $pb.RadiusPx) -lt 1e-6) "$id bond end trim != radiusB"
    }
    for ($b = 0; $b -lt $m.Bonds.Length; $b++) {
        Assert ($seenBonds.ContainsKey($b)) "$id missing bond index $b in default view"
    }

    # 전자쌍 항목 포함/제외 검증
    $lpItems = @($scene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::LonePair })
    Assert ($lpItems.Length -eq $spec.LonePairsTotal) "$id scene lone pair item count mismatch"
    $sceneNoLp = [MoleculeGeometry]::BuildScene($id, $null, 35.0, 18.0, 300.0, 300.0, 110.0, 6.5, 22.0, $false)
    $lpItemsOff = @($sceneNoLp.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::LonePair })
    Assert ($lpItemsOff.Length -eq 0) "$id lone pairs should be excluded when includeLonePairs=false"
}

# 2. 이원자(N2, O2) 표시 각 없음 명시 확인
Assert ([MoleculeGeometry]::Get('N2').Angles.Length -eq 0) 'N2 must have no angle'
Assert ([MoleculeGeometry]::Get('O2').Angles.Length -eq 0) 'O2 must have no angle'

# 3. 원근 효과: 가까운 원자가 먼 원자보다 크고 밝아야 함
$ch4 = [MoleculeGeometry]::BuildScene('CH4', $null, 25.0, 18.0, 300.0, 300.0, 110.0, 6.5, 22.0, $false)
$sortedAtoms = @($ch4.Atoms | Sort-Object Depth)
$nearest = $sortedAtoms[0]
$farthest = $sortedAtoms[$sortedAtoms.Length - 1]
Assert ($nearest.Depth -lt $farthest.Depth) 'CH4 atoms should span depth'
Assert ($nearest.PerspectiveScale -gt $farthest.PerspectiveScale) 'Nearer atom must have larger perspective scale'
Assert ($nearest.Brightness -gt $farthest.Brightness) 'Nearer atom must be brighter'

# 4. 원자가 완전히 겹치면(반지름이 매우 클 때) 뒤집힌 음수 길이 결합이 나오지 않아야 함
$overlapScene = [MoleculeGeometry]::BuildScene('N2', $null, 0.0, 0.0, 300.0, 300.0, 20.0, 6.5, 40.0, $false)
$overlapBonds = @($overlapScene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Bond })
Assert ($overlapBonds.Length -eq 0) 'Overlapping atoms must suppress inverted bond segment'

# 4-1. 일부 겹침 각도 케이스: C2H4를 C=C 축 정면(yaw=90, tilt=0)에서 보면 C0-C1(인덱스 0)만 겹쳐 생략되고 C-H 4개(인덱스 1..4)는 유지
$partialScene = [MoleculeGeometry]::BuildScene('C2H4', $null, 90.0, 0.0, 300.0, 300.0, 110.0, 6.5, 22.0, $false)
$partialBonds = @($partialScene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Bond })
Assert ($partialBonds.Length -eq 4) "C2H4 head-on should keep 4 C-H bonds and drop C=C, got $($partialBonds.Length)"
$partialIndices = @($partialBonds | ForEach-Object { $_.BondIndex } | Sort-Object)
Assert (($partialIndices -join ',') -eq '1,2,3,4') "C2H4 head-on bond indices expected 1,2,3,4 got $($partialIndices -join ',')"

# 5. near plane 뒤 원자 제외 및 결합 클리핑 경계 테스트
$ptBehind = New-Object MoleculeGeometry+ProjectedPoint
$okBehind = [MoleculeGeometry]::TryProjectPoint((New-Object MoleculeGeometry+Vec3 0.0, 0.0, 1.0), 300.0, 300.0, 100.0, 1.2, [ref]$ptBehind)
Assert ((-not $okBehind) -and (-not $ptBehind.Visible)) 'Point behind near plane (depth 0.20 < 0.25) must be culled'
$ptFront = New-Object MoleculeGeometry+ProjectedPoint
$okFront = [MoleculeGeometry]::TryProjectPoint((New-Object MoleculeGeometry+Vec3 0.0, 0.0, 0.95), 300.0, 300.0, 100.0, 1.2, [ref]$ptFront)
Assert ($okFront -and $ptFront.Visible -and [Math]::Abs($ptFront.Depth - 0.25) -lt 1e-9) 'Point on near plane (depth 0.25) must project'

# N2를 yaw=70, tilt=0, cameraDistance=0.70으로 두면 N0(z=+0.516 -> depth=0.184 < 0.25)는 near plane 뒤, N1(z=-0.516 -> depth=1.216)은 앞
$clipScene = [MoleculeGeometry]::BuildScene('N2', $null, 70.0, 0.0, 300.0, 300.0, 110.0, 0.70, 12.0, $true)
Assert ((-not $clipScene.Atoms[0].Visible) -and $clipScene.Atoms[1].Visible) 'N2 near-plane split visibility mismatch'
$clipAtomItems = @($clipScene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Atom })
Assert ($clipAtomItems.Length -eq 1 -and $clipAtomItems[0].AtomIndex -eq 1) 'Only front atom should be emitted'
$clipBondItems = @($clipScene.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Bond })
Assert ($clipBondItems.Length -eq 1 -and $clipBondItems[0].ClippedLength -gt 0.0) 'Bond crossing near plane must be clipped and visible'
$expectedClipDepth = ($clipScene.Atoms[1].Depth + [MoleculeGeometry]::MinCameraDepth) * 0.5
Assert ([Math]::Abs($clipBondItems[0].Depth - $expectedClipDepth) -lt 1e-6) 'Clipped bond depth must average front atom and near plane'

# 전체 분자(원자·결합·전자쌍 모두 XY 평면 z=0)가 near plane 뒤(cameraDistance=0.15 -> depth=0.15 < 0.25)이면 모든 항목 제외
$allBehind = [MoleculeGeometry]::BuildScene('CH2O', $null, 0.0, 0.0, 300.0, 300.0, 110.0, 0.15, 20.0, $true)
Assert ($allBehind.Items.Length -eq 0) 'All items behind near plane must be excluded'
Assert ((-not $allBehind.Angles[0].Visible) -and (-not $allBehind.Angles[1].Visible)) 'Angles behind near plane must be marked invisible'

# 6. 재사용 버퍼(SceneBuffer) 캐시 히트 및 정지 썸네일 재계산 생략 검증
$buf = [MoleculeGeometry]::CreateBuffer('H2O')
$atomsRef = $buf.Atoms
$rebuilt1 = [MoleculeGeometry]::BuildSceneInto($buf, $null, 30.0, 18.0, 200.0, 200.0, 90.0, 6.5, 18.0, $true)
$rebuilt2 = [MoleculeGeometry]::BuildSceneInto($buf, $null, 30.0, 18.0, 200.0, 200.0, 90.0, 6.5, 18.0, $true)
$rebuilt3 = [MoleculeGeometry]::BuildSceneInto($buf, $null, 45.0, 18.0, 200.0, 200.0, 90.0, 6.5, 18.0, $true)
Assert ($rebuilt1 -and (-not $rebuilt2) -and $rebuilt3 -and $buf.RebuildCount -eq 2) "SceneBuffer cache behavior mismatch ($rebuilt1,$rebuilt2,$rebuilt3,$($buf.RebuildCount))"
Assert ([Object]::ReferenceEquals($atomsRef, $buf.Atoms)) 'SceneBuffer must reuse Atoms array in place'

# 7. 비공유 전자쌍 방향과 결합 방향 사이 각도 검증 (7개 전자쌍 보유 분자)
function VecAngleDeg($v1, $v2) {
    $dot = [MoleculeGeometry+Vec3]::Dot($v1.Normalized(), $v2.Normalized())
    if ($dot -gt 1.0) { $dot = 1.0 }
    if ($dot -lt -1.0) { $dot = -1.0 }
    return [Math]::Acos($dot) * (180.0 / [Math]::PI)
}

# NH3: 전자쌍(0)이 세 N-H 결합 모두와 동일한 각(약 112.15 deg, NIST aXNH)
$nh3 = [MoleculeGeometry]::Get('NH3')
$nh3Lp = $nh3.LonePairs[0].Direction
$nh3Angles = @()
for ($h = 1; $h -le 3; $h++) { $nh3Angles += VecAngleDeg $nh3Lp ($nh3.Atoms[$h].Position - $nh3.Atoms[0].Position) }
Assert ([Math]::Abs($nh3Angles[0] - $nh3Angles[1]) -lt 1e-6 -and [Math]::Abs($nh3Angles[1] - $nh3Angles[2]) -lt 1e-6) 'NH3 lone pair must make equal angle with all 3 N-H bonds'
Assert ([Math]::Abs($nh3Angles[0] - 112.15) -lt 0.1) "NH3 LP-NH angle expected ~112.15 got $($nh3Angles[0])"

# H2O: 두 전자쌍 사이 약 109.5 deg, 각 전자쌍이 두 O-H 결합과 대칭 등각(약 110.7 deg)
$h2o = [MoleculeGeometry]::Get('H2O')
$h2oLpAngle = VecAngleDeg $h2o.LonePairs[0].Direction $h2o.LonePairs[1].Direction
Assert ([Math]::Abs($h2oLpAngle - 109.47) -lt 0.1) "H2O LP-LP angle expected ~109.47 got $h2oLpAngle"
$h2oB1 = $h2o.Atoms[1].Position - $h2o.Atoms[0].Position
$h2oB2 = $h2o.Atoms[2].Position - $h2o.Atoms[0].Position
foreach ($lp in $h2o.LonePairs) {
    $a1 = VecAngleDeg $lp.Direction $h2oB1
    $a2 = VecAngleDeg $lp.Direction $h2oB2
    Assert ([Math]::Abs($a1 - $a2) -lt 1e-6 -and $a1 -gt 105.0 -and $a1 -lt 115.0) "H2O LP-OH symmetry failed ($a1, $a2)"
}

# CO2: 각 O의 전자쌍 2개가 C=O 축과 120 deg, 형제 전자쌍끼리 120 deg
$co2 = [MoleculeGeometry]::Get('CO2')
foreach ($lp in $co2.LonePairs) {
    $bondVec = $co2.Atoms[0].Position - $co2.Atoms[$lp.AtomIndex].Position
    $ang = VecAngleDeg $lp.Direction $bondVec
    Assert ([Math]::Abs($ang - 120.0) -lt 0.1) "CO2 O lone pair angle to C=O expected 120 got $ang"
}
Assert ([Math]::Abs((VecAngleDeg $co2.LonePairs[0].Direction $co2.LonePairs[1].Direction) - 120.0) -lt 0.1) 'CO2 O1 LP-LP expected 120'
Assert ([Math]::Abs((VecAngleDeg $co2.LonePairs[2].Direction $co2.LonePairs[3].Direction) - 120.0) -lt 0.1) 'CO2 O2 LP-LP expected 120'

# N2: 각 N의 전자쌍이 N-N 결합 반대편 180 deg
$n2 = [MoleculeGeometry]::Get('N2')
Assert ([Math]::Abs((VecAngleDeg $n2.LonePairs[0].Direction ($n2.Atoms[1].Position - $n2.Atoms[0].Position)) - 180.0) -lt 0.1) 'N2 N0 LP expected 180'
Assert ([Math]::Abs((VecAngleDeg $n2.LonePairs[1].Direction ($n2.Atoms[0].Position - $n2.Atoms[1].Position)) - 180.0) -lt 0.1) 'N2 N1 LP expected 180'

# O2: 각 O의 전자쌍 2개가 O=O 축과 120 deg
$o2 = [MoleculeGeometry]::Get('O2')
foreach ($lp in $o2.LonePairs) {
    $otherIdx = if ($lp.AtomIndex -eq 0) { 1 } else { 0 }
    $ang = VecAngleDeg $lp.Direction ($o2.Atoms[$otherIdx].Position - $o2.Atoms[$lp.AtomIndex].Position)
    Assert ([Math]::Abs($ang - 120.0) -lt 0.1) "O2 LP angle expected 120 got $ang"
}

# HCN: N의 전자쌍이 C#N 반대편 180 deg
$hcn = [MoleculeGeometry]::Get('HCN')
$hcnAng = VecAngleDeg $hcn.LonePairs[0].Direction ($hcn.Atoms[0].Position - $hcn.Atoms[2].Position)
Assert ([Math]::Abs($hcnAng - 180.0) -lt 0.1) "HCN N LP expected 180 got $hcnAng"

# CH2O: O의 전자쌍 2개가 C=O 축과 120 deg, 서로 120 deg
$ch2o = [MoleculeGeometry]::Get('CH2O')
foreach ($lp in $ch2o.LonePairs) {
    $ang = VecAngleDeg $lp.Direction ($ch2o.Atoms[0].Position - $ch2o.Atoms[1].Position)
    Assert ([Math]::Abs($ang - 120.0) -lt 0.1) "CH2O O LP expected 120 got $ang"
}
Assert ([Math]::Abs((VecAngleDeg $ch2o.LonePairs[0].Direction $ch2o.LonePairs[1].Direction) - 120.0) -lt 0.1) 'CH2O O LP-LP expected 120'

# 8. 결합 차수 입력 반영 확인
$customOrders = [int[]]@(3, 2)
$customH2o = [MoleculeGeometry]::BuildScene('H2O', $customOrders, 0.0, 0.0, 300.0, 300.0, 120.0, 6.5, 20.0, $false)
$customBonds = @($customH2o.Items | Where-Object { $_.Kind -eq [MoleculeGeometry+DrawItemKind]::Bond } | Sort-Object BondIndex)
Assert ($customBonds.Length -eq 2 -and $customBonds[0].Order -eq 3 -and $customBonds[1].Order -eq 2) 'Custom bond orders must propagate'

# 9. 미등록 분자 ID 예외 및 TryGet false
$dummy = $null
Assert (-not [MoleculeGeometry]::TryGet('UNKNOWN', [ref]$dummy)) 'TryGet should return false for unknown id'
$threw = $false
try { [void][MoleculeGeometry]::Get('UNKNOWN') } catch { $threw = $true }
Assert $threw 'Get should throw for unknown id'

# 10. BondPuzzle.cs가 같은 저장소에 존재하면 10개 분자 규약(원소·기준 원자·결합 인덱스·정답 차수)을 직접 교차 비교
$bondPuzzlePath = Join-Path $repo 'Assets/Scripts/Chemistry/BondPuzzle.cs'
if (Test-Path $bondPuzzlePath) {
    Add-Type -Path $bondPuzzlePath
    Assert ([BondPuzzle]::Molecules.Length -eq [MoleculeGeometry]::AllIds.Length) 'BondPuzzle vs MoleculeGeometry count mismatch'
    foreach ($bm in [BondPuzzle]::Molecules) {
        $gm = [MoleculeGeometry]::Get($bm.Id)
        Assert ($gm.Atoms.Length -eq $bm.Atoms.Length) "Cross-check $($bm.Id) atom count mismatch"
        for ($i = 0; $i -lt $bm.Atoms.Length; $i++) {
            Assert ($gm.Atoms[$i].Element -eq $bm.Atoms[$i]) "Cross-check $($bm.Id) atom[$i] mismatch"
        }
        Assert ($bm.Center -eq 0 -and $gm.Atoms[$bm.Center].Position.Length() -lt 1e-9) "Cross-check $($bm.Id) center mismatch"
        Assert ($gm.Bonds.Length -eq $bm.Answers.Length) "Cross-check $($bm.Id) bond count mismatch"
        for ($b = 0; $b -lt $bm.Answers.Length; $b++) {
            $gb = $gm.Bonds[$b]
            Assert ($gb.AtomA -eq $bm.Edges[$b,0] -and $gb.AtomB -eq $bm.Edges[$b,1]) "Cross-check $($bm.Id) bond[$b] edge mismatch"
            Assert ($gb.DefaultOrder -eq $bm.Answers[$b]) "Cross-check $($bm.Id) bond[$b] default order mismatch"
        }
    }
    Write-Output 'MoleculeGeometry tests passed (10 molecules + BondPuzzle cross-check verified)'
} else {
    Write-Output 'MoleculeGeometry tests passed (10 molecules verified; BondPuzzle.cs absent in worktree, cross-check skipped)'
}
