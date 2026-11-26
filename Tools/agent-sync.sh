#!/usr/bin/env bash
# agent-sync (AI-ON, ported from surviving-game): git-native coordination for several PCs / agents working on main.
# Needs only git + bash (Git for Windows). Same tool for Claude Code, Codex and humans.
# Rules and usage: AGENTS.md section 5.
#
#   bash Tools/agent-sync.sh status                     fetch; new commits on origin/main, active claims, my inbox
#   bash Tools/agent-sync.sh claim "<task>" <path>...   reserve paths (folder = trailing /), commit + push claim
#   bash Tools/agent-sync.sh heartbeat                  refresh my claim so it does not go stale
#   bash Tools/agent-sync.sh release                    drop my claim, commit + push
#   bash Tools/agent-sync.sh finish "<msg>" <path>...   commit paths AND drop my claim in ONE commit, push once
#   bash Tools/agent-sync.sh finish                     fold the claim drop into my last unpushed commit, push
#   bash Tools/agent-sync.sh editor acquire "<why>" | release | status
#                                                       local (this clone only, no commit) Unity Editor lease
#   bash Tools/agent-sync.sh check <path>...            exit 1 if another agent actively claims a path
#   bash Tools/agent-sync.sh send <agent-id> <message>  leave a message for another agent (commit + push)
#   bash Tools/agent-sync.sh inbox | ack                show / clear my messages
#   bash Tools/agent-sync.sh whoami
# Hook entry points (called by git / Claude Code hooks): hook-session-start, hook-pre-edit,
#   check-staged, check-push
#
# Agent id: env AION_AGENT_ID, else `git config aion.agentId` (set by Tools/setup-git-unity.ps1).
# Tunables: AION_CLAIM_STALE_HOURS (default 8), AION_FETCH_TTL seconds (default 60),
#   AION_SKIP_COMPILE_CHECK=1, AION_SKIP_TESTS=1, ALLOW_FOREIGN_PATHS=1 (override a claim block, coordinate first).
set -u
# Git Bash rewrites args like "origin/main:path" into Windows path lists; keep them literal.
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'
# Print non-ASCII (Korean) paths as-is; git quotes them by default and the path checks would miss them.
_n=${GIT_CONFIG_COUNT:-0}; case "$_n" in ''|*[!0-9]*) _n=0 ;; esac
export "GIT_CONFIG_KEY_$_n=core.quotePath" "GIT_CONFIG_VALUE_$_n=false" GIT_CONFIG_COUNT=$((_n + 1)); unset _n

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || { echo "agent-sync: not inside a git checkout" >&2; exit 2; }
cd "$ROOT" || exit 2
REMOTE=${AION_REMOTE:-origin}
BRANCH=${AION_BRANCH:-main}
REF="$REMOTE/$BRANCH"
CLAIM_DIR=.agents/claims
INBOX_DIR=.agents/inbox
STALE_HOURS=${AION_CLAIM_STALE_HOURS:-8}
FETCH_TTL=${AION_FETCH_TTL:-60}
GITDIR=$(git rev-parse --absolute-git-dir)
ME=${AION_AGENT_ID:-$(git config --get aion.agentId 2>/dev/null || true)}
ZERO=0000000000000000000000000000000000000000

now() { date +%s; }
iso() { date '+%Y-%m-%dT%H:%M:%S%z'; }
warn() { echo "agent-sync: $*" >&2; }
die() { echo "agent-sync: $*" >&2; exit 1; }
age_text() { local s=$1; if [ "$s" -lt 3600 ]; then echo "$((s / 60))m"; else echo "$((s / 3600))h"; fi; }

# ---------------------------------------------------------------- remote state
fetch() { # $1=force to ignore the TTL
    local stamp="$GITDIR/aion-last-fetch" last
    if [ "${1:-}" != force ] && [ -f "$stamp" ]; then
        last=$(cat "$stamp" 2>/dev/null || echo 0)
        [ $(( $(now) - ${last:-0} )) -lt "$FETCH_TTL" ] && return 0
    fi
    if git fetch -q "$REMOTE" "$BRANCH" 2>/dev/null; then
        now > "$stamp"
    else
        warn "fetch failed (offline?); using last known $REF"
    fi
}

ref_exists() { git rev-parse -q --verify "$REF^{commit}" >/dev/null; }

# Claims of REF are loaded into parallel arrays.
C_AGENT=(); C_AGE=(); C_TASK=(); C_PATHS=()
load_claims() {
    C_AGENT=(); C_AGE=(); C_TASK=(); C_PATHS=()
    ref_exists || return 0
    local f agent ct content
    while IFS= read -r f; do
        case "$f" in *.claim) ;; *) continue ;; esac
        agent=${f##*/}; agent=${agent%.claim}
        ct=$(git log -1 --format=%ct "$REF" -- "$f" 2>/dev/null)
        content=$(git show "$REF:$f" 2>/dev/null) || continue
        C_AGENT+=("$agent")
        C_AGE+=($(( $(now) - ${ct:-0} )))
        C_TASK+=("$(printf '%s\n' "$content" | sed -n 's/^task:[[:space:]]*//p' | head -n 1)")
        C_PATHS+=("$(claim_paths_from "$content")")
    done < <(git ls-tree --name-only "$REF" -- "$CLAIM_DIR/" 2>/dev/null)
}

