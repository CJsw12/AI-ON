$ErrorActionPreference='Stop'
$p=Join-Path $PSScriptRoot '../Assets/Scripts/CodeLabTrace.cs'
if(-not(Test-Path $p)){throw 'CodeLabTrace not implemented'}
Add-Type -Path $p
function Check($v,$m){if(-not $v){throw $m}}
$t=New-Object CodeLabTrace -ArgumentList (,[int[]]@(4,8,12))
Check ($t.Current.Sum -eq 0) 'Start with an empty accumulator'
while(-not $t.Current.Finished){$t.Step() | Out-Null}
Check ($t.Current.Sum -eq 12 -and $t.Current.Average -eq 4) 'The reset bug must produce 4'
Check (-not $t.Step()) 'End of trace must not step beyond its bounds'
$t.Repair($true)
Check ($t.Current.Sum -eq 0 -and $t.Position -eq 0) 'Repair must restart, never mix old and new runs'
while(-not $t.Current.Finished){$t.Step() | Out-Null}
Check ($t.Current.Sum -eq 24 -and $t.Current.Average -eq 8) 'The repaired trace must produce 8'
$t.Reset()
Check ($t.Repaired -and $t.Current.Sum -eq 0) 'Restart preserves the chosen repair'
$empty=New-Object CodeLabTrace -ArgumentList (,[int[]]@())
while(-not $empty.Current.Finished){$empty.Step() | Out-Null}
Check ($empty.Current.Average -eq $null) 'Empty input must never divide by zero'
$one=New-Object CodeLabTrace -ArgumentList (,[int[]]@(8))
while(-not $one.Current.Finished){$one.Step() | Out-Null}
Check ($one.Current.Average -eq 8) 'A single value hides the reset bug'
'CodeLabTrace: 8 behavioral checks passed'
