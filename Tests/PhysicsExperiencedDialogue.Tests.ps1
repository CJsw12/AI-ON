$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$storyBase = Get-Content -Raw (Join-Path $repo 'Assets\Scripts\Stories\Story.cs')
$controller = Get-Content -Raw (Join-Path $repo 'Assets\Scripts\GameController.cs')
$physics = Get-Content -Raw (Join-Path $repo 'Assets\Scripts\Stories\Natural\PhysicsStory.cs')
$impact = Get-Content -Raw (Join-Path $repo 'Assets\Scripts\ImpactLab.cs')

if ($storyBase -notmatch 'ExperiencedDialogue' -or $storyBase -notmatch 'ExperiencedFollowUp') {
    throw 'Story does not yet support experienced-mode chapter and follow-up lines.'
}
if ($storyBase -notmatch 'Line\(int professor, int chapter, int index, out string speaker, out string text, bool experienced = false\)' -or
    $storyBase -notmatch 'FollowUpLine\(int professor, int chapter, int index, bool experienced = false\)') {
    throw 'Story line APIs do not yet accept an experienced-mode selection.'
}
if ($controller -notmatch 'S\.Line\(professor, scene, dialogueIndex, out speaker, out text, experienced\)' -or
    $controller -notmatch 'S\.FollowUpLine\(professor, scene, dialogueIndex, experienced\)') {
    throw 'GameController does not yet pass the selected mode to story dialogue.'
}
if ($physics -notmatch 'ExperiencedDialogue\s*=\s*ExperiencedDialogue' -or
    $physics -notmatch 'ExperiencedFollowUp\s*=\s*ExperiencedFollowUp') {
    throw 'PhysicsStory does not yet provide its own experienced-mode dialogue.'
}
foreach ($marker in @('조별', '실험 보고서', '세미나', '가설', '불확실도')) {
    if ($physics -notmatch [regex]::Escape($marker)) { throw "Experienced university dialogue is missing '$marker'." }
}
if ($physics -notmatch '일반물리학 I · 역학 계산과 조별 데이터 분석') { throw 'Experienced level selection still reads like basic tutoring.' }
if ($impact -notmatch '실험 조의 완충 설계 검토') { throw 'Experienced minigame instructions still read like one-to-one tutoring.' }

'Experienced dialogue routing: selected only for PhysicsStory major mode; novice data remains shared.'
