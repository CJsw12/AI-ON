$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../Assets/Scripts/Sign/SignVote.cs')
$cases = @(
    @{ name='hold-boundary'; inputs=@(@(0,0),@(100,0),@(200,0),@(300,0),@(400,0),@(500,0),@(599,0),@(600,0),@(650,0)) },
    @{ name='no-hand'; inputs=@(@(0,0),@(100,0),@(150,-2),@(200,0),@(300,0),@(400,0),@(500,0),@(600,0),@(700,0),@(800,0)) },
    @{ name='gap'; inputs=@(@(0,1),@(100,1),@(300,1),@(400,1),@(350,1),@(450,1)) },
    @{ name='tie-unknown'; inputs=@(@(0,0),@(50,1),@(100,-1),@(150,1),@(200,0),@(250,0),@(300,-1)) }
)
$output = foreach ($case in $cases) {
    $vote = [SignVote]::new()
    $entries = foreach ($inputPair in $case.inputs) {
        $r = $vote.Add([long]$inputPair[0], [int]$inputPair[1])
        @{ time=[long]$inputPair[0]; input=[int]$inputPair[1]; label=$r.Label; confirmed=$r.Confirmed; progress=$r.HoldProgress }
    }
    @{ name=$case.name; entries=@($entries) }
}
$dir = Join-Path $PSScriptRoot 'fixtures'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
[IO.File]::WriteAllText((Join-Path $dir 'sign_vote_cases.json'), (ConvertTo-Json -InputObject @($output) -Depth 8), [Text.UTF8Encoding]::new($false))
