# Physics Department Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a complete, playable Physics Department route and a compact mechanics minigame with experienced and beginner modes.

**Architecture:** Add a deterministic plain C# rules class and one IMGUI `MiniGame` subclass. Thread the experience flag only into the new game; preserve constructors and behavior for existing games. Add a single story data class and append it to the faculty registry.

**Tech Stack:** Unity 6000.3.23f1, C#, existing IMGUI `MiniGame` framework, PowerShell regression harness for pure physics rules, Unity batch compile and player validation.

**Spec:** `docs/superpowers/specs/2026-11-03-physics-department.md`

## Global Constraints

- Keep the six generated portrait PNGs and three physics backgrounds in `Assets/Resources`, each with its unique `.meta`; do not replace them.
- Preserve all existing faculty order and save indexes; append PhysicsStory.Data only.
- Keep the project on Unity 6000.3.23f1 and add no packages or scene/prefab edits.
- Existing games continue to use their current constructors, experience behavior, and practice behavior.
- Physics uses the project's 1280×900 IMGUI canvas, `MiniGame.Now` clock, pause behavior, result/reply contract, and three-star Delta mapping.
- Do not disclose hidden ending conditions in README or commit messages.

## Review Focus

- **Zero/negative or non-finite scenario values:** rules reject invalid inputs without division-by-zero or NaN scores; test in Task 1.
- **Force threshold and cushion boundaries:** equality at safe force and stopping boundary counts as success; test in Task 1.
- **Experience-mode routing:** story and practice use the selected mode; existing minigames remain unchanged; exercise both paths in Task 2.
- **Repeated rounds and completion:** one round cannot score twice, exactly three rounds set `Done` and Delta; test in Task 2.
- **Story arrays/assets/registry order:** all 12 slots and all referenced resources are valid; validate in Task 3.

---

### Task 1: Deterministic impulse and cushioning rules

**Files:**
- Create: `Assets/Scripts/PhysicsImpactRules.cs`
- Create: `Tests/PhysicsImpactRules.Tests.ps1`

**Interfaces:**
- Produces `PhysicsImpactRules.TryEvaluate(float massKg, float speedMps, float cushionMeters, float stopDistanceMeters, float maxForceNewtons, out float averageForceNewtons)`.
- Uses uniform deceleration to rest: `Favg = mass * speed * speed / (2 * cushionDistance)`; a choice succeeds only when the pad fits the available stop distance and calculated force is at or below the safe limit.

- [x] Write PowerShell assertions first for a known safe case (2 kg, 4 m/s, 1 m pad → 16 N), a longer pad lowering force, equality boundaries, and invalid/non-finite inputs.
- [x] Run the harness and confirm it fails because `PhysicsImpactRules` is not defined.
- [x] Implement the minimal Unity-independent rules class.
- [x] Re-run the harness and confirm all cases pass.

### Task 2: Small three-round cart-impact minigame and mode handoff

**Files:**
- Create: `Assets/Scripts/ImpactLab.cs` and `.meta`
- Modify: `Assets/Scripts/MiniGames.cs`
- Modify: `Assets/Scripts/GameController.cs`
- Modify: `Assets/Scripts/GameMenus.cs`

**Interfaces:**
- Add `MiniGameKind.ImpactLab` and dispatch it to `ImpactLab`.
- `ImpactLab(MiniGameTheme theme, int professor, bool experienced, bool practice)` uses the pure rules API from Task 1.
- Existing constructors are not changed; only `ImpactLab` receives the mode flag.
- Story flow passes its `experienced` value. Practice exposes a mode selector for Physics and defaults to beginner mode; other faculty practice layouts retain current behavior.

- [x] Add and run a regression check for `MiniGames.Create`, existing kind dispatch, and experience-mode handoff; it failed before the factory route existed.
- [x] Implement the factory route and experienced-mode handoff without changing existing constructors.
- [x] Implement instruction, preview, launch, feedback, and completed states; the tutorial is unscored and exactly three rounds are scored.
- [x] Use three cushion buttons, a clear force/stop-distance result, accessible feedback, and the existing sound/effects hooks. Keep scoring 3/3→2, 1–2/3→1, 0/3→0.
- [x] Provide beginner hints and simple values; provide introductory General Physics calculations for experienced mode. Retain identical controls.
- [x] Add the Physics practice mode toggle without changing other faculty layouts.
- [x] Compile the modified scripts with Unity in the isolated audit copy. Defer full round-by-round player interaction to Task 5, after the Physics route is registered and reachable.

