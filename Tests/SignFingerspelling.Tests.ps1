$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$paths = @('SignSample.cs','SignFeatures.cs','SignVote.cs','SignLabels.cs','KnnClassifier.cs') | ForEach-Object { Join-Path $repo "Assets/Scripts/Sign/$_" }
$dll = Join-Path ([IO.Path]::GetTempPath()) ("sign-tests-" + [guid]::NewGuid().ToString("N") + ".dll")
Add-Type -Path $paths -OutputAssembly $dll
Add-Type -Path $dll
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$cases = Get-Content (Join-Path $PSScriptRoot 'Fixtures/sign-features.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert ($cases.Count -eq 10) 'fixture count'
foreach ($case in $cases) {
    $actual=$null
    Assert ([SignFeatures]::TryCompute([float[]]$case.landmarks,$case.width,$case.height,$case.isLeft,[ref]$actual)) 'fixture compute'
    for ($i=0;$i -lt $actual.Length;$i++) { Assert ([Math]::Abs($actual[$i]-$case.features[$i]) -le 0.00001) "fixture $i" }
}
$base=[float[]]$cases[0].landmarks; $original=$null
[void][SignFeatures]::TryCompute($base,640,480,$false,[ref]$original)
$changed=[float[]]::new(63); $mirror=[float[]]::new(63)
for($i=0;$i -lt 21;$i++) { $changed[3*$i]=[float]($base[3*$i]*0.5+0.1); $changed[3*$i+1]=[float]($base[3*$i+1]*0.5+0.2); $mirror[3*$i]=[float](1-$base[3*$i]); $mirror[3*$i+1]=$base[3*$i+1] }
$scaled=$null; $left=$null
Assert ([SignFeatures]::TryCompute($changed,1280,960,$false,[ref]$scaled)) 'scaled'
Assert ([SignFeatures]::TryCompute($mirror,640,480,$true,[ref]$left)) 'mirror'
for($i=0;$i -lt $original.Length;$i++) { Assert ([Math]::Abs($original[$i]-$scaled[$i]) -lt 0.0001) 'translation scale'; Assert ([Math]::Abs($original[$i]-$left[$i]) -lt 0.0001) 'mirror' }
$aspect=[float[]]$base.Clone(); for($i=0;$i -lt 21;$i++) {$aspect[3*$i]=[float]($base[3*$i]/2)}; $af=$null
Assert ([SignFeatures]::TryCompute($aspect,1280,480,$false,[ref]$af)) 'aspect'; for($i=0;$i -lt $original.Length;$i++) { Assert ([Math]::Abs($original[$i]-$af[$i]) -lt 0.00001) 'aspect invariant' }
$small=[float[]]::new(63); $small[27]=0.01; $sf=$null; Assert (-not [SignFeatures]::TryCompute($small,640,480,$false,[ref]$sf)) 'small palm rejected'; Assert ([SignFeatures]::TryCompute($small,640,480,$false,[ref]$sf,5)) 'custom minimum accepted'
$zero=$null; Assert (-not [SignFeatures]::TryCompute([float[]]::new(63),640,480,$false,[ref]$zero)) 'zero palm'
$s=[SignSample]::new(); $s.Person='p'; $s.Session='s'; $s.Trial='t'; $s.Label='A'; $s.Width=640; $s.Height=480; $s.Handedness='Right'; $s.Landmarks=$base
$parsed=$null; $errorText=$null; $line=$s.ToCsvLine(); Assert ([SignSample]::TryParse($line,[ref]$parsed,[ref]$errorText)) 'CSV parse'; Assert ($parsed.ToCsvLine() -ceq $line) 'CSV roundtrip'; Assert (-not [SignSample]::TryParse('bad',[ref]$parsed,[ref]$errorText)) 'bad CSV'
$v=[SignVote]::new(); Assert (($v.Add(0,0)).Label -eq 0) 'vote'; Assert (($v.Add(50,1)).Label -eq -1) 'tie'; Assert (($v.Add(100,0)).Label -eq 0) 'majority'; Assert (($v.Add(110,-2)).Label -eq -1) 'no hand'; Assert (($v.Add(120,1)).Label -eq 1) 'clear'; Assert (($v.Add(320,0)).Label -eq 0) 'gap'
$v=[SignVote]::new(); $confirmed=0; for($t=0;$t -le 1000;$t+=100) {if(($v.Add($t,0)).Confirmed){$confirmed++}}; Assert ($confirmed -eq 1) 'confirmation once'
$v=[SignVote]::new(); [void]$v.Add(0,0); $progress=$v.Add(100,0); Assert ([Math]::Abs($progress.HoldProgress - 1.0/6) -lt 0.0001) 'hold progress'; $v.Reset(); $progress=$v.Add(150,0); Assert ($progress.HoldProgress -eq 0 -and -not $progress.Confirmed) 'reset clears hold'
$labels=[SignLabels]::new([string[]]@('A','B','C','_other'))
$samples=[System.Collections.Generic.List[KnnClassifier+Sample]]::new(); $samples.Add([KnnClassifier+Sample]::new([float[]]@(0),0)); $samples.Add([KnnClassifier+Sample]::new([float[]]@(0.1),0)); $samples.Add([KnnClassifier+Sample]::new([float[]]@(1),1)); $samples.Add([KnnClassifier+Sample]::new([float[]]@(5),3))
$knn=[KnnClassifier]::new($samples,$labels,1,0.5,0); $r=$knn.Classify([float[]]@(0)); Assert (-not $r.Rejected -and $r.FirstLabel -eq 0) 'knn'; Assert (($knn.Classify([float[]]@(3))).Rejected) 'distance'; Assert (($knn.Classify([float[]]@(5))).IsOther) 'other'
$knn=[KnnClassifier]::new($samples,$labels,2,10,0); Assert (($knn.Classify([float[]]@(0.6))).Rejected) 'margin'



# 경계 조건은 별도 C# 코드로 직접 호출한다.
$testCode = @"
using System;
using System.Collections.Generic;
public static class SignBoundaryChecks {
 static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
 static void Bad(Action action) { bool failed=false; try { action(); } catch(ArgumentException) { failed=true; } Check(failed,"잘못된 입력 거부"); }
 static KnnClassifier.Sample S(float v,int label) { return new KnnClassifier.Sample(new float[]{v},label); }
 public static void Run() {
  bool left; Check(SignHandedness.TryIsLeft("Left",false,out left)&&left,"left"); Check(SignHandedness.TryIsLeft("Right",true,out left)&&left,"mirror"); Check(!SignHandedness.TryIsLeft("unknown",false,out left)&&!left,"invalid handedness");
  var blank=new SignSample{Person=" ",Session="s",Trial="t",Label="A",Width=1,Height=1,Handedness="Left"}; Bad(()=>blank.ToCsvLine());
  var p=new float[63]; p[27]=.1f; float[] a,b; Check(SignFeatures.TryCompute(p,640,480,false,out a),"base"); p[2]=99; Check(SignFeatures.TryCompute(p,640,480,false,out b),"z"); for(int i=0;i<a.Length;i++) Check(a[i]==b[i],"z invariant"); Check(Math.Abs(a[42]-Math.PI)<1e-6,"coincident angle"); Check(!SignFeatures.TryCompute(new float[62],640,480,false,out b),"length"); p[1]=float.NaN; Check(!SignFeatures.TryCompute(p,640,480,false,out b),"nan");
  Bad(()=>new SignVote(double.NaN)); Bad(()=>new SignVote(0.3,double.PositiveInfinity)); Bad(()=>new SignVote(0.3,0.2,double.NegativeInfinity));
  var v=new SignVote(); v.Add(0,0); Check(v.Add(50,-1).Label==-1,"unknown denominator"); Check(v.Add(100,0).Label==0,"majority");
  v=new SignVote(0.1,1,0.6); v.Add(0,0); Check(v.Add(101,1).Label==1,"window expiration");
  v=new SignVote(); for(int i=0;i<=500;i+=100) Check(!v.Add(i,0).Confirmed,"early"); Check(!v.Add(700,0).Confirmed,"gap reset"); for(int i=800;i<1300;i+=100) Check(!v.Add(i,0).Confirmed,"gap hold"); Check(v.Add(1300,0).Confirmed,"gap confirmation"); Check(!v.Add(1400,0).Confirmed,"one confirmation"); Check(!v.Add(1,0).Confirmed,"reverse reset");
  var labels=new SignLabels(new[]{"A","B","_other"});
  var other=new KnnClassifier(new[]{S(0,2)},labels); var r=other.Classify(new[]{0f}); Check(r.IsOther&&r.Rejected&&r.PredictedLabel==SignVote.Unknown,"other");
  var normal=new KnnClassifier(new[]{S(0,0)},labels); r=normal.Classify(new[]{0f}); Check(!r.Rejected&&r.PredictedLabel==0,"less than k");
  var empty=new KnnClassifier(new KnnClassifier.Sample[0],labels); Check(empty.Classify(new[]{0f}).Rejected,"empty");
  var tied=new KnnClassifier(new[]{S(-1,0),S(1,1)},labels,1); r=tied.Classify(new[]{0f}); Check(r.Rejected&&r.FirstScore==r.SecondScore,"distance tie");
  var margin=new KnnClassifier(new[]{S(0,0),S(.1f,0),S(.2f,1)},labels,3,10,2); Check(margin.Classify(new[]{0f}).Rejected,"integer margin");
  var distance=new KnnClassifier(new[]{S(0,1),S(2,0),S(3,0)},labels,3,1,1); r=distance.Classify(new[]{0f}); Check(r.FirstLabel==0&&r.Rejected&&r.NearestDistance==0&&r.FirstLabelDistance==2,"winner distance");
  Bad(()=>new KnnClassifier(new KnnClassifier.Sample[]{null},labels)); Bad(()=>new KnnClassifier(new[]{S(0,3)},labels)); Bad(()=>new KnnClassifier(new[]{S(0,0),new KnnClassifier.Sample(new[]{1f,2f},1)},labels)); Bad(()=>new KnnClassifier(new[]{S(float.NaN,0)},labels)); Bad(()=>new KnnClassifier(new[]{S(float.PositiveInfinity,0)},labels));
 }
}
"@
Add-Type -TypeDefinition $testCode -ReferencedAssemblies $dll
[SignBoundaryChecks]::Run()
$python = Get-Command python -ErrorAction SilentlyContinue
if ($python) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $python.Source --version 2>$null | Out-Null
    $available = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $previousPreference
} else { $available = $false }
if ($available) {
    & $python.Source (Join-Path $repo 'Tools/SignTrain/test_sign_features.py')
    if ($LASTEXITCODE -ne 0) { throw "Python 테스트 실패: $LASTEXITCODE" }
} else { Write-Host 'Python 인터프리터가 없어 Python 테스트 건너뜀' }
Write-Host 'SignFingerspelling tests passed'
