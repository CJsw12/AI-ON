$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '../Assets/Scripts/IPhoneCalibrationGate.cs'
if (-not (Test-Path -LiteralPath $source)) { throw 'IPhoneCalibrationGate is not implemented.' }
Add-Type -Path $source
function Check([bool]$value, [string]$message) { if (-not $value) { throw $message } }
$gate = New-Object IPhoneCalibrationGate
Check (-not $gate.CanCapture(100, 1.25)) 'An unseen target must not collect samples even after a long delay.'
$gate.MarkRendered(100)
$gate.MarkRendered(101)
Check (-not $gate.CanCapture(101, 1.25)) 'Wait for refixation after first rendering.'
Check ($gate.CanCapture(101.25, 1.25)) 'Repeated paints must not restart the settling timer.'
Check ($gate.CanCapture(101.6, 1.25)) 'A brief rendering hitch must not discard a visible target.'
Check (-not $gate.CanCapture(104, 1.25)) 'A long-hidden target must not keep collecting samples.'
$gate.Reset()
Check (-not $gate.CanCapture(500, 1.25)) 'A new or retried target must be rendered again.'
$samples = New-Object 'System.Collections.Generic.List[double[]]'
for ($i=0; $i -lt 36; $i++) { $samples.Add([double[]]@((1 + ($i % 3) * .1), (2 + ($i % 2) * .1))) }
Check ([IPhoneCalibrationGate]::Stable($samples)) 'Small tracker noise must be accepted.'
$samples[0] = [double[]]@(40, 40)
Check ([IPhoneCalibrationGate]::Stable($samples)) 'One short spike must not reject a stable fixation.'
for ($i=18; $i -lt 36; $i++) { $samples[$i] = [double[]]@(4, 2) }
Check (-not [IPhoneCalibrationGate]::Stable($samples)) 'An eye movement during collection must be retried.'
$samples[0] = [double[]]@([double]::NaN, 0)
Check (-not [IPhoneCalibrationGate]::Stable($samples)) 'Invalid sensor values must not be learned.'
$samples.Clear()
Check (-not [IPhoneCalibrationGate]::Stable($samples)) 'An empty or insufficient window must fail.'
Check ([IPhoneCalibrationGate]::Accurate(90, 150)) 'A good independent check must pass.'
Check (-not [IPhoneCalibrationGate]::Accurate(310, 454)) 'The observed bad calibration must not be accepted.'
Check (-not [IPhoneCalibrationGate]::Accurate(100, 300)) 'A large tail error must fail even with an acceptable mean.'
Check (-not [IPhoneCalibrationGate]::Accurate([double]::NaN, 10)) 'Invalid check results must fail.'
Check (-not [IPhoneCalibrationGate]::Accurate(10, -1)) 'A missing independent check must fail.'
'IPhoneCalibrationGate: 17 behavioral assertions passed.'