### Task 3: Physics Department six-chapter story and registration

**Files:**
- Create: `Assets/Scripts/Stories/Natural/PhysicsStory.cs` and `.meta`
- Modify: `Assets/Scripts/Stories/Story.cs`

**Interfaces:**
- `PhysicsStory.Data` uses `College = "자연과학부"`, two professors mapped to `ProfessorOh`/`ProfessorIm`, 6 chapters, 12 slots, 8 endings, and the existing quiz/minigame APIs.
- Quiz slots include Major and Minor prompts/replies. Three ImpactLab themes vary place/scenario data while retaining the same mini-game class.
- `Stories.All` appends PhysicsStory.Data after ArtsHealthStory.Data.

- [x] Add a validation script/check first for registry order, 12 slots, six choices/three quiz slots/three game slots, parallel array lengths, referenced portraits/backgrounds, and professor-visible lines before interactive slots.
- [x] Run the check and confirm it fails because PhysicsStory is not registered.
- [x] Write a six-chapter festival demonstration story with non-repeating conflict beats, a strict mustached professor and a playful Feynman-inspired male professor, and occasional English physics nouns in the strict professor's Korean dialogue.
- [x] Add three beginner/experienced mechanics questions and six choices; map the three game slots to PhysicsLectureHall, PhysicsOpticsLab, and PhysicsResearchLounge.
- [x] Add eight ending texts/lines using the existing resolver, without printing secret ending criteria.
- [x] Append the new story at the end of Stories.All.
- [x] Re-run story validation; confirm previous four faculty indexes are unchanged and the new fifth faculty belongs to Natural Sciences.

### Task 4: Player-facing setup and guide updates

**Files:**
- Modify: `README.md`
- Modify: `STORY_GUIDE.md`
- Modify: `Assets/Scripts/Stories/Story.cs` comments only if required to explain mode-specific minigame data.

- [x] Update the department table and Physics minigame/experience description.
- [x] Document the strict professor's English terminology convention, unique Physics backgrounds, and existing six-file portrait naming pattern.
- [x] Document the new minigame interface and two modes without changing other teams' minigame workflow.
- [x] Verify all documented filenames and constraints match the implementation.

### Task 5: Compile, route, save, and minigame validation

**Files:**
- No production files unless a validation failure requires a focused correction.

- [x] Run the physics rules PowerShell harness and story validation script.
- [x] Compile with Unity 6000.3.23f1; inspect the complete compiler log for new C# errors.
- [x] Run a player smoke check for both mode quiz variants, one complete ImpactLab round set per mode, all six chapters, save/load, and ending transition. The automated route begins after faculty/professor selection; title-screen clicking was not manually exercised.
- [x] Confirm the original four faculty registries and representative existing minigames still route as before.
- [x] Review the final diff and report exact validation evidence and any unverified presentation details.

### Task 6: University-style experienced-mode dialogue

**Files:**
- Modify: `Assets/Scripts/Stories/Story.cs`, `Assets/Scripts/GameController.cs`, `Assets/Scripts/Stories/Natural/PhysicsStory.cs`, `Assets/Scripts/ImpactLab.cs`
- Modify: `README.md`, `STORY_GUIDE.md`, and the Physics Department spec
- Create: `Tests/PhysicsExperiencedDialogue.Tests.ps1`

- [x] Add a regression check first for experienced-only dialogue data, mode handoff, and university-course vocabulary; confirm it fails before the feature exists.
- [x] Add optional major-mode dialogue arrays and route the selected experience level through the existing story line accessors.
- [x] Write class-, lab-group-, TA-, report-, and seminar-based dialogue while preserving line counts and novice dialogue.
- [x] Update major quiz feedback, level-selection notes, and ImpactLab framing to match the collaborative university setting.
- [x] Verify all harnesses and compile/runtime routes in Unity 6000.3.23f1.
