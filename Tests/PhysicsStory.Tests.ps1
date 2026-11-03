$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$storyPath = Join-Path $repo 'Assets\Scripts\Stories\Natural\PhysicsStory.cs'
$registryPath = Join-Path $repo 'Assets\Scripts\Stories\Story.cs'
if (-not (Test-Path -LiteralPath $storyPath)) { throw 'PhysicsStory is not defined yet.' }

$story = Get-Content -Raw -Encoding UTF8 $storyPath
$registry = Get-Content -Raw -Encoding UTF8 $registryPath
if ($registry -notmatch 'Story\[\]\s+All\s*=\s*\{\s*AiSwStory\.Data,\s*SmartSystemsStory\.Data,\s*BusinessStory\.Data,\s*ArtsHealthStory\.Data,\s*PhysicsStory\.Data(?=\s*[,}])') {
    throw 'PhysicsStory.Data must remain fifth, after the original four faculties.'
}
if ($story -notmatch 'College\s*=\s*"자연과학부"' -or $story -notmatch 'Name\s*=\s*"물리학과"') { throw 'Physics story is not identified under Natural Sciences.' }
if (($story | Select-String -Pattern 'new Slot\s*\{' -AllMatches).Matches.Count -ne 12) { throw 'PhysicsStory must define exactly twelve slots.' }
if (($story | Select-String -Pattern '(?m)^            Options\s*=\s*new\[\]' -AllMatches).Matches.Count -ne 6) { throw 'PhysicsStory must define six choice slots.' }
if (($story | Select-String -Pattern 'Major\s*=\s*new Quiz' -AllMatches).Matches.Count -ne 3) { throw 'PhysicsStory must define three major/minor quiz slots.' }
if (($story | Select-String -Pattern 'Minor\s*=\s*new Quiz' -AllMatches).Matches.Count -ne 3) { throw 'PhysicsStory must define three beginner quiz slots.' }
if (($story | Select-String -Pattern 'Kind\s*=\s*MiniGameKind\.ImpactLab' -AllMatches).Matches.Count -ne 3) { throw 'PhysicsStory must define three ImpactLab slots.' }
if ($story -notmatch 'Name\s*=\s*"오명환 교수"' -or $story -notmatch 'Name\s*=\s*"임재하 교수"') { throw 'Both requested professors must be present.' }
foreach ($term in @('measurement|측정', 'model|모델', 'instrument|센서|계기', 'momentum|운동량', 'stopping distance|정지\s*거리|정지선', 'kinetic energy|운동\s*에너지', 'data|데이터')) {
    if ($story -notmatch $term) { throw "Physics concepts are missing '$term'." }
}
foreach ($background in @('PhysicsLectureHall','PhysicsOpticsLab','PhysicsResearchLounge')) {
    if ($story -notmatch [regex]::Escape($background)) { throw "Physics story is missing background '$background'." }
}
if (($story | Select-String -Pattern 'EndingNames\s*=\s*new\[\]' -AllMatches).Matches.Count -ne 1 -or
    ($story | Select-String -Pattern 'EndingText\s*=\s*new\[\]' -AllMatches).Matches.Count -ne 1 -or
    ($story | Select-String -Pattern 'EndingLines\s*=\s*new\[\]' -AllMatches).Matches.Count -ne 1) { throw 'All eight ending collections must be present.' }
foreach ($resource in @('ProfessorOhTransparent','ProfessorOhHappyTransparent','ProfessorOhFrownTransparent','ProfessorImTransparent','ProfessorImHappyTransparent','ProfessorImFrownTransparent','PhysicsLectureHall','PhysicsOpticsLab','PhysicsResearchLounge')) {
    if (-not (Test-Path (Join-Path $repo "Assets\Resources\$resource.png"))) { throw "Missing story resource $resource.png" }
}

'Physics story structure: 12 slots, faculty append order, and referenced resources verified.'
