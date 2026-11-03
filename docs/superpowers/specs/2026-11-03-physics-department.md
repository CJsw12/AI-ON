# Physics Department Design

## Intent

Add a playable Physics Department route under Natural Sciences. Reuse the portraits and three physics-specific backgrounds already present in `Assets/Resources`. Build a first-semester campus story for the two standard professor archetypes, add one mechanics minigame with separate experienced and beginner difficulty, and leave the other faculties' behavior unchanged.

## Story proposal

The semester centers on preparing a hands-on mechanics demonstration for the campus festival. A rolling cart carrying fragile demonstration equipment reveals how small assumptions about momentum and stopping distance can cause a real lab mishap. The student and professors turn the problem into a collaborative project, balancing careful measurement with curiosity and communication.

The six chapters follow orientation, the first demonstration, interpreting experiment data, redesigning the apparatus, a disagreement about the final safety setup, and the festival demonstration. Use PhysicsLectureHall, PhysicsOpticsLab, and PhysicsResearchLounge as this route's exclusive backgrounds. No observatory or astronomy setting is used.

Working asset identities: both professors are men. `ProfessorOh` is the strict professor, with a vintage mustache-and-tweed physicist look; his Korean dialogue occasionally uses English physics nouns such as momentum, impulse, and measurement. `ProfessorIm` is the cheerful, clean-shaven professor, with a casual and animated Feynman-inspired lecturer look. His provisional Korean name is 임재하 교수; 오명환 remains the strict professor's provisional name. The cheerful professor explains through curiosity, playful questions, and approachable analogies. Maintain the project's two speech styles and youthful, non-dark tone.

## Minigame: “멈춰라, 운동량!”

The player stops a moving lab cart carrying delicate equipment by choosing one of three cushioning distances. A short pad causes a high average force; a long pad lowers the force but may cross the stopping boundary. The one-dimensional, constant-deceleration model uses `p = mv`, `J = Δp`, `Favg = Δp / Δt`, and `d = v₀t / 2`, which also gives `Favg = mv₀² / (2d)` for a cart brought to rest. The interface evaluates a small deterministic model and animates the result; it does not use Unity physics or require free-form math input.

The game has a brief practice round and three scored rounds. Each scored round previews mass, speed, stopping boundary, and the allowed average-force limit. The player selects a pad, launches the cart, and receives immediate success or failure feedback with a short explanation. Three successes award Delta 2, one or two award Delta 1, and zero award Delta 0.

Both modes use the same screen and controls:

- **전공자:** introductory General Physics calculations with simple values; derive momentum change, impulse, average force, and the minimum stopping distance from the supplied constraints. No calculus, trigonometry, or advanced mechanics.
- **비전공자:** high-school Physics I mechanics at a simple arithmetic and conceptual level; interpret mass-speed changes and the impulse/contact-time/average-force relationship with visual hints and scaffolded formulas.

The current minigame framework passes no experience flag to games. Add the smallest mode handoff needed for this new game and let the practice menu select the mode. Existing minigames retain their current behavior.

## Experienced-mode dialogue update

Keep the beginner story dialogue intact. Add an optional experienced-mode version for PhysicsStory only, where the same six chapter events unfold through lecture, lab-section, group-report, and seminar conversations. Professors address the class and compare group results instead of tutoring the protagonist one-on-one. Keep line counts identical to the default data so saved scene and dialogue indexes remain valid. The three experienced quiz feedback replies should use appropriate first-year General Physics language and refer to reports, measurement assumptions, and data comparison.

## Story structure

Follow the existing six-chapter, twelve-slot, eight-ending structure and append PhysicsStory.Data at the end of `Stories.All` so saved faculty indexes remain stable. Use six choices, three quizzes with different Major/Minor variants, and the new minigame in three story slots with progressively adjusted scenario data. Place the physics quizzes and game in different chapters from other departments where practical. Use the existing ending resolver and mood/background systems.

## Non-goals

- No physics engine, free-form calculator, networking, new packages, or scene/prefab edits.
- No changes to existing minigame behavior or other stories.
- No new minigame class beyond this mechanics game.
- No astronomy or observatory content.

## Acceptance criteria

- Physics appears under the Natural Sciences tab and can be selected, played through all six chapters, and recorded in existing saves/endings without changing current faculty indexes.
- Both professor portraits load in all three expressions; all story and minigame backgrounds resolve from Resources.
- A physics route has exactly twelve valid slots: six choices, three quizzes, and three physics minigames; each quiz changes appropriately by mode.
- The physics minigame's two modes preserve the shared mechanic while changing prompt difficulty, hints, and scenario data. Practice offers both modes.
- Both professors are male: strict `ProfessorOh` and cheerful Feynman-inspired `ProfessorIm`; the strict professor uses occasional English physics nouns naturally.
- Experienced-mode story dialogue reads like a university course and lab collaboration; beginner-mode dialogue remains unchanged, and both variants preserve per-chapter line counts.
- The new rules have deterministic edge-case coverage and the Unity project compiles.
