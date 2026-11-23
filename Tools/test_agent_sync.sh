#!/usr/bin/env bash
# Sandbox test for Tools/agent-sync.sh and Tools/git-hooks: two clones ("PCs") share a bare remote.
# Touches nothing outside a temp folder. Run: bash Tools/test_agent_sync.sh  (exit 0 = all passed)
set -u
src=$(cd "$(dirname "$0")/.." && pwd)
tmp=$(mktemp -d "${TMPDIR:-/tmp}/aion-agent-sync.XXXXXX")
trap 'rm -rf "$tmp"' EXIT
export AION_SKIP_TESTS=1 AION_SKIP_COMPILE_CHECK=1 AION_FETCH_TTL=0
unset AION_AGENT_ID
pass=0 fail=0
ok() { echo "PASS: $1"; pass=$((pass + 1)); }
ng() { echo "FAIL: $1"; fail=$((fail + 1)); }
expect_ok() { local name=$1; shift; if "$@" >"$tmp/out" 2>&1; then ok "$name"; else ng "$name"; sed 's/^/    /' "$tmp/out"; fi; }
expect_fail() { local name=$1; shift; if "$@" >"$tmp/out" 2>&1; then ng "$name (should have failed)"; sed 's/^/    /' "$tmp/out"; else ok "$name"; fi; }
expect_fail_with() { # name, expected message (grep -E), command...
    local name=$1 pat=$2; shift 2
    if "$@" >"$tmp/out" 2>&1; then ng "$name (should have failed)"; sed 's/^/    /' "$tmp/out"
    elif grep -Eq "$pat" "$tmp/out"; then ok "$name"
    else ng "$name (failed for another reason)"; sed 's/^/    /' "$tmp/out"; fi
}

git init -q --bare -b main "$tmp/remote.git"
seed=$tmp/seed
git init -q -b main "$seed"
mkdir -p "$seed/Tools/git-hooks" "$seed/Assets/Scripts/Stories"
cp "$src/Tools/agent-sync.sh" "$seed/Tools/"
cp "$src/Tools/git-hooks/pre-commit" "$src/Tools/git-hooks/pre-push" "$seed/Tools/git-hooks/"
cp "$src/.gitattributes" "$seed/"
echo "// story" > "$seed/Assets/Scripts/Stories/MathStory.cs"
echo "meta" > "$seed/Assets/Scripts/Stories/MathStory.cs.meta"
echo "// menu" > "$seed/Assets/Scripts/GameMenus.cs"
echo "meta" > "$seed/Assets/Scripts/GameMenus.cs.meta"
(cd "$seed" && git add -A && git -c user.name=t -c user.email=t@example.invalid commit -q -m init &&
    git remote add origin "$tmp/remote.git" && git push -q origin main)

clone() { # $1 = folder, $2 = agent id
    git clone -q "$tmp/remote.git" "$tmp/$1"
    git -C "$tmp/$1" config user.name "$2"; git -C "$tmp/$1" config user.email "$2@example.invalid"
    git -C "$tmp/$1" config aion.agentId "$2"
    cp "$tmp/$1/Tools/git-hooks/pre-commit" "$tmp/$1/Tools/git-hooks/pre-push" "$tmp/$1/.git/hooks/"
}
clone a pc1-claude
clone b pc2-codex
A() { (cd "$tmp/a" && "$@"); }
B() { (cd "$tmp/b" && "$@"); }

