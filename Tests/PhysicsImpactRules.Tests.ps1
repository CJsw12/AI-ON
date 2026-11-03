$ErrorActionPreference = 'Stop'
$rulesPath = Join-Path $PSScriptRoot '..\Assets\Scripts\PhysicsImpactRules.cs'
if (-not (Test-Path -LiteralPath $rulesPath)) {
    throw 'PhysicsImpactRules is not defined yet.'
}
Add-Type -Path $rulesPath

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Assert-Near([double]$actual, [double]$expected, [string]$message) {
    if ([math]::Abs($actual - $expected) -gt 0.001) {
        throw "$message (expected $expected, got $actual)"
    }
}

$force = [single]0
Assert-True ([PhysicsImpactRules]::TryEvaluate(2, 4, 1, 1.2, 16, [ref]$force)) 'Known one-meter cushion should be safe.'
Assert-Near $force 16 'Average force for 2 kg at 4 m/s over 1 m is 16 N.'

$shortForce = [single]0
$longForce = [single]0
Assert-True ([PhysicsImpactRules]::TryEvaluate(2, 4, 0.5, 1.2, 40, [ref]$shortForce)) 'Short cushion should fit and pass its generous force limit.'
Assert-True ([PhysicsImpactRules]::TryEvaluate(2, 4, 1, 1.2, 40, [ref]$longForce)) 'Longer cushion should fit and pass.'
Assert-True ($longForce -lt $shortForce) 'A longer stopping distance should reduce average force.'

$boundaryForce = [single]0
Assert-True ([PhysicsImpactRules]::TryEvaluate(2, 4, 1, 1, 16, [ref]$boundaryForce)) 'Exact force and distance boundaries should pass.'
Assert-True (-not [PhysicsImpactRules]::TryEvaluate(2, 4, 0.99, 1.2, 16, [ref]$force)) 'Force just above the limit should fail.'
Assert-True (-not [PhysicsImpactRules]::TryEvaluate(2, 4, 1.01, 1, 100, [ref]$force)) 'Pad extending past the stopping boundary should fail.'

Assert-True (-not [PhysicsImpactRules]::TryEvaluate(0, 4, 1, 1.2, 16, [ref]$force)) 'Zero mass should be rejected.'
Assert-True (-not [PhysicsImpactRules]::TryEvaluate(2, [float]::NaN, 1, 1.2, 16, [ref]$force)) 'NaN speed should be rejected.'
Assert-True (-not [PhysicsImpactRules]::TryEvaluate(2, 4, [float]::PositiveInfinity, 1.2, 16, [ref]$force)) 'Infinite cushion should be rejected.'
Assert-True (-not [PhysicsImpactRules]::TryEvaluate(2, 4, 1, 1.2, -1, [ref]$force)) 'Negative force limit should be rejected.'

'PhysicsImpactRules: 10 assertions passed.'