claim_paths_from() { # claim file text -> one path per line
    printf '%s\n' "$1" | sed -n '/^paths:/,$p' | sed '1d' |
        sed 's/^[[:space:]]*-[[:space:]]*//; s/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v '^#' | grep -v '^$'
}

is_stale() { [ "${C_AGE[$1]}" -gt $(( STALE_HOURS * 3600 )) ]; }

# ---------------------------------------------------------------- paths
norm() { # absolute (C:\.., C:/.., /c/..) or relative path -> repo-relative with /
    local p=${1//\\//} r lr lp
    case "$p" in /[a-zA-Z]/*) p="${p:1:1}:${p:2}" ;; esac
    r=$ROOT
    lp=$(printf '%s' "$p" | tr 'A-Z' 'a-z'); lr=$(printf '%s' "$r" | tr 'A-Z' 'a-z')
    case "$lp" in
        "$lr") p="" ;;
        "$lr"/*) p=${p:$(( ${#r} + 1 ))} ;;
        [a-z]:/*) echo "::outside::"; return ;;
    esac
    p=${p#./}
    printf '%s' "$p"
}

overlap() { # symmetric: equal, or one is a folder claim (trailing /) containing the other; case-insensitive (Windows)
    local a=${1,,} b=${2,,}
    [ "$a" = "$b" ] && return 0
    case "$a" in */) case "$b" in "$a"*) return 0 ;; esac ;; esac
    case "$b" in */) case "$a" in "$b"*) return 0 ;; esac ;; esac
    return 1
}

valid_claim_path() {
    case "$1" in ''|.|./|/*|*..*|*\\*|*'*'*|*'?'*|*' '*) return 1 ;; esac
    case "$1" in [A-Za-z]:*) return 1 ;; esac
    return 0
}

# Print conflicts for target paths against other agents' claims.
# Output lines: ACTIVE|STALE <tab> agent <tab> target <tab> claimed-path <tab> age <tab> task
# Returns 1 when at least one ACTIVE conflict exists.
conflicts() {
    local t base i p kind owner hit=0
    for t in "$@"; do
        [ -z "$t" ] && continue
        case "$t" in
            "$INBOX_DIR"/*|PROGRESS.md) continue ;;
            "$CLAIM_DIR"/*.claim)
                owner=${t##*/}; owner=${owner%.claim}
                [ "$owner" = "$ME" ] && continue
                printf 'ACTIVE\t%s\t%s\t%s\t-\t(claim file of another agent)\n' "$owner" "$t" "$t"; hit=1; continue ;;
        esac
        base=${t%.[mM][eE][tT][aA]}
        for i in "${!C_AGENT[@]}"; do
            [ "${C_AGENT[$i]}" = "$ME" ] && continue
            while IFS= read -r p; do
                [ -z "$p" ] && continue
                if overlap "$t" "$p" || overlap "$base" "$p"; then
                    kind=ACTIVE; is_stale "$i" && kind=STALE
                    printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$kind" "${C_AGENT[$i]}" "$t" "$p" "$(age_text "${C_AGE[$i]}")" "${C_TASK[$i]}"
                    [ "$kind" = ACTIVE ] && hit=1
                    break
                fi
            done <<< "${C_PATHS[$i]}"
        done
    done
    return $hit
}

report_conflicts() { # stdin = conflicts output; $1 = context word
    local kind agent t p age task
    while IFS=$'\t' read -r kind agent t p age task; do
        if [ "$kind" = ACTIVE ]; then
            echo "  BLOCKED $t -> claimed by $agent ($p, updated $age ago, task: $task)"
        else
            echo "  stale   $t -> old claim by $agent ($p, $age ago). Ask before taking over: bash Tools/agent-sync.sh send $agent \"...\""
        fi
    done
}

# ---------------------------------------------------------------- git writes
require_me() { [ -n "$ME" ] || die "no agent id. Run Tools/setup-git-unity.ps1 -AgentId <pc>-<tool>-<model> or set AION_AGENT_ID."; }
require_main() {
    local b; b=$(git symbolic-ref --short -q HEAD)
    [ "$b" = "$BRANCH" ] || die "current branch is '${b:-detached}'; this project works on '$BRANCH' only."
    git rev-parse -q --verify MERGE_HEAD >/dev/null && die "merge in progress; finish it first."
    [ -d "$GITDIR/rebase-merge" ] || [ -d "$GITDIR/rebase-apply" ] && die "rebase in progress; finish it first."
    return 0
}

