$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '../Assets/Scripts/SpeechTurnGate.cs'
if (-not (Test-Path $source)) { throw 'SpeechTurnGate is not implemented.' }
Add-Type -Path $source
function Check([bool]$value, [string]$message) { if (-not $value) { throw $message } }
$gate = New-Object SpeechTurnGate
$gate.Reset(0)
Check (-not $gate.Feed(0.001, 2, 0.012)) 'Initial silence must never submit.'
Check (-not $gate.Feed(0.003, 5, 0.012)) 'Background noise must not count as speech.'
$gate.Feed(0.08, 5.1, 0.012) | Out-Null
$gate.Feed(0.09, 5.4, 0.012) | Out-Null
Check (-not $gate.Feed(0.001, 6.0, 0.012)) 'A short pause must allow the student to continue.'
Check ($gate.Feed(0.001, 6.8, 0.012)) 'A completed utterance followed by silence must finish.'
$gate.Reset(10)
$gate.Feed(0.2, 10.5, 0.012) | Out-Null
Check (-not $gate.Feed(0.001, 12, 0.012)) 'One spike must not submit an answer.'
$gate.Reset(20)
Check (-not $gate.HasSpeech) 'A new turn must not reuse old speech.'
Check (-not $gate.Feed([double]::NaN, 21, 0.012)) 'Invalid volume must not submit.'
$gate.Feed(0.1, 21.1, 0.012) | Out-Null
$gate.Feed(0.1, 21.5, 0.012) | Out-Null
Check ($gate.Feed(0.1, 49, 0.012)) 'Maximum recording length must end a real utterance.'
Check ([SpeechTurnGate]::Command('잠깐만요.') -eq 'pause') 'Punctuation must not hide a voice command.'
Check ([SpeechTurnGate]::Command('다시 말할게요') -eq 'retry') 'A retry command must not be graded as an answer.'
Check ([SpeechTurnGate]::Command('저는 다시 반복문을 사용하겠습니다') -eq '') 'Ordinary answers must not be mistaken for commands.'
'SpeechTurnGate: 11 behavioral assertions passed.'