expect_ok   "A claims the math story folder" A bash Tools/agent-sync.sh claim "math story" Assets/Scripts/Stories/
B git pull -q
expect_fail "B check on a claimed file exits 1" B bash Tools/agent-sync.sh check Assets/Scripts/Stories/MathStory.cs
expect_fail "B cannot claim an overlapping file" B bash Tools/agent-sync.sh claim "also story" Assets/Scripts/Stories/MathStory.cs
expect_fail_with "claims compare case-insensitively (Windows)" "BLOCKED" B bash Tools/agent-sync.sh check assets/scripts/stories/MathStory.cs
expect_fail_with "an upper-case .META path maps to the claimed asset" "BLOCKED" B bash Tools/agent-sync.sh check Assets/Scripts/Stories/MathStory.cs.META
B sh -c 'echo "// b edit" >> Assets/Scripts/Stories/MathStory.cs && git add -A'
expect_fail_with "pre-commit blocks B committing A's claimed file" "claimed by another agent" B git commit -q -m "b edits story"
B git reset -q
B git checkout -q -- Assets/Scripts/Stories/MathStory.cs
B sh -c 'echo "// menu edit" >> Assets/Scripts/GameMenus.cs'
expect_ok   "B claims and finishes an unclaimed file" B bash Tools/agent-sync.sh claim "menu" Assets/Scripts/GameMenus.cs
expect_ok   "B finish commits, pushes and drops the claim" B bash Tools/agent-sync.sh finish "Tweak menu" Assets/Scripts/GameMenus.cs
git -C "$tmp/b" cat-file -e origin/main:.agents/claims/pc2-codex.claim 2>/dev/null && ng "B claim gone from origin" || ok "B claim gone from origin"
A sh -c 'echo "// a edit" >> Assets/Scripts/Stories/MathStory.cs'
expect_ok   "A finish rebases on B's push and releases" A bash Tools/agent-sync.sh finish "Extend math story" Assets/Scripts/Stories/MathStory.cs
B git pull -q
expect_ok   "B can claim the story after A finished" B bash Tools/agent-sync.sh claim "story 2" Assets/Scripts/Stories/MathStory.cs
expect_ok   "B release" B bash Tools/agent-sync.sh release

B sh -c 'mkdir -p Assets/Resources/UI && echo x > Assets/Resources/UI/IconStar.png && echo m > Assets/Resources/UI/IconStar.png.meta && git add -f Assets/Resources/UI'
expect_fail_with "pre-commit blocks the paid UI pack" "private files" B git commit -q -m "ui pack"
B git reset -q
B rm -rf Assets/Resources/UI
B sh -c 'echo note > Assets/Readme.txt && git add Assets/Readme.txt'
expect_fail_with "pre-commit blocks an asset without .meta" "missing meta" B git commit -q -m "no meta"
B sh -c 'echo m > Assets/Readme.txt.meta && git add Assets/Readme.txt.meta'
expect_ok   "pre-commit accepts the asset with its .meta" B git commit -q -m "with meta"
expect_ok   "status runs" B bash Tools/agent-sync.sh status

# Review fixes
B sh -c 'mkdir -p Assets/Resources/UI && echo x > "Assets/Resources/UI/별.png" && echo m > "Assets/Resources/UI/별.png.meta" && git add -f Assets/Resources/UI'
expect_fail_with "pre-commit blocks a Korean-named UI pack file" "private files" B git commit -q -m "ui pack ko"
B git reset -q
B rm -rf Assets/Resources/UI
B git rm -q --cached Assets/Readme.txt.meta
expect_fail_with "pre-commit blocks deleting a .meta while keeping its asset" "meta deleted but asset kept" B git commit -q -m "drop meta"
B git reset -q
B sh -c 'echo other > Assets/Other.txt && git mv Assets/Readme.txt.meta Assets/Other.txt.meta && git add Assets/Other.txt'
expect_fail_with "pre-commit blocks moving a kept asset's .meta to another asset" "meta deleted but asset kept" B git commit -q -m "steal meta"
B git reset -q --hard
B rm -f Assets/Other.txt
expect_fail_with "claim refuses while unpushed work commits exist" "unpushed commit" B bash Tools/agent-sync.sh claim "x" Assets/Scripts/GameMenus.cs
expect_ok   "B pushes the pending commit" B git push -q origin main
B sh -c 'echo "// pushed" >> Assets/Scripts/GameMenus.cs && git commit -q -am "menu change" && echo "// not committed" >> Assets/Scripts/GameMenus.cs'
expect_fail_with "pre-push refuses when the working tree differs from the pushed commit" "differ from the commits being pushed" B env -u AION_SKIP_TESTS git push -q origin main
B git checkout -q -- Assets/Scripts/GameMenus.cs
expect_ok   "pre-push passes once the working tree matches" B env -u AION_SKIP_TESTS git push -q origin main

echo "== $pass passed, $fail failed"
[ "$fail" = 0 ]