commit_only() { # $1 message, rest = paths (added, modified or deleted)
    local msg=$1; shift
    git add -A -- "$@" || return 1
    git commit -q --only -m "$msg" -m "Agent: $ME" -- "$@"
}

# git pull --rebase --autostash leaves the stash behind (and the working tree without the user's changes)
# when re-applying conflicts, and only says so in a line that is easy to miss. Make that loud and fatal.
safe_pull() {
    local before after
    before=$(git stash list 2>/dev/null | wc -l)
    git pull -q --rebase --autostash "$REMOTE" "$BRANCH" || return 1
    after=$(git stash list 2>/dev/null | wc -l)
    if [ "$after" -gt "$before" ]; then
        echo "agent-sync: !! your uncommitted changes could NOT be re-applied after pulling and are parked in stash@{0}." >&2
        echo "agent-sync: !! run 'git stash pop' now and resolve conflicts (or 'git checkout --theirs' for noise files) before editing more." >&2
        return 2
    fi
    return 0
}

push_with_retry() { # $1 = optional function re-run after each rebase (non-zero aborts)
    local recheck=${1:-} attempt
    for attempt in 1 2 3 4; do
        if git push -q "$REMOTE" "HEAD:$BRANCH"; then now > "$GITDIR/aion-last-fetch"; return 0; fi
        fetch force
        if git merge-base --is-ancestor "$REF" HEAD 2>/dev/null; then
            warn "push failed but $REF is already contained in HEAD (auth/hook failure?). Nothing was rebased."
            return 1
        fi
        warn "push rejected (someone pushed first); rebasing on $REF (attempt $attempt)"
        safe_pull ||
            { warn "rebase stopped on a conflict. Resolve it ('git mergetool --tool=unityyamlmerge' for scenes), 'git rebase --continue', then rerun."; return 1; }
        if [ -n "$recheck" ]; then "$recheck" || return 3; fi
    done
    warn "gave up after 4 attempts"; return 1
}

my_claim_file() { echo "$CLAIM_DIR/$ME.claim"; }

# claim/heartbeat/release/send/ack push HEAD:main, which would also publish unrelated local commits.
require_no_unpushed() {
    ref_exists || return 0
    local n; n=$(git rev-list --count "$REF..HEAD" 2>/dev/null || echo 0)
    [ "$n" -eq 0 ] || die "HEAD has $n unpushed commit(s) that this push would publish too. Push them first (git push), or end the work unit with finish."
}

