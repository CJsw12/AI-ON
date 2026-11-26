$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$paths = @('BondPuzzle.cs','BondTexts.cs') | ForEach-Object { Join-Path $repo "Assets/Scripts/Chemistry/$_" }
Add-Type -Path $paths
function Assert($condition, $message) { if (-not $condition) { throw $message } }
Assert ([BondPuzzle]::Molecules.Length -eq 10) 'molecule count'
$expected = @(
 @{ id='H2O'; a='OHH'; bonds='11'; pairs='200'; regions=4; shape='Bent'; pol='Polar' },
 @{ id='NH3'; a='NHHH'; bonds='111'; pairs='1000'; regions=4; shape='TrigonalPyramidal'; pol='Polar' },
 @{ id='CH4'; a='CHHHH'; bonds='1111'; pairs='00000'; regions=4; shape='Tetrahedral'; pol='Nonpolar' },
 @{ id='CO2'; a='COO'; bonds='22'; pairs='022'; regions=2; shape='Linear'; pol='Nonpolar' },
 @{ id='N2'; a='NN'; bonds='3'; pairs='11'; regions=2; shape='Linear'; pol='Nonpolar' },
 @{ id='O2'; a='OO'; bonds='2'; pairs='22'; regions=3; shape='Linear'; pol='Nonpolar' },
 @{ id='HCN'; a='CHN'; bonds='13'; pairs='001'; regions=2; shape='Linear'; pol='Polar' },
 @{ id='CH2O'; a='COHH'; bonds='211'; pairs='0200'; regions=3; shape='TrigonalPlanar'; pol='Polar' },
 @{ id='C2H4'; a='CCHHHH'; bonds='21111'; pairs='000000'; regions=3; shape='TrigonalPlanar'; pol='Nonpolar' },
 @{ id='C2H2'; a='CCHH'; bonds='311'; pairs='0000'; regions=2; shape='Linear'; pol='Nonpolar' }
)
for($midx=0; $midx -lt 10; $midx++) {
 $m=[BondPuzzle]::Molecules[$midx]; $e=$expected[$midx]
 Assert ($m.Id -eq $e.id -and ($m.Atoms -join '') -eq $e.a -and ($m.Answers -join '') -eq $e.bonds) "table $($m.Id)"
 Assert ($m.Shape.ToString() -eq $e.shape -and $m.Polarity.ToString() -eq $e.pol) "shape/polarity $($m.Id)"
 $solutions=0; $limit=[int][Math]::Pow(4,$m.Answers.Length)
 for($number=0; $number -lt $limit; $number++) {
  $n=$number; $orders=New-Object int[] $m.Answers.Length
  for($i=0; $i -lt $orders.Length; $i++) { $orders[$i]=$n % 4; $n=[Math]::Floor($n/4) }
  if([BondPuzzle]::Complete($m,$orders)) { $solutions++; Assert (($orders -join '') -eq $e.bonds) "solution $($m.Id)" }
 }
 Assert ($solutions -eq 1) "unique $($m.Id)"
 for($i=0; $i -lt $m.Atoms.Length; $i++) { Assert ([BondPuzzle]::LonePairs($m,$m.Answers,$i) -eq [int]::Parse($e.pairs.Substring($i,1))) "pairs $($m.Id) $i" }
 Assert ([BondPuzzle]::ElectronRegions($m,$m.Answers) -eq $e.regions) "regions $($m.Id)"
 $empty=New-Object int[] $m.Answers.Length
 Assert ($null -eq [BondPuzzle]::LonePairs($m,$empty,0)) "incomplete $($m.Id)"
 $over=[int[]]$m.Answers.Clone(); $over[0]=3; if($over[0] -eq $m.Answers[0]) { $over[0]=0 }
 Assert ($null -eq [BondPuzzle]::LonePairs($m,$over,0)) "invalid $($m.Id)"
 foreach($p in 0,1) { foreach($stage in [BondStage]::Shape,[BondStage]::Polarity) { if($m.Diatomic -and $stage -eq [BondStage]::Shape) { continue }; Assert (-not [string]::IsNullOrWhiteSpace([BondTexts]::Fact($p,$m,$stage))) 'fact' }; foreach($status in 0,1,2) { $a=New-Object int[] $m.Answers.Length; if($status -eq 1) { $a=[int[]]$m.Answers.Clone() }; if($status -eq 2) { for($i=0;$i -lt $a.Length;$i++) { $a[$i]=3 } }; $hint=[BondTexts]::Hint($p,$m,$a,0); if([BondPuzzle]::Status($m,$a,0) -eq [AtomStatus]::Exact) { Assert ($hint -eq '') 'exact hint empty' } else { Assert (-not [string]::IsNullOrWhiteSpace($hint)) 'wrong atom hint' } } }
 Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Bond,$m.Answers).Length -eq 0) 'correct bond confusion'
 foreach($shape in [Enum]::GetValues([BondShape])) { $c=[BondPuzzle]::PossibleConfusions($m,[BondStage]::Shape,$shape); Assert ($c.Length -le 2) 'shape confusion limit'; if($shape -eq $m.Shape) { Assert ($c.Length -eq 0) 'correct shape confusion' } }
 foreach($pol in [Enum]::GetValues([BondPolarity])) { $c=[BondPuzzle]::PossibleConfusions($m,[BondStage]::Polarity,$pol); Assert ($c.Length -le 2) 'polarity confusion limit'; if($pol -eq $m.Polarity) { Assert ($c.Length -eq 0) 'correct polarity confusion' } }
}
foreach($p in 0,1) { Assert ([BondTexts]::Replies[$p].Length -eq 3) 'replies count'; foreach($reply in [BondTexts]::Replies[$p]) { Assert (-not [string]::IsNullOrWhiteSpace($reply)) 'reply'; }; Assert ([BondTexts]::Introduction($p).Length -gt 0 -and [BondTexts]::OxygenLimitation($p).Length -gt 0) 'text' }
Assert ([BondPuzzle]::Cycle(3,$false) -eq 0 -and [BondPuzzle]::Cycle(0,$true) -eq 3) 'cycle'
foreach($max in 13,14,15) { for($score=0;$score -le $max;$score++) { $d=if($score*100 -ge $max*85){2}elseif($score*100 -ge $max*60){1}else{0}; Assert ([BondPuzzle]::DeltaFor($score,$max) -eq $d) "delta $score/$max" } }
$p0=[BondTexts]::CommentPhrases(0); $p1=[BondTexts]::CommentPhrases(1)
$t0=[BondTexts]::CommentTags(0); $t1=[BondTexts]::CommentTags(1)
Assert ($p0.Length -ge 20 -and $p1.Length -ge 20) 'at least 20 phrases per professor'
Assert ($t0.Length -eq $p0.Length -and $t1.Length -eq $p1.Length) 'tags match phrases count'
$forbiddenWords=@('직선','굽은','삼각','사면체','평면','극성','쌍극','결합','전자','영역','각도','°','옥텟','원자','분자','공유','대칭','구조','모양','홀전자','상자성','차수','물','암모니아','메테인','이산화 탄소','질소','산소','사이안화','폼알데하이드','에텐','에타인','수소','탄소','정답','오답','맞았','맞아요','맞습니다','틀렸','틀려','아니라','이유','단일','이중','삼중','다중','고립','반발','상쇄','전하','음성도','이온','입체','꺾','구부','뿔','피라미드','일렬','치우','밀어','에틸렌','아세틸렌','포름알데히드','메탄','하나','둘','셋','넷','백팔십','사잇각','내각','도예요','도야','π','σ','선 세','선 두','녹','산성','염기')
foreach($profIdx in 0,1) {
 $plist=[BondTexts]::CommentPhrases($profIdx); $tlist=[BondTexts]::CommentTags($profIdx)
 Assert (@($plist | Select-Object -Unique).Length -eq $plist.Length) "unique phrases prof $profIdx"
 for($k=0;$k -lt $plist.Length;$k++) {
  $ph=$plist[$k]
  Assert (-not [string]::IsNullOrWhiteSpace($ph) -and $ph.Length -le 40) "phrase length prof $profIdx #$k ($($ph.Length))"
  Assert (-not [string]::IsNullOrWhiteSpace($tlist[$k])) "tag empty prof $profIdx #$k"
  foreach($bw in $forbiddenWords) { Assert ($ph.IndexOf($bw,[StringComparison]::OrdinalIgnoreCase) -lt 0) "phrase prof $profIdx #$k contains forbidden '$bw': $ph" }
  foreach($ch in $ph.ToCharArray()) { Assert (-not ([Char]::IsNumber($ch) -or ($ch -ge 'A' -and $ch -le 'Z') -or ($ch -ge 'a' -and $ch -le 'z') -or ($ch -ge [char]0x00c0 -and $ch -le [char]0x024f -and [Char]::IsLetter($ch)) -or ($ch -ge [char]0xff21 -and $ch -le [char]0xff3a) -or ($ch -ge [char]0xff41 -and $ch -le [char]0xff5a))) "phrase prof $profIdx #$k has digit/latin: $ph" }
  Assert ([BondPuzzle]::AcceptComment($ph)) "single phrase accepted prof $profIdx #$k"
 }
}
# ComposeComment 파싱 검증 (ASCII 1-based 숫자 최대 2개, 전각 숫자는 무시)
Assert ([BondTexts]::ComposeComment(0,'3, 12') -eq ($p0[2]+' '+$p0[11])) 'compose two normal'
Assert ([BondTexts]::ComposeComment(0,'번호: 7') -eq $p0[6]) 'compose one labeled'
Assert ([BondTexts]::ComposeComment(1,'7번과 3번') -eq ($p1[6]+' '+$p1[2])) 'compose two reversed korean'
Assert ([BondTexts]::ComposeComment(0,'0, 99, 5') -eq $p0[4]) 'compose ignore out of range'
Assert ($null -eq [BondTexts]::ComposeComment(0,'0, 99')) 'compose all out of range null'
Assert ([BondTexts]::ComposeComment(0,'4, 4, 9') -eq ($p0[3]+' '+$p0[8])) 'compose deduplicate keep second'
Assert ([BondTexts]::ComposeComment(0,'4, 4') -eq $p0[3]) 'compose duplicate single'
Assert ([BondTexts]::ComposeComment(1,'2, 5, 8, 11') -eq ($p1[1]+' '+$p1[4])) 'compose max two'
Assert ($null -eq [BondTexts]::ComposeComment(0,'좋아요!')) 'compose no numbers null'
Assert ($null -eq [BondTexts]::ComposeComment(0,'')) 'compose empty null'
Assert ($null -eq [BondTexts]::ComposeComment(0,$null)) 'compose null input'
Assert ($null -eq [BondTexts]::ComposeComment(0,'４, １１')) 'compose ignores fullwidth digits'
Assert ([BondTexts]::ComposeComment(0,'４, 5') -eq $p0[4]) 'compose ignores fullwidth keeps ascii'
Assert ([BondTexts]::ComposeComment(0,'99999999999999, 2') -eq $p0[1]) 'compose overflow safe'
# AcceptComment 멤버십 검증
Assert ([BondPuzzle]::AcceptComment([BondTexts]::ComposeComment(0,'1, 2'))) 'composed accepted'
Assert ([BondPuzzle]::AcceptComment($p0[5]+' '+$p0[0])) 'reversed two phrases accepted'
Assert ([BondPuzzle]::AcceptComment($p1[10]+' '+$p1[3])) 'prof 1 two phrases accepted'
Assert (-not [BondPuzzle]::AcceptComment($p0[0]+' '+$p0[0])) 'duplicate phrase rejected'
Assert (-not [BondPuzzle]::AcceptComment($p0[0]+' '+$p0[1]+' '+$p0[2])) 'three phrases rejected'
Assert (-not [BondPuzzle]::AcceptComment(' '+$p0[0])) 'leading space rejected'
Assert (-not [BondPuzzle]::AcceptComment($p0[0]+'  '+$p0[1])) 'double space rejected'
foreach($s in @('', '   ', '물에 녹아요', '이 답은 산성이에요', '좋아요', '좋아요!', '물론 다음엔 더 잘할 수 있어요!', '삼각평면이 아니라', '무극성입니다', 'H2O', ('가'*81))) { Assert (-not [BondPuzzle]::AcceptComment($s)) "reject non-member $s" }
$validComment = $p0[0]
$reasons=@{'H2O'='쌍극자가 상쇄되지 않고 남음'; 'NH3'='쌍극자가 상쇄되지 않고 남음'; 'CH4'='결합 쌍극자가 대칭으로 상쇄'; 'CO2'='결합 쌍극자가 대칭으로 상쇄'; 'N2'='같은 원자끼리 결합'; 'O2'='같은 원자끼리 결합'; 'HCN'='쌍극자가 상쇄되지 않고 남음'; 'CH2O'='쌍극자가 상쇄되지 않고 남음'; 'C2H4'='결합 쌍극자가 대칭으로 상쇄'; 'C2H2'='결합 쌍극자가 대칭으로 상쇄'}
foreach($m in [BondPuzzle]::Molecules) { foreach($p in 0,1) { Assert ([BondTexts]::Fact($p,$m,[BondStage]::Polarity).Contains($reasons[$m.Id])) "polarity reason $($m.Id)" } }
$selected=[BondMolecule[]]@([BondPuzzle]::Molecules[0],[BondPuzzle]::Molecules[4],[BondPuzzle]::Molecules[1],[BondPuzzle]::Molecules[2],[BondPuzzle]::Molecules[3])
$retry=New-Object BondPuzzle -ArgumentList (,$selected); $retry.SubmitBond() | Out-Null; $first=$retry.BeginRequest(); $retry.CycleBond(0,$false); $retry.SubmitBond() | Out-Null; Assert (-not $retry.FinishRequest($first,$validComment)) 'retry invalidates old attempt'
$game=New-Object BondPuzzle -ArgumentList (,$selected)
Assert (-not $game.HintVisible -and $game.MaxScore -eq 14) 'start'
Assert (-not $game.SubmitBond() -and $game.HintVisible) 'hint after first error'
for($i=0;$i -lt $game.Current.Answers.Length;$i++) { for($j=0;$j -lt $game.Current.Answers[$i];$j++) { $game.CycleBond($i,$false) } }
Assert ($game.SubmitBond() -and $game.Score -eq 0 -and $game.Stage -eq [BondStage]::Shape) 'retry no bond score'
Assert ($game.SubmitShape([BondShape]::Bent) -and $game.Score -eq 1) 'shape score'
$game.Frame(1,$null); Assert ($game.Remaining -eq 149 -and $game.Stage -eq [BondStage]::Polarity) '3d timer'
Assert ($game.SubmitPolarity([BondPolarity]::Polar) -and $game.Score -eq 2) 'polarity score'
$game.Advance()
for($j=0;$j -lt 3;$j++) { $game.CycleBond(0,$false) }
Assert ($game.SubmitBond() -and $game.Stage -eq [BondStage]::Polarity -and $game.Score -eq 3) 'diatomic skip'
$token=$game.BeginRequest(); Assert ($null -ne $token -and $null -eq $game.BeginRequest()) 'one request'
$game.Frame(14,$null); Assert ($game.FinishRequest($token,$validComment)) '14 seconds accepted'
$token=$game.BeginRequest(); $game.Frame(16,$null); Assert (-not $game.FinishRequest($token,$validComment)) '16 seconds rejected'
$token=$game.BeginRequest(); $game.SubmitPolarity([BondPolarity]::Nonpolar) | Out-Null; Assert ($null -eq $game.BeginRequest()) 'inflight after transition'; Assert (-not $game.FinishRequest($token,$validComment)) 'old stage'; Assert ($null -eq $game.BeginRequest() -eq $false) 'new request after completion'
$game2=New-Object BondPuzzle -ArgumentList (,$selected); for($i=0;$i -lt 10;$i++) { $t=$game2.BeginRequest(); Assert ($null -ne $t) 'request quota'; Assert ($game2.FinishRequest($t,$validComment)) 'request complete' }; Assert ($null -eq $game2.BeginRequest()) 'quota 10'
$game3=New-Object BondPuzzle -ArgumentList (,$selected); $game3.CycleBond(0,$false); $game3.CycleBond(1,$false); $game3.Frame(150, [Action]{ $game3.SubmitBond() | Out-Null }); Assert ($game3.Score -eq 1 -and $game3.Stage -eq [BondStage]::Finished -and $game3.Remaining -eq 0) 'same frame submission'
$game4=New-Object BondPuzzle -ArgumentList (,$selected); $t=$game4.BeginRequest(); $game4.End(); Assert (-not $game4.FinishRequest($t,$validComment)) 'end invalidates token'
$game5=New-Object BondPuzzle -ArgumentList (,$selected); $foreign=$game5.BeginRequest(); $other=New-Object BondPuzzle -ArgumentList (,$selected); $own=$other.BeginRequest(); Assert (-not $other.FinishRequest($foreign,$validComment)) 'round token invalid'; Assert ($other.FinishRequest($own,$validComment)) 'foreign token cannot consume request'
$random1=New-Object BondPuzzle -ArgumentList 27; $random2=New-Object BondPuzzle -ArgumentList 27; Assert ($random1.Current.Id -eq $random2.Current.Id) 'seed'
Assert ([BondPuzzle]::PossibleConfusions([BondPuzzle]::Molecules[1],[BondStage]::Shape,[BondShape]::TrigonalPlanar).Length -eq 1) 'ammonia planar confusion'
Assert ([BondPuzzle]::PossibleConfusions([BondPuzzle]::Molecules[0],[BondStage]::Shape,[BondShape]::Linear).Length -eq 1) 'water linear confusion'
Assert ([BondPuzzle]::PossibleConfusions([BondPuzzle]::Molecules[1],[BondStage]::Shape,[BondShape]::Tetrahedral).Length -eq 1) 'ammonia tetrahedral confusion'
Assert ([BondPuzzle]::PossibleConfusions([BondPuzzle]::Molecules[8],[BondStage]::Shape,[BondShape]::Tetrahedral).Length -eq 1) 'ethene confusion'
foreach($i in 0,1,3) { $m=[BondPuzzle]::Molecules[$i]; $wrong=if($m.Polarity -eq [BondPolarity]::Polar){[BondPolarity]::Nonpolar}else{[BondPolarity]::Polar}; Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Polarity,$wrong).Length -eq 1) 'polarity confusion table' }
foreach($m in [BondPuzzle]::Molecules) { $wrong=if($m.Polarity -eq [BondPolarity]::Polar){[BondPolarity]::Nonpolar}else{[BondPolarity]::Polar}; Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Polarity,$wrong).Length -ge 1) "polarity confusion $($m.Id)" }
foreach($n in 1..6) { $a=[int[]]@([Math]::Min(3,$n),[Math]::Max(0,$n-3)); $m=if($n -eq 2){[BondPuzzle]::Molecules[7]}else{[BondPuzzle]::Molecules[0]}; if($n -eq 2){$a=[int[]]@(2,0,0)}; $hint=[BondTexts]::Hint(0,$m,$a,0); Assert ($hint.Contains("현재 결합 $n") -and $hint -notmatch '[1-6](이에요|예요|이|가|을|를)') "numeric particle $n" }
foreach($i in 3,4) { $m=[BondPuzzle]::Molecules[$i]; $wrong=New-Object int[] $m.Answers.Length; for($j=0;$j -lt $wrong.Length;$j++) { $wrong[$j]=1 }; Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Bond,$wrong).Length -eq 2) 'multiple bond confusion' }
$thirteen=[BondMolecule[]]@([BondPuzzle]::Molecules[0],[BondPuzzle]::Molecules[4],[BondPuzzle]::Molecules[5],[BondPuzzle]::Molecules[2],[BondPuzzle]::Molecules[3]); $fifteen=[BondMolecule[]]@([BondPuzzle]::Molecules[0],[BondPuzzle]::Molecules[1],[BondPuzzle]::Molecules[2],[BondPuzzle]::Molecules[3],[BondPuzzle]::Molecules[6])
Assert ((New-Object BondPuzzle -ArgumentList (,$thirteen)).MaxScore -eq 13 -and (New-Object BondPuzzle -ArgumentList (,$fifteen)).MaxScore -eq 15) 'variable maximum'
$f=New-Object BondPuzzle -ArgumentList (,$selected); $f.CycleBond(0,$false); $f.CycleBond(1,$false); $f.SubmitBond() | Out-Null; $f.SubmitShape([BondShape]::Linear) | Out-Null; $st=$f.BeginRequest([BondStage]::Shape); Assert ($st.Stage -eq [BondStage]::Shape -and $f.FinishRequest($st,$validComment)) 'shape explanation valid in polarity'
$st=$f.BeginRequest([BondStage]::Shape); $f.SubmitPolarity([BondPolarity]::Nonpolar) | Out-Null; Assert (-not $f.FinishRequest($st,$validComment)) 'shape explanation invalid after polarity'; Assert ($f.Stage -eq [BondStage]::PolarityReview -and $f.Index -eq 0) 'polarity review remains current'; $before=$f.Remaining; $f.Frame(1,$null); Assert ($f.Remaining -eq $before-1) 'review timer continues'; $pt=$f.BeginRequest(); Assert ($pt.Stage -eq [BondStage]::Polarity -and $f.FinishRequest($pt,$validComment)) 'polarity review token'; $f.Advance(); Assert ($f.Index -eq 1 -and $f.Stage -eq [BondStage]::Bond) 'advance'
$last=New-Object BondPuzzle -ArgumentList (,$selected); for($n=0;$n -lt 5;$n++) { for($i=0;$i -lt $last.Current.Answers.Length;$i++) { for($j=0;$j -lt $last.Current.Answers[$i];$j++) { $last.CycleBond($i,$false) } }; $last.SubmitBond() | Out-Null; if($last.Stage -eq [BondStage]::Shape) { $last.SubmitShape($last.Current.Shape) | Out-Null }; $last.SubmitPolarity([BondPolarity]::Polar) | Out-Null; if($n -lt 4) { $last.Advance() } }; Assert ($last.Stage -eq [BondStage]::PolarityReview -and $last.Index -eq 4) 'last wrong review'; $lt=$last.BeginRequest(); Assert ($null -ne $lt -and $lt.Molecule -eq 4 -and $lt.Stage -eq [BondStage]::Polarity) 'last polarity request'; $last.Advance(); Assert ($last.Stage -eq [BondStage]::Finished -and -not $last.FinishRequest($lt,$validComment)) 'last advance invalidates'
$cancel=New-Object BondPuzzle -ArgumentList (,$selected); $ct=$cancel.BeginRequest(); $cancel.CancelRequest($ct); Assert (-not $cancel.FinishRequest($ct,$validComment)) 'cancelled'; $ct=$cancel.BeginRequest(); Assert ($null -ne $ct) 'cancel frees slot'; $cancel.Frame(44.999,$null); Assert ($null -eq $cancel.BeginRequest()) 'stale boundary before'; $cancel.Frame(0.001,$null); $new=$cancel.BeginRequest(); Assert ($null -ne $new) 'stale boundary at 45'; Assert (-not $cancel.FinishRequest($ct,$validComment) -and $cancel.FinishRequest($new,$validComment)) 'stale reply cannot consume new request'
foreach($idx in 3,4,5,6,7,8,9) { $m=[BondPuzzle]::Molecules[$idx]; $wrong=[int[]]$m.Answers.Clone(); for($i=0;$i -lt $wrong.Length;$i++) { if($m.Answers[$i] -ge 2) { $wrong[$i]=1; break } }; Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Bond,$wrong).Length -eq 2) "missing multiple $($m.Id)"; $wrong=[int[]]$m.Answers.Clone(); $wrong[0]=[Math]::Min(3,$wrong[0]+1); if($wrong[0] -eq $m.Answers[0] -and $wrong.Length -gt 1) { $wrong[1]++ }; if(($wrong -join '') -ne ($m.Answers -join '')) { Assert ([BondPuzzle]::PossibleConfusions($m,[BondStage]::Bond,$wrong).Length -eq 1) "excess $($m.Id)" } }
Assert ([BondTexts]::Fact(1,[BondPuzzle]::Molecules[0],[BondStage]::Shape,$false).StartsWith('자, 보세요!')) 'wrong professor tone'; Assert ([BondTexts]::Fact(1,[BondPuzzle]::Molecules[0],[BondStage]::Shape,$true).StartsWith('짜잔!')) 'correct professor tone'
foreach($i in 8,9) { Assert ([BondTexts]::Fact(0,[BondPuzzle]::Molecules[$i],[BondStage]::Shape,$false).Contains('강조된 C 주변')) 'highlighted carbon' }
$threw=$false; try { [BondTexts]::Fact(0,[BondPuzzle]::Molecules[4],[BondStage]::Shape,$false) } catch { $threw=$true }; Assert $threw 'diatomic shape rejected'
$threw=$false; try { [BondTexts]::Introduction(2) } catch { $threw=$true }; Assert $threw 'invalid professor'
$threw=$false; try { [BondPuzzle]::BondSum([BondPuzzle]::Molecules[0],$null,0) } catch { $threw=$true }; Assert $threw 'null bond orders'
$threw=$false; try { [BondPuzzle]::BondSum([BondPuzzle]::Molecules[0],[int[]]@(1),0) } catch { $threw=$true }; Assert $threw 'bad bond order length'
$threw=$false; try { $retry.CycleBond(-1,$false) } catch { $threw=$true }; Assert $threw 'bad edge'
foreach($seed in 0,1,27,100) { $r=New-Object BondPuzzle -ArgumentList $seed; $ids=@(); for($n=0;$n -lt 5;$n++) { $ids+= $r.Current.Id; for($i=0;$i -lt $r.Current.Answers.Length;$i++) { for($j=0;$j -lt $r.Current.Answers[$i];$j++) { $r.CycleBond($i,$false) } }; $r.SubmitBond() | Out-Null; if($r.Stage -eq [BondStage]::Shape) { $r.SubmitShape($r.Current.Shape) | Out-Null }; $r.SubmitPolarity($r.Current.Polarity) | Out-Null; $r.Advance() }; Assert (@($ids | Select-Object -Unique).Length -eq 5) 'seed unique selection' }
$selectionTest=New-Object BondPuzzle -ArgumentList (,$selected); $selectionTest.End(); Assert ($selectionTest.Selection.Length -eq 5 -and (($selectionTest.Selection | ForEach-Object Id) -join ',') -eq (($selected | ForEach-Object Id) -join ',')) 'selection remains ordered after finish'
Write-Output 'BondPuzzle tests passed'
