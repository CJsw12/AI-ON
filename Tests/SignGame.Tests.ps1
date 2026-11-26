$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$paths = @('SignSample.cs','SignFeatures.cs','SignVote.cs','SignLabels.cs','KnnClassifier.cs','SignDataset.cs','SignGameSession.cs') | ForEach-Object { Join-Path $repo "Assets/Scripts/Sign/$_" }
Add-Type -Path $paths
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$dir = Join-Path ([IO.Path]::GetTempPath()) ('sign-game-' + [guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory $dir | Out-Null
 Assert ([SignDataset]::Load($dir).IsEmpty) 'empty'
 $fixture = (Get-Content (Join-Path $PSScriptRoot 'Fixtures/sign-features.json') -Raw -Encoding UTF8 | ConvertFrom-Json)[0]
 $s = [SignSample]::new(); $s.Person='p'; $s.Session='s'; $s.Trial='t'; $s.Label='A'; $s.Width=640; $s.Height=480; $s.Handedness='Right'; $s.Landmarks=[float[]]$fixture.landmarks
 $a=$s.ToCsvLine(); $s.Label='_other'; $other=$s.ToCsvLine()
 New-Item -ItemType Directory (Join-Path $dir 'nested') | Out-Null
 [IO.File]::WriteAllLines((Join-Path $dir 'data.csv'), [string[]]@([SignSample]::Header,$a,$other,'invalid'))
 $s.Trial='t2'; $other2=$s.ToCsvLine(); $s.Label='A'; $a2=$s.ToCsvLine()
 [IO.File]::WriteAllLines((Join-Path $dir 'nested/second.csv'), [string[]]@(([char]0xFEFF + [SignSample]::Header),$a2,$other2,'invalid2'))
 $data=[SignDataset]::Load($dir)
 Assert ($data.FileCount -eq 2 -and $data.InvalidLines -eq 2 -and $data.Samples.Count -eq 4) 'load counts'
 Assert ($data.Labels.IndexOf('_other') -ge 0 -and $data.Counts['A'] -eq 2) 'labels'
 Assert ([Math]::Abs($data.SuggestedMaxDistance - 0.05) -lt 0.000001) 'minimum distance'
 $s.Label='A'; $points=[float[]]$s.Landmarks.Clone(); $points[3]+=0.05; $s.Landmarks=$points; $changed=$s.ToCsvLine()
 [IO.File]::WriteAllLines((Join-Path $dir 'nested/second.csv'), [string[]]@([SignSample]::Header,$changed,$other2))
 $different=[SignDataset]::Load($dir); $vectors=@($different.Samples | Where-Object { $_.Label -eq $different.Labels.IndexOf('A') }); $sum=0.0
 for ($i=0; $i -lt $vectors[0].Features.Length; $i++) { $delta=[double]$vectors[0].Features[$i]-$vectors[1].Features[$i]; $sum+=$delta*$delta }
 Assert ($sum -gt 0 -and [Math]::Abs($different.SuggestedMaxDistance - [Math]::Sqrt($sum)*1.5) -lt 0.00001) 'distance percentile'
 $s.Trial='t'; $near=[float[]]$fixture.landmarks; $near[3]+=0.000001; $s.Landmarks=$near; $nearLine=$s.ToCsvLine()
 [IO.File]::WriteAllLines((Join-Path $dir 'data.csv'), [string[]]@([SignSample]::Header,$a,$nearLine,$other,'invalid'))
 $adjacent=[SignDataset]::Load($dir)
 Assert ($adjacent.SuggestedMaxDistance -gt $different.SuggestedMaxDistance * 0.9) 'same trial neighbours excluded'
 $classifier=[KnnClassifier]::new($data.Samples,$data.Labels); $scores=$classifier.Classify($data.Samples[0].Features).LabelScores
 Assert ([Math]::Abs(($scores | Measure-Object -Sum).Sum - 1) -lt 0.00001) 'score sum'
 Remove-Item -LiteralPath (Join-Path $dir 'nested/second.csv')
 Assert ([float]::IsPositiveInfinity([SignDataset]::Load($dir).SuggestedMaxDistance)) 'singleton threshold'
 $identityDir=Join-Path $dir 'identity'; New-Item -ItemType Directory $identityDir | Out-Null
 $s.Label='A'; $s.Trial='same'; $s.Person='p1'; $s.Session='s'; $one=$s.ToCsvLine(); $s.Person='p2'; $two=$s.ToCsvLine()
 [IO.File]::WriteAllLines((Join-Path $identityDir 'people.csv'), [string[]]@([SignSample]::Header,$one,$two))
 Assert ([Math]::Abs([SignDataset]::Load($identityDir).SuggestedMaxDistance - 0.05) -lt 0.000001) 'person distinguishes trials'
 $s.Person='p1'; $s.Session='s2'; $two=$s.ToCsvLine(); [IO.File]::WriteAllLines((Join-Path $identityDir 'people.csv'), [string[]]@([SignSample]::Header,$one,$two))
 Assert ([Math]::Abs([SignDataset]::Load($identityDir).SuggestedMaxDistance - 0.05) -lt 0.000001) 'session distinguishes trials'
 Remove-Item -LiteralPath (Join-Path $identityDir 'people.csv')
 $bulk = [System.Collections.Generic.List[string]]::new(); $bulk.Add([SignSample]::Header); $s.Label='A'; $s.Landmarks=[float[]]$fixture.landmarks
 for ($i=0; $i -lt 1100; $i++) { $s.Trial='t'+($i % 3); $s.Landmarks[3]=[float]([double]$fixture.landmarks[3]+0.05*($i % 3)+0.0001*($i % 30)); $bulk.Add($s.ToCsvLine()) }
 [IO.File]::WriteAllLines((Join-Path $dir 'data.csv'), $bulk)
 $bulk1=[SignDataset]::Load($dir); $bulk2=[SignDataset]::Load($dir)
 Assert ($bulk1.DistanceReferenceCount -eq 1000 -and $bulk2.DistanceReferenceCount -eq 1000 -and $bulk1.SuggestedMaxDistance -gt 0.05 -and $bulk1.SuggestedMaxDistance -eq $bulk2.SuggestedMaxDistance) 'sample determinism'
 $onlyOther=[SignLabels]::new([string[]]@('_other')); $threw=$false
 try { [void][SignGameSession]::new($onlyOther) } catch { $threw=$true }; Assert $threw 'other only rejected'
 $labels=[SignLabels]::new([string[]]@('A','B'))
 $game=[SignGameSession]::new($labels,1000,17); $game.BeginPractice()
 $first=$game.CurrentLabel; $game.Update(0,$first); $second=$game.CurrentLabel; $game.Update(1,$first)
 Assert ($game.CurrentLabel -eq $second) 'duplicate'
 $game.Update(2,$null); $game.Update(3,$second)
 Assert ($game.CurrentStage -eq [SignGameSession+Stage]::Challenge) 'practice'
 Assert ($game.RemainingMs(103) -eq 900) 'remaining time'
 $target=$game.CurrentLabel; $wrong=if ($target -eq 'A') { 'B' } else { 'A' }
 $game.Update(4,$null); $game.Update(100,$wrong)
 Assert ($game.CurrentLabel -eq $target -and $game.Answers.Count -eq 0 -and $game.LastWrongLabel -eq $wrong -and $game.WrongAttempts -eq 1) 'wrong remains on target'
 $game.Update(101,$null); $game.Update(200,$target)
 Assert ($game.Answers.Count -eq 1 -and $game.Answers[0].Correct -and $game.Answers[0].WrongAttempts -eq 1 -and $game.CurrentLabel -ne $target) 'wrong then correct advances'
 Assert ($game.WrongAttempts -eq 0 -and [string]::IsNullOrEmpty($game.LastWrongLabel)) 'new target resets attempts'
 $target=$game.CurrentLabel; $wrong=if ($target -eq 'A') { 'B' } else { 'A' }
 $game.Update(201,$null); $game.Update(300,$wrong); $game.Update(301,$null); $game.Update(400,$wrong)
 Assert ($game.WrongAttempts -eq 2 -and $game.Answers.Count -eq 1) 'repeated wrong waits'
 $game.Update(1200,$null)
 Assert ($game.CurrentStage -eq [SignGameSession+Stage]::Result -and $game.CorrectCount -eq 1 -and $game.Answers.Count -eq 2) 'results'
 Assert ($game.Answers[0].Correct -and -not $game.Answers[1].Correct -and $game.Answers[1].ElapsedMs -eq 1000 -and $game.Answers[1].WrongAttempts -eq 2) 'timeout'
 $game.Update(1300,'A'); Assert ($game.Answers.Count -eq 2) 'result ignores input'
 $many=[SignGameSession]::new([SignLabels]::new([string[]]@('A','B','C','D','E','F','_other')),1000,17)
 $many.Update(0,'A'); Assert ($many.CurrentStage -eq [SignGameSession+Stage]::Intro) 'intro ignores input'
 $many.BeginPractice(); for ($i=0; $i -lt 6; $i++) { $many.Update(($i*2),$many.CurrentLabel); $many.Update(($i*2+1),$null) }
 Assert ($many.CurrentStage -eq [SignGameSession+Stage]::Challenge) 'six practice letters'
 $current=$many.CurrentLabel; $many.Update(9,$current); Assert ($many.CurrentLabel -eq $current -and $many.Answers.Count -eq 0) 'backward timestamp ignored'
 for ($i=0; $i -lt 5; $i++) { $many.Update((20+$i*2),$many.CurrentLabel); $many.Update((21+$i*2),$null) }
 Assert ($many.CurrentStage -eq [SignGameSession+Stage]::Result -and $many.Answers.Count -eq 5) 'challenge maximum five'
 Write-Output 'SignGame tests passed'
} finally { Remove-Item -LiteralPath $dir -Recurse -Force }