# ---------------------------------------------------------------- local setup self-check
# git does not install hooks or local config on clone/pull, so every status / pre-commit
# verifies this clone and prints the exact fix. Returns 1 when something is missing.
SETUP_CMD='powershell -NoProfile -ExecutionPolicy Bypass -File Tools/setup-git-unity.ps1 -AgentId <pc>-<tool>-<model>'
setup_problems() {
    local hooks h installed src
    hooks=$(git rev-parse --git-path hooks)
    case "$hooks" in /*|[A-Za-z]:*) ;; *) hooks="$ROOT/$hooks" ;; esac
    [ -z "$ME" ] && echo "agent id not set (git config aion.agentId)"
    for h in pre-commit pre-push; do
        installed="$hooks/$h"; src="$ROOT/Tools/git-hooks/$h"
        [ -f "$src" ] || continue
        if [ ! -f "$installed" ]; then
            echo "git hook '$h' not installed"
        elif ! grep -q "AI-ON $h guard" "$installed"; then
            echo "git hook '$h' is not the project hook (stock/other hook installed)"
        elif [ "$(tr -d '\r' < "$installed")" != "$(tr -d '\r' < "$src")" ]; then
            echo "git hook '$h' is outdated (Tools/git-hooks/$h changed since setup)"
        fi
    done
}

setup_check() { # $1 = prefix for each line
    local problems
    problems=$(setup_problems)
    [ -z "$problems" ] && return 0
    echo "${1:-}!! THIS CLONE IS NOT SET UP FOR MULTI-PC WORK:"
    printf '%s\n' "$problems" | sed "s/^/${1:-}   - /"
    echo "${1:-}   Fix (once per clone, then restart the agent session): $SETUP_CMD"
    return 1
}

# ---------------------------------------------------------------- commands
cmd_status() {
    setup_check
    fetch force
    local behind=0 ahead=0 head
    if ref_exists; then
        behind=$(git rev-list --count "HEAD..$REF" 2>/dev/null || echo 0)
        ahead=$(git rev-list --count "$REF..HEAD" 2>/dev/null || echo 0)
    fi
    head=$(git rev-parse --short "$REF" 2>/dev/null || echo none)
    echo "== agent-sync (me: ${ME:-UNSET}, $REF $head, local is $behind behind / $ahead ahead)"
    if [ "$behind" -gt 0 ]; then
        echo "-- New on $REF (run: git pull --rebase):"
        git log --format='   %h %s (%an, %cr)' "HEAD..$REF" | head -n 12
        echo "   changed paths:"; git diff --name-only "HEAD...$REF" | sed 's/^/     /' | head -n 25
    fi
    load_claims
    if [ "${#C_AGENT[@]}" -eq 0 ]; then
        echo "-- Active claims: none"
    else
        echo "-- Claims (stale after ${STALE_HOURS}h):"
        local i flag
        for i in "${!C_AGENT[@]}"; do
            flag=""; is_stale "$i" && flag=" [STALE]"
            [ "${C_AGENT[$i]}" = "$ME" ] && flag="$flag [ME]"
            echo "   ${C_AGENT[$i]}$flag  updated $(age_text "${C_AGE[$i]}") ago  task: ${C_TASK[$i]}"
            printf '%s\n' "${C_PATHS[$i]}" | sed 's/^/     /'
        done
    fi
    cmd_inbox quiet
    echo "-- Rule: claim once per work unit; finish \"<msg>\" <paths> pushes the last slice and drops the claim in one commit."
}

claim_recheck() {
    load_claims
    local out
    out=$(conflicts "${CLAIM_REQ[@]}") && return 0
    warn "after rebase another agent claimed overlapping paths:"
    printf '%s\n' "$out" | report_conflicts >&2
    undo_last_claim_commit
    return 1
}

undo_last_claim_commit() {
    local f; f=$(my_claim_file)
    if [ "$(git log -1 --format=%s HEAD)" = "agent: claim $ME" ] && [ "$(git diff --name-only HEAD~1 HEAD)" = "$f" ]; then
        git reset -q --soft HEAD~1
        git restore -q --staged -- "$f" 2>/dev/null
        if git cat-file -e "HEAD:$f" 2>/dev/null; then git checkout -q HEAD -- "$f"; else rm -f "$f"; fi
        warn "local claim commit undone; nothing was pushed for it."
    fi
}

cmd_claim() {
    require_me; require_main
    [ $# -ge 2 ] || die 'usage: claim "<task>" <path>... (folders end with /)'
    local task=$1; shift
    local p n
    CLAIM_REQ=()
    for p in "$@"; do
        n=$(norm "$p")
        [ "$n" = "::outside::" ] && die "path outside the repo: $p"
        case "$n" in PROGRESS.md) die "'$n' is a shared status doc; edit it briefly without claiming (pull before edit, push right after)." ;; esac
        valid_claim_path "$n" || die "invalid claim path '$p' (repo-relative, no wildcards/spaces/..; folders end with /)"
        CLAIM_REQ+=("$n")
    done
    fetch force
    safe_pull || die "could not update local main cleanly; resolve and retry."
    require_no_unpushed
    load_claims
    local out
    if ! out=$(conflicts "${CLAIM_REQ[@]}"); then
        echo "agent-sync: cannot claim, overlapping active claims:" >&2
        printf '%s\n' "$out" | report_conflicts >&2
        exit 1
    fi
    [ -n "$out" ] && printf '%s\n' "$out" | report_conflicts >&2

    local f started existing all
    f=$(my_claim_file)
    mkdir -p "$CLAIM_DIR"
    started=$(iso); existing=""
    if [ -f "$f" ]; then
        started=$(sed -n 's/^started:[[:space:]]*//p' "$f" | head -n 1); [ -z "$started" ] && started=$(iso)
        existing=$(claim_paths_from "$(cat "$f")")
    fi
    all=$(printf '%s\n' "$existing" "${CLAIM_REQ[@]}" | grep -v '^$' | awk '!seen[$0]++')
    {
        echo "agent: $ME"
        echo "task: $task"
        echo "host: $(hostname 2>/dev/null || echo unknown)"
        echo "started: $started"
        echo "updated: $(iso)"
        echo "paths:"
        printf '%s\n' "$all" | sed 's/^/- /'
    } > "$f"
    commit_only "agent: claim $ME" "$f" || die "could not commit the claim"
    push_with_retry claim_recheck || exit $?
    echo "agent-sync: claimed for $ME:"; printf '%s\n' "$all" | sed 's/^/  /'
}

cmd_heartbeat() {
    require_me; require_main
    local f; f=$(my_claim_file)
    [ -f "$f" ] || die "no claim to refresh"
    fetch force; require_no_unpushed
    sed -i "s/^updated:.*/updated: $(iso)/" "$f"
    commit_only "agent: heartbeat $ME" "$f" || die "commit failed"
    push_with_retry && echo "agent-sync: claim refreshed"
}

cmd_release() {
    require_me; require_main
    local f; f=$(my_claim_file)
    safe_pull || die "could not update local main cleanly"
    require_no_unpushed
    [ -f "$f" ] || { echo "agent-sync: no claim to release"; return 0; }
    rm -f "$f"
    commit_only "agent: release $ME" "$f" || die "commit failed"
    push_with_retry && {
        echo "agent-sync: claim released"
    }
}

