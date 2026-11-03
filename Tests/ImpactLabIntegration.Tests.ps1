$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$miniGamesPath = Join-Path $repo 'Assets\Scripts\MiniGames.cs'
$controllerPath = Join-Path $repo 'Assets\Scripts\GameController.cs'
$menusPath = Join-Path $repo 'Assets\Scripts\GameMenus.cs'
$miniGames = Get-Content -Raw $miniGamesPath
$controller = Get-Content -Raw $controllerPath
$menus = Get-Content -Raw $menusPath

if ($miniGames -notmatch 'ImpactLab') { throw 'ImpactLab is not registered in the minigame factory.' }
if (-not (Test-Path (Join-Path $repo 'Assets\Scripts\ImpactLab.cs'))) { throw 'ImpactLab implementation is missing.' }
if ($miniGames -notmatch 'Create\(MiniGameTheme theme, int professor, bool practice = false, bool experienced = false\)') { throw 'Factory does not accept an experience mode.' }
if ($miniGames -notmatch 'new ImpactLab\(theme, professor, experienced, practice\)') { throw 'Factory does not pass both selected mode and practice state.' }
if ($miniGames -notmatch 'case MiniGameKind\.Cards: return new CardMatch\(theme\);' -or $miniGames -notmatch 'default: return new CoffeeOrder\(professor, theme, practice\);') {
    throw 'Representative existing minigame routes changed.'
}
if ($controller -notmatch 'MiniGames\.Create\(slot\.Game, professor, false, experienced\)') { throw 'Story launch does not pass the selected experience mode.' }
if ($menus -notmatch 'practiceExperienced' -or $menus -notmatch 'MiniGames\.Create\(theme, practiceProfessor, true, practiceExperienced\)') {
    throw 'Physics practice cannot select and pass an experience mode.'
}

'ImpactLab integration: factory, story mode, practice mode, and existing routes verified.'