# finish: the last content commit also deletes my claim, so a work unit costs one coordination
# commit (the claim) instead of two. Pre-commit/pre-push read claims from origin, where my claim
# still exists and is mine, so they do not block this commit.
cmd_finish() {
    require_me; require_main
    local f drop=0; f=$(my_claim_file)
    git cat-file -e "HEAD:$f" 2>/dev/null && drop=1   # tracked in HEAD -> this commit must delete it
    if [ "$drop" = 1 ] && { ! git diff --quiet HEAD -- "$f" 2>/dev/null || ! git diff --cached --quiet HEAD -- "$f" 2>/dev/null; }; then
        die "$f has uncommitted changes (an earlier claim/heartbeat did not finish). Commit or restore it first."
    fi
    if [ $# -eq 0 ]; then
        fetch force || die "could not fetch $REF; refusing to amend without knowing what is published"
        if ! ref_exists || git merge-base --is-ancestor HEAD "$REF" 2>/dev/null; then
            warn "HEAD is already on $REF (nothing unpushed to fold into); using release"; cmd_release; return
        fi
        if [ "$drop" = 1 ]; then
            git rm -q --cached -- "$f" && rm -f "$f" &&
                git commit -q --amend --no-edit --only -- "$f" ||
                { git checkout -q HEAD -- "$f" 2>/dev/null; die "could not fold the claim drop into HEAD"; }
        fi
    else
        [ $# -ge 2 ] || die 'usage: finish "<commit message>" <path>...   (or: finish  with no args after committing)'
        local msg=$1; shift
        local paths=("$@")
        if [ "$drop" = 1 ]; then git rm -q --cached -- "$f" && rm -f "$f"; paths+=("$f"); fi
        git add -A -- "$@" || { [ "$drop" = 1 ] && git checkout -q HEAD -- "$f"; die "could not stage the given paths"; }
        git commit -q --only -m "$msg" -m "Agent: $ME" -- "${paths[@]}" ||
            { [ "$drop" = 1 ] && git checkout -q HEAD -- "$f"; die "commit failed (nothing to commit?); claim kept"; }
    fi
    push_with_retry && echo "agent-sync: pushed and claim released"
}

# editor lease: one Unity Editor per project folder (each clone/worktree opens its own Editor), shared
# by every tool working in that folder. Lives in .git (never committed), acquired atomically with
# mkdir. A stale lease (> AION_EDITOR_LEASE_MINUTES, default 90) is reported but only taken with --force.
EDITOR_LEASE="$GITDIR/aion-editor-lease.d"
LEASE_MINUTES=${AION_EDITOR_LEASE_MINUTES:-90}
lease_field() { sed -n "s/^$1:[[:space:]]*//p" "$EDITOR_LEASE/owner" 2>/dev/null; }
cmd_editor() {
    local sub=${1:-status}; [ $# -gt 0 ] && shift
    local owner since age stale=0
    owner=$(lease_field owner); since=$(lease_field since)
    age=$(( $(now) - ${since:-$(now)} ))
    [ "$age" -gt $(( LEASE_MINUTES * 60 )) ] && stale=1
    case "$sub" in
        status)
            if [ ! -d "$EDITOR_LEASE" ]; then echo "editor: free"
            else echo "editor: held by ${owner:-?} for $(age_text "$age")$([ "$stale" = 1 ] && echo ' [STALE]')  why: $(lease_field why)  head: $(lease_field head)"; fi ;;
        acquire)
            require_me
            local why=${1:-unspecified} force=${2:-}
            [ "$why" = "--force" ] && { force=--force; why=${2:-unspecified}; }
            if ! mkdir "$EDITOR_LEASE" 2>/dev/null; then
                if [ "$owner" = "$ME" ]; then echo "editor: already held by $ME"; return 0; fi
                if [ "$force" != "--force" ]; then
                    die "editor is held by ${owner:-?} for $(age_text "$age")$([ "$stale" = 1 ] && echo ' [STALE]') ($(lease_field why)). Wait, ask them to run 'editor release', or 'editor acquire --force \"<why>\"' if they are gone."
                fi
                warn "forcing the editor lease away from ${owner:-?}"
            fi
            printf 'owner: %s\nsince: %s\nwhy: %s\nhead: %s\n' "$ME" "$(now)" "$why" "$(git rev-parse --short HEAD)" > "$EDITOR_LEASE/owner"
            echo "editor: acquired by $ME" ;;
        release)
            require_me
            if [ ! -d "$EDITOR_LEASE" ]; then echo "editor: already free"
            elif [ "$owner" = "$ME" ] || [ "${1:-}" = "--force" ]; then rm -rf "$EDITOR_LEASE"; echo "editor: released"
            else die "editor is held by ${owner:-?}, not $ME (use: editor release --force only if agreed)"; fi ;;
        *) die 'usage: editor acquire "<why>" | acquire --force "<why>" | release [--force] | status' ;;
    esac
}

cmd_check() {
    local p n targets=() fetchmode=""
    [ "${1:-}" = "--no-fetch" ] && { fetchmode=skip; shift; }
    [ -z "$fetchmode" ] && fetch
    for p in "$@"; do n=$(norm "$p"); [ "$n" = "::outside::" ] || targets+=("$n"); done
    [ "${#targets[@]}" -eq 0 ] && return 0
    load_claims
    local out rc=0
    out=$(conflicts "${targets[@]}") || rc=1
    [ -n "$out" ] && printf '%s\n' "$out" | report_conflicts >&2
    return $rc
}

cmd_send() {
    require_me; require_main
    [ $# -ge 2 ] || die "usage: send <agent-id> <message>"
    local to=$1; shift
    case "$to" in *[!A-Za-z0-9._-]*|'') die "invalid agent id: $to" ;; esac
    safe_pull || die "could not update local main cleanly"
    require_no_unpushed
    local f="$INBOX_DIR/$to/$(date +%Y%m%d-%H%M%S)-$ME.md"
    mkdir -p "$INBOX_DIR/$to"
    printf 'from: %s\nsent: %s\n\n%s\n' "$ME" "$(iso)" "$*" > "$f"
    commit_only "agent: message $ME -> $to" "$f" || die "commit failed"
    push_with_retry && echo "agent-sync: message delivered to $to (they see it on their next status/commit)"
}

cmd_inbox() { # $1=quiet: only print when messages exist
    [ -z "$ME" ] && return 0
    local files
    files=$(git ls-tree --name-only "$REF" -- "$INBOX_DIR/$ME/" 2>/dev/null)
    if [ -z "$files" ]; then [ "${1:-}" = quiet ] || echo "agent-sync: inbox empty"; return 0; fi
    echo "-- Inbox for $ME ($(printf '%s\n' "$files" | wc -l | tr -d ' ') message(s); clear with: bash Tools/agent-sync.sh ack):"
    local f
    while IFS= read -r f; do
        echo "   [$f]"; git show "$REF:$f" | sed 's/^/     /'
    done <<< "$files"
}

cmd_ack() {
    require_me; require_main
    safe_pull || die "could not update local main cleanly"
    require_no_unpushed
    [ -d "$INBOX_DIR/$ME" ] || { echo "agent-sync: inbox empty"; return 0; }
    rm -rf "${INBOX_DIR:?}/$ME"
    commit_only "agent: ack inbox $ME" "$INBOX_DIR/$ME" || die "commit failed"
    push_with_retry && echo "agent-sync: inbox cleared"
}

# ---------------------------------------------------------------- hook entry points
hook_session_start() { cmd_status 2>&1; return 0; }

hook_pre_edit() { # Claude Code PreToolUse: JSON on stdin; exit 2 blocks the edit
    local json path n
    json=$(cat)
    path=$(printf '%s' "$json" | grep -o '"\(file_path\|notebook_path\)"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1 |
        sed 's/^[^:]*:[[:space:]]*"//; s/"$//')
    [ -z "$path" ] && return 0
    path=${path//\\\\/\\}
    n=$(norm "$path")
    [ "$n" = "::outside::" ] && return 0
    fetch
    load_claims
    local out
    if ! out=$(conflicts "$n"); then
        {
            echo "agent-sync: edit blocked. Another agent holds an active claim on this path:"
            printf '%s\n' "$out" | report_conflicts
            echo "Do not edit it. Pick another task, or coordinate: bash Tools/agent-sync.sh send <agent> \"<question>\"."
            echo "If the user explicitly told you to override, they can run the edit with ALLOW_FOREIGN_PATHS=1 set."
        } >&2
        [ "${ALLOW_FOREIGN_PATHS:-}" = 1 ] && return 0
        return 2
    fi
    return 0
}

check_staged() { # pre-commit
    setup_check "pre-commit: " >&2  # warn only; the commit itself is not blocked by this
    [ "${ALLOW_FOREIGN_PATHS:-}" = 1 ] && return 0
    local files=()
    mapfile -t files < <(git diff --cached --name-only --no-renames --diff-filter=ACMRD)
    [ "${#files[@]}" -eq 0 ] && return 0
    fetch
    load_claims
    local out
    if ! out=$(conflicts "${files[@]}"); then
        echo "pre-commit: staged paths are claimed by another agent:" >&2
        printf '%s\n' "$out" | report_conflicts >&2
        echo "  Coordinate first (bash Tools/agent-sync.sh status / send), or ALLOW_FOREIGN_PATHS=1 if agreed." >&2
        return 1
    fi
    return 0
}

# Unity Editor for this project: AION_UNITY_EDITOR, else git config aion.unityEditor, else the Hub default.
unity_editor_path() {
    [ -n "${AION_UNITY_EDITOR:-}" ] && { printf '%s' "$AION_UNITY_EDITOR"; return; }
    local v configured
    configured=$(git config --get aion.unityEditor 2>/dev/null)
    [ -n "$configured" ] && { cygpath -u "$configured" 2>/dev/null || printf '%s' "$configured"; return; }
    v=$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' ProjectSettings/ProjectVersion.txt 2>/dev/null | tr -d '\r' | head -n 1)
    printf '%s' "/c/Program Files/Unity/Hub/Editor/$v/Editor/Unity.exe"
}

# No Editor has this project open: compile headless with the existing Library (about 1-3 minutes).
batch_compile() {
    local exe log rc
    exe=$(unity_editor_path)
    [ -f "$exe" ] || { warn "Unity Editor not found at '$exe' (set AION_UNITY_EDITOR); compile NOT checked before push"; return 0; }
    [ -d Library ] || { warn "no Library/ yet (open the project in Unity once); compile NOT checked before push"; return 0; }
    log="$GITDIR/aion-compile.log"
    echo "pre-push: no open Unity Editor; compiling in batch mode (log: $log) ..." >&2
    "$exe" -batchmode -nographics -quit -projectPath "$(pwd -W 2>/dev/null || pwd)" \
        -logFile "$(cygpath -w "$log" 2>/dev/null || printf '%s' "$log")" >/dev/null 2>&1
    rc=$?
    # The Editor may rewrite ProjectSettings files with identical bytes; git status then lists them as
    # modified with an empty diff. A push hook must not touch work files, so only explain it.
    if [ -n "$(git status --porcelain --untracked-files=no -- ProjectSettings 2>/dev/null)" ] &&
        git diff --quiet -- ProjectSettings 2>/dev/null; then
        echo "pre-push: note: Unity rewrote ProjectSettings files without changing them (git status shows M, diff is empty)." >&2
        echo "  Harmless. To clear it when no Editor is open: git checkout -- ProjectSettings" >&2
    fi
    if grep -q 'error CS[0-9]' "$log" 2>/dev/null; then
        echo "pre-push: Unity batch compile FAILED:" >&2
        grep 'error CS[0-9]' "$log" | sed 's/^/  /' | sort -u | head -n 20 >&2
        echo "  Fix it before pushing (AION_SKIP_COMPILE_CHECK=1 only if intended)." >&2
        return 1
    fi
    if [ "$rc" -ne 0 ]; then
        warn "Unity batch mode exited $rc without compiler errors (license? project open elsewhere?); compile NOT checked. See $log"
        return 0
    fi
    echo "pre-push: Unity batch compile OK" >&2
    return 0
}

compile_gate() {
    [ "${AION_SKIP_COMPILE_CHECK:-}" = 1 ] && { warn "compile check skipped (AION_SKIP_COMPILE_CHECK=1)"; return 0; }
    local proj out reachable=0
    proj=$(pwd -W 2>/dev/null || pwd)
    if command -v unity >/dev/null 2>&1; then
        out=$(unity command eval --project-path "$proj" --format json --result-only -- --code \
            'UnityEditor.AssetDatabase.Refresh(); return "compiling=" + UnityEditor.EditorApplication.isCompiling + "|failed=" + UnityEditor.EditorUtility.scriptCompilationFailed;' 2>&1)
        case "$out" in *compiling=*) reachable=1 ;; esac   # --result-only prints the bare result, no "success" field
    fi
    if [ "$reachable" = 0 ]; then
        if [ -f Temp/UnityLockfile ]; then
            warn "a Unity Editor seems to have this project open but the Unity CLI cannot reach it; compile NOT checked before push (check the Console for errors)"
            return 0
        fi
        batch_compile; return $?
    fi
    # Refresh() above usually starts a compile; wait for it instead of rejecting the push (max ~3 min).
    local tries=0
    while case "$out" in *compiling=True*) true ;; *) false ;; esac && [ "$tries" -lt 36 ]; do
        tries=$((tries + 1)); sleep 5
        out=$(unity command eval --project-path "$proj" --format json --result-only -- --code \
            'return "compiling=" + UnityEditor.EditorApplication.isCompiling + "|failed=" + UnityEditor.EditorUtility.scriptCompilationFailed;' 2>&1)
    done
    case "$out" in
        *failed=True*) echo "pre-push: Unity reports script compilation FAILED. Fix it before pushing (AION_SKIP_COMPILE_CHECK=1 only if intended)." >&2; return 1 ;;
        *compiling=True*) echo "pre-push: Unity is still compiling after 3 minutes. Push again when it finishes." >&2; return 1 ;;
        *compiling=False*failed=False*) ;;
        *) warn "Unity Editor stopped answering while compiling; compile NOT checked before push"; return 0 ;;
    esac
    echo "pre-push: Unity compile OK" >&2
    return 0
}

# PowerShell regression tests (Tests/*.ps1); every one must exit 0.
tests_gate() {
    [ "${AION_SKIP_TESTS:-}" = 1 ] && { warn "regression tests skipped (AION_SKIP_TESTS=1)"; return 0; }
    local pw="" c t failed=0 log="$GITDIR/aion-test.log"
    # Prefer the regular PowerShell 7 install over the Microsoft Store (MSIX) one.
    for c in "${LOCALAPPDATA:-}/Programs/PowerShell/7/pwsh.exe" "/c/Program Files/PowerShell/7/pwsh.exe"; do
        [ -f "$c" ] && { pw=$c; break; }
    done
    [ -z "$pw" ] && command -v pwsh >/dev/null 2>&1 && pw=pwsh
    [ -z "$pw" ] && { warn "PowerShell 7 not found; regression tests NOT run before push"; return 0; }
    local managed args
    managed="$(dirname "$(unity_editor_path)")/Data/Managed/UnityEngine"
    for t in Tests/*.ps1; do
        [ -f "$t" ] || continue
        args=()
        if [ -d "$managed" ] && grep -q 'UnityManagedPath' "$t"; then
            args=(-UnityManagedPath "$(cygpath -w "$managed" 2>/dev/null || printf '%s' "$managed")")
        fi
        if "$pw" -NoProfile -File "$t" "${args[@]}" > "$log" 2>&1; then
            echo "pre-push: ok   $t" >&2
        else
            echo "pre-push: FAIL $t" >&2; tail -n 15 "$log" | sed 's/^/  /' >&2; failed=1
        fi
    done
    [ "$failed" = 0 ] || echo "pre-push: fix the failing tests before pushing (AION_SKIP_TESTS=1 only if intended)." >&2
    return $failed
}

check_push() { # pre-push: stdin = "<local ref> <local sha> <remote ref> <remote sha>"
    local lref lsha rref rsha files=() shas=() base code=0
    while read -r lref lsha rref rsha; do
        [ -z "${lsha:-}" ] && continue
        [ "$lsha" = "$ZERO" ] && continue
        if [ "$rsha" = "$ZERO" ] || ! git cat-file -e "$rsha^{commit}" 2>/dev/null; then
            base=$(git rev-parse -q --verify "$REF" 2>/dev/null) || continue
        else
            base=$rsha
        fi
        mapfile -t -O "${#files[@]}" files < <(git diff --name-only --no-renames "$base" "$lsha")
        shas+=("$lsha")
    done
    [ "${#files[@]}" -eq 0 ] && return 0
    if [ "${ALLOW_FOREIGN_PATHS:-}" != 1 ]; then
        fetch force
        load_claims
        local out
        if ! out=$(conflicts "${files[@]}"); then
            echo "pre-push: pushed commits change paths another agent claims:" >&2
            printf '%s\n' "$out" | report_conflicts >&2
            echo "  Coordinate first, or ALLOW_FOREIGN_PATHS=1 git push if agreed." >&2
            return 1
        fi
    fi
    if printf '%s\n' "${files[@]}" | grep -Eq '\.(cs|asmdef|asmref)$|^Tests/.*\.ps1$|^Packages/(manifest|packages-lock)\.json$' &&
        { [ "${AION_SKIP_TESTS:-}" != 1 ] || [ "${AION_SKIP_COMPILE_CHECK:-}" != 1 ]; }; then
        # Tests and the compile run on the working tree, so it must match the commits being pushed.
        local s dirty
        local inputs=('*.cs' '*.asmdef' '*.asmref' 'Tests/*.ps1' 'Packages/manifest.json' 'Packages/packages-lock.json')
        dirty=$( { for s in "${shas[@]}"; do git diff --name-only "$s" -- "${inputs[@]}"; done
                   git ls-files --others --exclude-standard -- "${inputs[@]}"; } | sort -u)
        if [ -n "$dirty" ]; then
            echo "pre-push: these files differ from the commits being pushed (uncommitted or untracked):" >&2
            printf '%s\n' "$dirty" | sed 's/^/  /' >&2
            echo "  The checks run on the working tree, so commit or stash them first, then push again." >&2
            return 1
        fi
        tests_gate || code=1
    fi
    if printf '%s\n' "${files[@]}" | grep -Eq '\.(cs|asmdef|asmref)$|^Packages/(manifest|packages-lock)\.json$'; then
        compile_gate || code=1
    fi
    return $code
}

# ---------------------------------------------------------------- main
cmd=${1:-status}; [ $# -gt 0 ] && shift
case "$cmd" in
    status) cmd_status ;;
    claim) cmd_claim "$@" ;;
    heartbeat) cmd_heartbeat ;;
    release) cmd_release ;;
    finish) cmd_finish "$@" ;;
    editor) cmd_editor "$@" ;;
    check) cmd_check "$@" ;;
    send) cmd_send "$@" ;;
    inbox) fetch; cmd_inbox ;;
    ack) cmd_ack ;;
    whoami) echo "${ME:-UNSET}" ;;
    hook-session-start) hook_session_start ;;
    hook-pre-edit) hook_pre_edit ;;
    check-staged) check_staged ;;
    check-push) check_push "$@" ;;
    -h|--help|help) sed -n '2,26p' "$0" ;;
    *) die "unknown command '$cmd' (try: help)" ;;
esac
