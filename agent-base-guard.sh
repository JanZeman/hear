#!/usr/bin/env bash
set -euo pipefail

# agent-base-guard.sh - Verify agent documentation system health.
#
# Usage (from the target repo root):
#   bash agent-base-guard.sh
#
# This script checks that the agent documentation system is correctly set up:
# - Required files exist
# - Adapters reference AGENTS.md (not independent rulebooks)
# - No feature docs have leaked outside their Product
#
# Customize the required files list for your project.

# --- Colors and messaging ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
DIM='\033[2m'
NC='\033[0m'

info()    { echo -e "${BLUE}$1${NC}"; }
success() { echo -e "${GREEN}✔ $1${NC}"; }
warn()    { echo -e "${YELLOW}⚠ $1${NC}"; }
error()   { echo -e "${RED}✘ $1${NC}"; }
step()    { echo -e "\n${BOLD}${BLUE}--- $1 ---${NC}"; }

# --- Banner (shown once per session) ---
print_banner() {
  [[ -n "${AGENT_BASE_BANNER_SHOWN:-}" ]] && return
  export AGENT_BASE_BANNER_SHOWN=1
  local agent_base_version="unknown"
  if [[ -f "$ROOT_DIR/AGENT_BASE_VERSION" ]]; then
    agent_base_version="$(head -1 "$ROOT_DIR/AGENT_BASE_VERSION" | tr -d '[:space:]')"
  fi
  local banner_file="$ROOT_DIR/.agents/agent-base/banner.txt"
  local line
  printf "\n${BLUE}"
  if [[ -f "$banner_file" ]]; then
    while IFS= read -r line || [[ -n "$line" ]]; do
      printf '%s\n' "${line//<version>/$agent_base_version}"
    done < "$banner_file"
  else
    printf 'AGENT-BASE v %s\n' "$agent_base_version"
  fi
  printf "${NC}\n"
}

# --- Path resolution ---
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  echo "Usage: bash agent-base-guard.sh"
  echo ""
  echo "Verifies agent documentation system health: required files, adapter references,"
  echo "and agent-base sync."
  echo ""
  echo "Run this at the start of every agent session."
  exit 0
fi

# AGENT-BASE-CONTEXT-METER:START
# Measure the actual session-start payload before printing it, so the report can appear first.
# Every vendor whose own adapter is natively read by its tool (claude/CLAUDE.md, warp/WARP.md,
# copilot/.github/copilot-instructions.md) also gets AGENTS.md forced into context below via
# AGENT-BASE-AGENTS-MD-PRINT, so all three need measuring here. Codex reads AGENTS.md natively
# instead and skips that forced print, but still gets measured (context-budget.py's own vendor
# branch treats it as the "AGENTS.md is native" case). An unset/unrecognized vendor is left
# unmeasured on purpose: there is no known native adapter file to attribute it to, and that path
# usually means the guard was invoked manually, outside any vendor's real session context.
if [[ "${AGENT_BASE_CONTEXT_CAPTURED:-}" != "1" \
  && ( "${AGENT_BASE_VENDOR:-}" == "claude" || "${AGENT_BASE_VENDOR:-}" == "codex" \
       || "${AGENT_BASE_VENDOR:-}" == "warp" || "${AGENT_BASE_VENDOR:-}" == "copilot" ) \
  && -f "$ROOT_DIR/scripts/context-budget.py" ]]; then
  _context_output="$(mktemp "${TMPDIR:-/tmp}/agent-base-context.XXXXXX")"
  set +e
  AGENT_BASE_CONTEXT_CAPTURED=1 bash "$0" "$@" >"$_context_output" 2>&1
  _context_status=$?
  set -e
  python3 "$ROOT_DIR/scripts/context-budget.py" --root "$ROOT_DIR" \
    --vendor "$AGENT_BASE_VENDOR" --guard-output "$_context_output" || true
  cat "$_context_output"
  rm -f "$_context_output"
  exit "$_context_status"
fi
# AGENT-BASE-CONTEXT-METER:END

print_banner

exit_code=0

fail() {
  error "$1"
  exit_code=1
}

assert_file_exists() {
  local file_path="$1"
  if [[ ! -f "$file_path" ]]; then
    fail "Missing file: $file_path"
  fi
}

assert_contains() {
  local file_path="$1"
  local pattern="$2"
  if [[ -f "$file_path" ]] && ! grep -Fq "$pattern" "$file_path"; then
    fail "Expected '$pattern' in $file_path"
  fi
}

assert_not_contains() {
  local file_path="$1"
  local pattern="$2"
  if [[ -f "$file_path" ]] && grep -Fq "$pattern" "$file_path"; then
    fail "Unexpected legacy content '$pattern' in $file_path"
  fi
}

# AGENT-BASE-UPDATE-CONSENT:START
# --- Agent-base update reminder (never fetches, syncs, or changes a checkout) ---
# Cache: remind at most once per day. Delete .agents/.agent-base-last-run to show it again.
AGENT_BASE_CACHE="$ROOT_DIR/.agents/.agent-base-last-run"
AGENT_BASE_TTL=86400  # seconds (24 h)

_stale=true
if [[ -f "$AGENT_BASE_CACHE" ]]; then
  _last=$(cat "$AGENT_BASE_CACHE" 2>/dev/null || echo 0)
  _now=$(date +%s)
  (( _now - _last < AGENT_BASE_TTL )) && _stale=false
fi

if [[ "$_stale" == true ]] && [[ -d "_sub/agent-base" && -f "_sub/agent-base/apply-agent-base.sh" ]]; then
  warn "Agent-base update check is due. Automatic updates are disabled."
  echo -e "${DIM}  Ask the current user before acting. Run only after explicit approval: Update AB.${NC}"
elif [[ "$_stale" == false ]]; then
  echo -e "${DIM}Agent-base update reminder: cached (next after $(date -r $(( _last + AGENT_BASE_TTL )) '+%Y-%m-%d %H:%M' 2>/dev/null || echo '~24h'))${NC}"
fi
# AGENT-BASE-UPDATE-CONSENT:END

# AGENT-BASE-AUTONOMY-CHECK:START
# --- Machine/IDE autonomy advisory (read-only, never blocks the guard) ---
# Machine fixes go through consent-gated vendor.sh with timestamped backups. Manual-only IDE/UI
# findings are handed off as exact user steps; undocumented editor state is never rewritten.
if [[ -f "scripts/agent-autonomy.py" ]]; then
  python3 scripts/agent-autonomy.py check --vendor all --repo "$ROOT_DIR" || \
    warn "Agent autonomy diagnostics could not run; use: python3 scripts/agent-autonomy.py status --vendor all --repo ."
fi
# AGENT-BASE-AUTONOMY-CHECK:END
# AGENT-BASE-TOPIC-QUEUE-CHECK:START
# --- Session topic queue advisory (read-only, never blocks the guard) ---
# Agents decide whether topics are truly related; this only keeps the durable queue structure
# visible when a handoff exists. Missing headings are a problem worth surfacing, not a reason to
# reject a project or print normal-success noise.
if [[ -d ".agents/sessions" ]]; then
  _missing_topic_queue=false
  shopt -s nullglob
  for _session_handoff in .agents/sessions/*.md; do
    if ! grep -Fqx '## Active topic' "$_session_handoff" \
      || ! grep -Fqx '## Queued topics' "$_session_handoff"; then
      _missing_topic_queue=true
      break
    fi
  done
  shopt -u nullglob
  if [[ "$_missing_topic_queue" == true ]]; then
    warn "A session handoff lacks Active topic or Queued topics headings."
    echo -e "${DIM}  Keep one active topic (two only when dependent); queue the rest or promote${NC}"
    echo -e "${DIM}  broad independent work to a roadmap item. See AB-SESSION-001.${NC}"
  fi
fi
# AGENT-BASE-TOPIC-QUEUE-CHECK:END
# AGENT-BASE-VENTURE-PREPARE:START
# --- Venture Summary refresh and periodic review signals (read-only except managed/local state) ---
_venture_helper="scripts/venture.py"
_venture_wiki_due=false
_venture_review_due=false
if [[ -f "$_venture_helper" ]]; then
  _venture_refresh="$(python3 "$_venture_helper" --root "$ROOT_DIR" refresh-summary 2>&1)" ||
    warn "Venture Summary could not be refreshed: $_venture_refresh"
  if python3 "$_venture_helper" --root "$ROOT_DIR" wiki-config >/dev/null 2>&1 \
    && [[ "$(python3 "$_venture_helper" --root "$ROOT_DIR" check-due 2>/dev/null || true)" == "due" ]]; then
    _venture_wiki_due=true
  fi
  if python3 "$_venture_helper" --root "$ROOT_DIR" context >/dev/null 2>&1 \
    && [[ "$(python3 "$_venture_helper" --root "$ROOT_DIR" check-due --kind review 2>/dev/null || true)" == "due" ]]; then
    _venture_review_due=true
  fi
fi
# AGENT-BASE-VENTURE-PREPARE:END
# AGENT-BASE-QUALITY:START
# --- AB quality conditions (standards/ab-quality.md). Mechanical only; judged ones need the
# ab-quality-check skill and a present human. Every finding here is deferrable by design. ---
# Defined inside the managed block on purpose. The guard is customize-once, so a helper added to
# its messaging section would reach new repositories only; an existing one would receive this
# block and fail on a missing function. A managed block carries its own dependencies.
#
# Severity between warn and fail. Deliberately does not touch exit_code: the guard prints
# AGENTS.md and the Venture Context after its result, so a non-zero exit strips the agent's own
# rules and decision context for the whole session. That is right for a broken repository and
# wrong for a quality finding, which the human must stay free to defer.
strong() { echo -e "\n${BOLD}${YELLOW}! $1${NC}"; }
_q_findings=""
_q_add() { _q_findings="${_q_findings}    - $1"$'\n'; }

# Sections are read through scripts/venture.py, the same extractor that renders them, rather
# than re-parsed here. A shell strip of "<!--" only truncates the line that opens a comment, so a
# multi-line comment counted as content and this block called a correct teaser over budget while
# the renderer disagreed. Two extractors are two chances to drift; there is now one.
if [[ -f ".agents/VENTURE.md" && -f "scripts/venture.py" ]]; then
  _q_identity="$(python3 scripts/venture.py --root "$ROOT_DIR" section Identity 2>/dev/null || true)"
  if [[ -z "$_q_identity" ]]; then
    if grep -Fqx '## Summary' ".agents/VENTURE.md"; then
      _q_add "AB-Q001: .agents/VENTURE.md still has the retired ## Summary teaser. Rename it to ## Identity and keep only the clause saying what this Venture is; the rest was Strategy, which is now rendered from its own section."
    else
      _q_add "AB-Q001: .agents/VENTURE.md has no ## Identity section; Purpose is standing in."
    fi
  else
    _q_words="$(printf '%s' "$_q_identity" | wc -w | tr -d ' ')"
    # Read from the renderer, not restated here, for the same reason as the extraction above.
    # This is authoring guidance only: the renderer always preserves the complete Identity.
    _q_recommended_max="$(python3 scripts/venture.py --root "$ROOT_DIR" identity-recommended-max 2>/dev/null || echo 0)"
    if (( _q_recommended_max > 0 && _q_words > _q_recommended_max )); then
      _q_add "AB-Q002: ## Identity is $_q_words words; prefer no more than $_q_recommended_max, but the complete Identity remains in session context. Shorten the source when practical; this can wait."
    fi
    _q_purpose="$(python3 scripts/venture.py --root "$ROOT_DIR" section Purpose 2>/dev/null || true)"
    if [[ -n "$_q_purpose" && "$_q_identity" == "$_q_purpose" ]]; then
      _q_add "AB-Q003: ## Identity only repeats ## Purpose. Identity says what this Venture is; Purpose says why it exists."
    fi
  fi
fi

if [[ -d .agents/requests ]]; then
  _q_catalog=".agents/requests/_REQUESTS_CATALOG.md"
  for _q_f in .agents/requests/*.md; do
    [[ -f "$_q_f" ]] || continue
    _q_name="$(basename "$_q_f")"
    [[ "$_q_name" == _* ]] && continue
    if grep -qE '^\*\*Status\*\*: (Released|Refused|Downstream-only)[[:space:]]*$' "$_q_f" &&
      ! grep -qE '^\*\*Blocks\*\*:[[:space:]]*\S' "$_q_f"; then
      _q_add "AB-Q004: $_q_f is answered but has no Blocks value; nothing says what to resume."
    fi
    # The catalog is the human-readable index. A request missing from it is invisible to anyone
    # who reads the index instead of listing the directory, which is what the index is for.
    # Only a table row counts. A bare grep also matched the name inside prose or a comment,
    # which would let the index look complete while listing nothing.
    if [[ -f "$_q_catalog" ]] && ! grep '^|' "$_q_catalog" | grep -Fq "$_q_name"; then
      _q_add "AB-Q005: $_q_name has no row in _REQUESTS_CATALOG.md; the index and the files disagree."
    fi
  done
fi

# AB-Q006: unconditional, per the human's explicit direction. Every Venture, whether or not an
# operator-local wiki registry even exists, must have a wiki mapping to be venture
# compatible. Structure only, never content: this reads scripts/venture.py's own error, which
# names the missing piece (no registry, no repository entry, no wiki mapping, or a malformed
# connector/root) without this guard knowing or naming any actual wiki host or path (GEN-001).
if [[ -f "scripts/venture.py" ]]; then
  _q_wiki_error="$(python3 scripts/venture.py --root "$ROOT_DIR" wiki-config 2>&1 >/dev/null || true)"
  if [[ -n "$_q_wiki_error" ]]; then
    _q_add "AB-Q006: ${_q_wiki_error#Venture: }. Configure a wiki mapping; see venture-wiki-sync."
  fi
fi

if [[ -n "$_q_findings" ]]; then
  strong "AB quality: $(printf '%s' "$_q_findings" | grep -c '^') condition(s) below standard."
  printf '%s' "$_q_findings"
  echo -e "${DIM}  Fix these when you can; you may defer them and continue this task now.${NC}"
  echo -e "${DIM}  Details and severities: .agents/agent-base/standards/ab-quality.md${NC}"
  echo -e "${DIM}  For the judged conditions too, say \"AB check\" (AB-QUALITY-001).${NC}"
  echo -e "${DIM}  During an agent-base update, only report these: they need a present human.${NC}"
fi
# AGENT-BASE-QUALITY:END

# AGENT-BASE-REQUESTS:START
# --- Requests to agent-base: state enum, plus the two reminders that keep the thread alive ---
# A request usually interrupts something here, so two moments can go cold silently: the answer
# never coming back, and the answer arriving while the interrupted work stays abandoned. Both get
# one line. Accepted is deliberately silent: it is answered but not yet deliverable, and repeating
# it for however many weeks the upstream release takes would train the reader to skip this block.
if [[ -d .agents/requests ]]; then
  _req_waiting=0
  _req_answered=0
  _req_resume_names=""
  for _req_f in .agents/requests/*.md; do
    [[ -f "$_req_f" ]] || continue
    _req_base="$(basename "$_req_f")"
    [[ "$_req_base" == _* ]] && continue
    _req_line="$(grep -m1 '^\*\*Status\*\*:' "$_req_f" || true)"
    [[ -z "$_req_line" ]] && continue
    _req_status="$(echo "$_req_line" | sed -E 's/^\*\*Status\*\*: *//; s/[[:space:]]+$//')"
    case "$_req_status" in
      New|Collected) _req_waiting=$(( _req_waiting + 1 )) ;;
      Released|Refused|Downstream-only)
        # "-" is the template's own value for a request that interrupted nothing. Counting it
        # here produced a nudge that told the reader work was suspended and pointed them at a
        # Blocks field saying nothing, forever, because only Resumed would clear it.
        if grep -qE '^\*\*Blocks\*\*:[[:space:]]*-[[:space:]]*$' "$_req_f"; then
          :
        else
          _req_answered=$(( _req_answered + 1 ))
          _req_resume_names="${_req_resume_names}    - $_req_base ($_req_status)"$'\n'
        fi
        ;;
      Accepted|Resumed) ;;
      *)
        fail "$_req_f has Status: '$_req_status' -- must be one of New|Collected|Accepted|Refused|Downstream-only|Released|Resumed. This project sets New and Resumed; agent-base writes the rest."
        ;;
    esac
  done
  if [[ $_req_waiting -gt 0 ]]; then
    echo ""
    info "$_req_waiting agent-base request(s) still waiting for an answer."
    echo -e "${DIM}  They travel upstream at the next sync or fleet update. Nothing to do here.${NC}"
  fi
  if [[ $_req_answered -gt 0 ]]; then
    echo ""
    info "$_req_answered agent-base request(s) answered, with work here still suspended:"
    printf '%s' "$_req_resume_names"
    echo -e "${DIM}  Read each one's Blocks field, resume or deliberately drop what it names, then${NC}"
    echo -e "${DIM}  set Status: Resumed. See .agents/requests/_REQUESTS_CATALOG.md.${NC}"
  fi
fi
# AGENT-BASE-REQUESTS:END

# AGENT-BASE-WORKFLOW-CHECK:START
# --- Trunk-based + worktree workflow compliance (read-only, never blocks the guard) ---
# .agents/standards/development-workflow.md defines a trunk-based model (only `main`, no
# dev/develop), a two-letter `repo_id`, the `<REPO_ID>` main-worktree folder rule, and the
# `<REPO_ID><N>` / `_<repo_id><N>` slot convention. worktrees.sh hard-fails on the folder and
# branch rules; this block only surfaces them once per session, loudly, in red. It must never
# touch exit_code: a non-zero guard strips the session's rules and Venture Context, the wrong
# outcome for a repo that has simply not finished migrating.
_wf_file=".agents/standards/development-workflow.md"
_wf_problems=""
# 1. Trunk-based: no dev/develop branch, local or on origin.
for _wf_ref in refs/heads/dev refs/heads/develop refs/remotes/origin/dev refs/remotes/origin/develop; do
  if git show-ref --verify --quiet "$_wf_ref" 2>/dev/null; then
    _wf_problems="${_wf_problems}  - A '${_wf_ref##*/}' branch exists (${_wf_ref}). This project is trunk-based: only 'main'. A human promotes it into the trunk and deletes it.\n"
  fi
done
if [[ -f "$_wf_file" ]]; then
  # Pull the repo_id cell from its table row: | `repo_id` | `xx` | ... | -- || true keeps a
  # missing row (no match, head closes the pipe early) from tripping the guard's set -e.
  _repo_id_value="$({ grep -E '^\|[[:space:]]*`repo_id`[[:space:]]*\|' "$_wf_file" \
    | head -1 \
    | sed -E 's/^\|[[:space:]]*`repo_id`[[:space:]]*\|[[:space:]]*`?([^`|]*)`?[[:space:]]*\|.*/\1/' \
    | tr -d '[:space:]'; } 2>/dev/null || true)"
  if [[ -z "$_repo_id_value" || "$_repo_id_value" == "TODO" ]]; then
    _wf_problems="${_wf_problems}  - repo_id is unset in $_wf_file. Worktree tooling will refuse to run until it is two lowercase letters.\n"
  elif ! [[ "$_repo_id_value" =~ ^[a-z]{2}$ ]]; then
    _wf_problems="${_wf_problems}  - repo_id '$_repo_id_value' in $_wf_file is not two lowercase letters (^[a-z]{2}\$).\n"
  else
    _wf_stable_branch="$({ grep -E '^\|[[:space:]]*`stable_branch`[[:space:]]*\|' "$_wf_file" \
      | head -1 \
      | sed -E 's/^\|[[:space:]]*`stable_branch`[[:space:]]*\|[[:space:]]*`?([^`|]*)`?[[:space:]]*\|.*/\1/' \
      | tr -d '[:space:]'; } 2>/dev/null || true)"
    [[ -n "$_wf_stable_branch" && "$_wf_stable_branch" != "TODO" ]] || _wf_stable_branch="main"
    _wf_upper="$(printf '%s' "$_repo_id_value" | tr '[:lower:]' '[:upper:]')"
    if grep -Fqx 'git worktree add -b _xx1 ../XX1 main' "$_wf_file" \
      || grep -Fqx 'git worktree add -b _xx2 ../XX2 main' "$_wf_file"; then
      _wf_problems="${_wf_problems}  - The literal worktree-slot scaffold remains in $_wf_file. Fill repo_id, rerun Agent Base to materialize the default two slots, or declare this project's slots manually.\n"
    else
      _wf_slot_count=0
      _wf_slot_numbers=""
      while IFS= read -r _wf_slot_line; do
        [[ -n "$_wf_slot_line" ]] || continue
        if ! [[ "$_wf_slot_line" =~ ^git[[:space:]]+worktree[[:space:]]+add[[:space:]]+-b[[:space:]]+([^[:space:]]+)[[:space:]]+([^[:space:]]+)[[:space:]]+([^[:space:]]+)$ ]]; then
          _wf_problems="${_wf_problems}  - Worktree setup command has an unsupported shape: $_wf_slot_line\n"
          continue
        fi
        _wf_slot_count=$((_wf_slot_count + 1))
        _wf_slot_branch="${BASH_REMATCH[1]}"
        _wf_slot_path="${BASH_REMATCH[2]}"
        _wf_slot_base="${BASH_REMATCH[3]}"
        if [[ "$_wf_slot_branch" =~ ^_${_repo_id_value}([1-9][0-9]*)$ ]]; then
          _wf_slot_number="${BASH_REMATCH[1]}"
          _wf_slot_numbers="$_wf_slot_numbers $_wf_slot_number"
          _wf_slot_want_path="../${_wf_upper}${_wf_slot_number}"
          if [[ "$_wf_slot_path" != "$_wf_slot_want_path" ]]; then
            _wf_problems="${_wf_problems}  - Slot $_wf_slot_number has sibling path '$_wf_slot_path', expected '$_wf_slot_want_path'.\n"
          fi
        else
          _wf_problems="${_wf_problems}  - Worktree branch '$_wf_slot_branch' does not match '_${_repo_id_value}<N>'.\n"
        fi
        if [[ "$_wf_slot_base" != "$_wf_stable_branch" ]]; then
          _wf_problems="${_wf_problems}  - Worktree base '$_wf_slot_base' does not match stable_branch '$_wf_stable_branch'.\n"
        fi
      done < <(grep -E '^git[[:space:]]+worktree[[:space:]]+add[[:space:]]+' "$_wf_file" 2>/dev/null || true)
      if [[ "$_wf_slot_count" -eq 0 ]]; then
        _wf_problems="${_wf_problems}  - No direct worktree slot commands are declared in $_wf_file.\n"
      else
        for ((_wf_expected_slot = 1; _wf_expected_slot <= _wf_slot_count; _wf_expected_slot++)); do
          [[ " $_wf_slot_numbers " == *" $_wf_expected_slot "* ]] ||
            _wf_problems="${_wf_problems}  - Worktree slot numbering is missing $_wf_expected_slot for repo_id '$_repo_id_value'.\n"
        done
      fi
    fi
    # 2. Main worktree folder must be the uppercase repo_id. Only checkable in the main worktree.
    _wf_top="$(git rev-parse --show-toplevel 2>/dev/null || true)"
    _wf_gd="$(git rev-parse --path-format=absolute --git-dir 2>/dev/null || true)"
    _wf_cd="$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
    if [[ -n "$_wf_top" && -n "$_wf_gd" && "$_wf_gd" == "$_wf_cd" ]]; then
      _wf_want="$_wf_upper"
      if [[ "${_wf_top##*/}" != "$_wf_want" ]]; then
        _wf_problems="${_wf_problems}  - Main worktree folder is '${_wf_top##*/}', not '$_wf_want'. Clone this repository into a folder named '$_wf_want'.\n"
      fi
    fi
  fi
fi
if [[ -n "$_wf_problems" ]]; then
  echo ""
  echo -e "${RED}${BOLD}=== DEVELOPMENT WORKFLOW: NOT COMPLIANT ===${NC}"
  printf "${RED}%b${NC}" "$_wf_problems"
  echo -e "${DIM}  Standard: $_wf_file. Branch deletion and folder moves are manual, human-only (AB-GIT-001).${NC}"
fi
# AGENT-BASE-WORKFLOW-CHECK:END

# --- Required files ---
assert_file_exists "AGENTS.md"
assert_file_exists "README.md"
assert_file_exists "AGENT_BASE_VERSION"
assert_file_exists ".agents/agent-base/banner.txt"
assert_file_exists ".agents/roadmap/_ROADMAP_CATALOG.md"
assert_file_exists ".markdownlint.jsonc"

# --- Agent-base-owned managed rules must stay in the AB-* namespace ---
# Only inspect declarations inside the managed block. Unprefixed declarations elsewhere are
# project-owned and intentionally outside this check, even when their suffix matches an AB
# default. This catches agent-base regressing into the real downstream collision that led to
# v0.0.97 without claiming to infer project semantics.
if grep -Fqx '<!-- AGENT-BASE-RULES:START -->' "AGENTS.md"; then
  while IFS= read -r _managed_rule_id; do
    [[ -z "$_managed_rule_id" ]] && continue
    if [[ "$_managed_rule_id" != AB-* ]]; then
      fail "Managed agent-base rule '$_managed_rule_id' must use an AB-* ID."
    fi
  done < <(awk '
    $0 == "<!-- AGENT-BASE-RULES:START -->" { inside=1; next }
    $0 == "<!-- AGENT-BASE-RULES:END -->"   { inside=0; next }
    inside && $0 ~ /^- `[^`]+` \[`P[0-9]+`\]:/ {
      line=$0
      sub(/^- `/, "", line)
      sub(/`.*/, "", line)
      print line
    }
  ' "AGENTS.md")
fi

# Vendor adapters (uncomment as applicable)
# assert_file_exists "WARP.md"
# assert_file_exists "CLAUDE.md"
# assert_file_exists ".github/copilot-instructions.md"
# assert_file_exists ".claude/settings.json"

# Standards (uncomment as applicable)
# assert_file_exists ".agents/standards/formatting.md"
# assert_file_exists ".agents/standards/development-workflow.md"

# Agent docs structure (uncomment as applicable)
# assert_file_exists ".agents/products/example/PRODUCT.md"
# assert_file_exists ".agents/products/example/features/_FEATURE_CATALOG.md"

# --- All adapters must reference AGENTS.md ---
assert_contains "README.md" "AGENTS.md"

if [[ -f "WARP.md" ]]; then
  assert_contains "WARP.md" "AGENTS.md"
fi

if [[ -f "CLAUDE.md" ]]; then
  assert_contains "CLAUDE.md" "AGENTS.md"
fi

if [[ -f ".github/copilot-instructions.md" ]]; then
  assert_contains ".github/copilot-instructions.md" "AGENTS.md"
fi

# --- Real Claude Code SessionStart hook must exist (not the old, dead .claude/hooks.json) ---
# Claude Code only executes hooks declared inside settings.json's "hooks" key (PascalCase
# events) -- a standalone .claude/hooks.json file is never read by Claude Code at all, so a
# repo that still has one has a SessionStart reminder and a Stop hook that have never fired.
if [[ -f ".claude/hooks.json" ]]; then
  warn ".claude/hooks.json exists but Claude Code never reads this file -- hooks only work"
  echo -e "${DIM}  inside settings.json's \"hooks\" key. Re-run apply-agent-base.sh for the${NC}"
  echo -e "${DIM}  exact fix, or see .claude/settings.json in the agent-base template.${NC}"
fi

_has_session_start_hook=false
for f in .claude/settings.json .claude/settings.local.json; do
  [[ -f "$f" ]] || continue
  if python3 -c "import json,sys; d=json.load(open('$f')); sys.exit(0 if d.get('hooks',{}).get('SessionStart') else 1)" 2>/dev/null; then
    _has_session_start_hook=true
  fi
done
if [[ "$_has_session_start_hook" == false ]]; then
  warn "No real SessionStart hook found in .claude/settings.json or settings.local.json."
  echo -e "${DIM}  Without one, this project's AGENTS.md is only ever read if the agent chooses${NC}"
  echo -e "${DIM}  to follow the text instruction in CLAUDE.md -- not guaranteed. See${NC}"
  echo -e "${DIM}  .claude/settings.json in the agent-base template for the hook to add.${NC}"
fi

# --- Codex hooks: SessionStart (parity with Claude Code, above) and UserPromptSubmit ---
# Codex has real SessionStart and UserPromptSubmit hooks, confirmed against published docs
# (not assumed) -- SessionStart accepts plain stdout as context, same as Claude Code, and
# cannot block session start regardless of exit code. GitHub Copilot's CLI has the
# UserPromptSubmit event but can't inject hook output into context yet; Warp has no
# per-message hook at all. For those two, the fallback is apply-agent-base.sh's copy-paste
# "Agent prompt" block, which works for any agent since a human pastes it in -- no hook needed.
if [[ -f ".codex/hooks.json" ]]; then
  if ! python3 -c "import json,sys; d=json.load(open('.codex/hooks.json')); sys.exit(0 if d.get('hooks',{}).get('SessionStart') else 1)" 2>/dev/null; then
    warn "No SessionStart hook found in .codex/hooks.json."
    echo -e "${DIM}  Without one, this project's AGENTS.md and goals/roadmap state are only ever${NC}"
    echo -e "${DIM}  read if Codex chooses to run agent-base-guard.sh itself -- not guaranteed. See${NC}"
    echo -e "${DIM}  .codex/hooks.json in the agent-base template for the hook to add.${NC}"
  fi
  if ! python3 -c "import json,sys; d=json.load(open('.codex/hooks.json')); sys.exit(0 if d.get('hooks',{}).get('UserPromptSubmit') else 1)" 2>/dev/null; then
    warn "No UserPromptSubmit hook found in .codex/hooks.json."
    echo -e "${DIM}  Without one, pending agent-base migrations are only ever surfaced via the${NC}"
    echo -e "${DIM}  copy-paste prompt apply-agent-base.sh prints, not automatically. See${NC}"
    echo -e "${DIM}  .codex/hooks.json in the agent-base template for the hook to add.${NC}"
  fi
fi

# --- Adapter hook injection ---
for adapter in WARP.md CLAUDE.md .github/copilot-instructions.md; do
  if [[ -f "$adapter" ]]; then
    if ! grep -q 'AGENT-BASE-HOOKS:START' "$adapter"; then
      fail "$adapter is missing AGENT-BASE-HOOKS block (run: bash _sub/agent-base/apply-agent-base.sh)"
    fi
    # Both short activation points are generated from .agents/hooks.md. Re-run
    # apply-agent-base.sh when an older adapter lacks either one.
    if grep -q 'AGENT-BASE-HOOKS:START' "$adapter" \
      && ! grep -q '^## Task Start .*MANDATORY$' "$adapter"; then
      fail "$adapter is missing the Task Start hook (run: bash _sub/agent-base/apply-agent-base.sh)"
    fi
    if grep -q 'AGENT-BASE-HOOKS:START' "$adapter" \
      && ! grep -q '^## Every Change .*MANDATORY$' "$adapter"; then
      fail "$adapter is missing the Every Change hook (run: bash _sub/agent-base/apply-agent-base.sh)"
    fi
  fi
done

# --- Adapters must stay thin, not become second rulebooks (DOC-003) ---
# Heuristic: count non-blank lines outside the AGENT-BASE-HOOKS managed block. A correctly
# thin adapter (title, one description line, the "Reading/Editing this file?" footer) sits
# around 7. Real rule content copy-pasted in from AGENTS.md pushes this well past the
# threshold below -- exactly the failure mode this check exists to catch (observed directly:
# identical rule text duplicated across CLAUDE.md/WARP.md/copilot-instructions.md instead of
# staying in AGENTS.md alone).
ADAPTER_THIN_MAX=20
for adapter in WARP.md CLAUDE.md .github/copilot-instructions.md; do
  if [[ -f "$adapter" ]]; then
    _adapter_extra=$(sed '/AGENT-BASE-HOOKS:START/,/AGENT-BASE-HOOKS:END/d' "$adapter" \
      | grep -cv '^[[:space:]]*$')
    if [[ "$_adapter_extra" -gt "$ADAPTER_THIN_MAX" ]]; then
      warn "$adapter has $_adapter_extra non-blank line(s) outside the managed hook block (expected ~7)."
      echo -e "${DIM}  This may be duplicating rules that belong in AGENTS.md only (DOC-003) --${NC}"
      echo -e "${DIM}  adapters must not become second rulebooks. See Tool Adapter Policy.${NC}"
    fi
  fi
done

# --- Standards must not be at repo root ---
for f in FORMATTING.md BRANCHING.md ARCHITECTURE.md; do
  if [[ -f "$f" ]]; then
    fail "'$f' should be in .agents/standards/, not repo root"
  fi
done

# --- Feature docs must live under their Product, not at repo root ---
for f in FEATURE_*.md; do
  if [[ -f "$f" ]]; then
    fail "Feature doc '$f' should be in .agents/products/<product>/features/, not repo root"
  fi
done

# --- Roadmap item Status must be one of the defined enum values (AB-ROADMAP-004) ---
# Catches an invented status (e.g. "Closed") silently drifting from the defined lifecycle --
# this exact bug shipped undetected for weeks in a downstream repo before this check existed.
if [[ -d .agents/roadmap ]]; then
  for f in .agents/roadmap/*.md; do
    [[ -f "$f" ]] || continue
    base="$(basename "$f")"
    [[ "$base" == _* ]] && continue
    status_line="$(grep -m1 '^\*\*Status\*\*:' "$f" || true)"
    [[ -z "$status_line" ]] && continue
    status_val="$(echo "$status_line" | sed -E 's/^\*\*Status\*\*: *//; s/[[:space:]]+$//')"
    case "$status_val" in
      Idea|Open|"In Progress"|Done|Blocked|Deferred|Archived) ;;
      *)
        fail "$f has Status: '$status_val' -- must be one of Idea|Open|In Progress|Done|Blocked|Deferred|Archived. Decide: did it succeed (Done) or not happen as scoped (Archived)?"
        ;;
    esac
  done
fi

# --- README must not duplicate rule content (spot-check) ---
# Uncomment and customize these checks for your project:
# assert_not_contains "README.md" "your duplicated rule text here"

# --- Optional: Run markdown linting on canonical docs ---
# Uncomment if markdownlint is available:
# if command -v markdownlint &>/dev/null || command -v npx &>/dev/null; then
#   bash scripts/markdown-lint.sh AGENTS.md README.md .agents/standards/formatting.md
# fi

# --- Result ---
if [[ $exit_code -ne 0 ]]; then
  echo ""
  error "Integrity guard FAILED. See errors above."
  exit "$exit_code"
fi

# Stamp the agent-base cache only on success so a failing guard retries next session.
# Write is best-effort - sandboxed agents (e.g. Codex) may not have write permission.
if [[ "$_stale" == true ]]; then
  { mkdir -p "$(dirname "$AGENT_BASE_CACHE")" && date +%s > "$AGENT_BASE_CACHE"; } 2>/dev/null || true
fi

success "Integrity guard passed."

# AGENT-BASE-AGENTS-MD-PRINT:START
# --- Force AGENTS.md's full content into context, every session ---
# Claude Code does not read AGENTS.md natively (confirmed: anthropics/claude-code#34235, open
# as of Aug 2026 -- it only auto-loads CLAUDE.md). Everything else this guard prints is either
# a structural pass/fail or a derived summary (goals, roadmap stats below); none of it is the
# actual rule text. This is the one part of the whole mechanism that closes that gap for real:
# SessionStart hook stdout is injected into context unconditionally (confirmed against the
# official docs), so catting the file here reaches every session regardless of whether the
# model would otherwise have chosen to follow the "read AGENTS.md" instruction in CLAUDE.md.
# Placed before the goals/roadmap-focus output below on purpose -- that section stays the
# literal last thing printed (see its own comment for why), this just guarantees the rule
# text arrives at all.
#
# Codex is the one supported vendor this does not apply to. It reads AGENTS.md natively already
# (the AGENTS.md specification, and this repository's own AGENT_BASE.md, both confirm it), and a
# live Codex session was observed receiving the file twice when this ran unconditionally for
# every vendor: once complete via that native read, once truncated via this block. Detail:
# roadmap item 090. .codex/hooks.json sets AGENT_BASE_VENDOR=codex on the exact command that
# invokes this guard, specifically so this check can tell; no other supported vendor sets it, so
# an unset or different value preserves today's behavior.
if [[ "${AGENT_BASE_VENDOR:-}" == "codex" ]]; then
  echo ""
  echo -e "${DIM}AGENTS.md: delivered via Codex's own native read; the forced copy is skipped for this vendor.${NC}"
else
  echo ""
  echo -e "${BOLD}${BLUE}=== AGENTS.md (forced into context every session -- see AGENTS.md itself) ===${NC}"
  echo ""
  cat AGENTS.md
  echo ""
  echo -e "${BOLD}${BLUE}=== end AGENTS.md ===${NC}"
fi
# AGENT-BASE-AGENTS-MD-PRINT:END

# --- Roadmap focus (informational, not a gate) ---
# Mechanical count of open work vs. done, printed every session so the same figure AB-GOALS-001
# states inline when it flags an off-goal item is also visible passively, without an off-goal
# task having to happen first. "Open" here means the In Progress and Open tables in
# _ROADMAP_CATALOG.md; "Done" means the Done table. Idea and Archived items are excluded on
# purpose - they are tracked in their own catalog sections but are not "work left to do".
ROADMAP_CATALOG=".agents/roadmap/_ROADMAP_CATALOG.md"

if [[ -f "$ROADMAP_CATALOG" ]]; then
  # Comment-aware: skips example rows inside <!-- TODO --> blocks in a freshly-seeded,
  # not-yet-filled-in catalog, so a brand-new project reports 0/0 instead of the sample rows.
  read -r _open_ct _done_ct < <(awk '
    in_comment {
      if ($0 ~ /-->/) in_comment = 0
      next
    }
    /<!--/ && !/-->/ { in_comment = 1; next }
    /<!--/ && /-->/  { next }
    /^## In Progress/ { sect = "open"; next }
    /^## Open/        { sect = "open"; next }
    /^## Done/        { sect = "done"; next }
    /^## / && sect != "" { sect = "" }
    sect != "" && /^\| *[0-9]+ *\|/ {
      if (sect == "done") done_ct++; else open_ct++
    }
    END { printf "%d %d\n", open_ct + 0, done_ct + 0 }
  ' "$ROADMAP_CATALOG")
  _total_ct=$(( _open_ct + _done_ct ))
  if [[ $_total_ct -gt 0 ]]; then
    _open_pct=$(( _open_ct * 100 / _total_ct ))
    step "Roadmap focus"
    echo -e "  ${DIM}$_open_ct of $_total_ct roadmap step(s) still open or in progress (~${_open_pct}%).${NC}"
    echo -e "  ${DIM}Not a gate - just how much of the roadmap is currently unfinished.${NC}"
  fi
fi

# Cost of this session (item 158). Printed before the Venture Context so the human sees it early.
# Silent unless the operator keeps a model ladder, so a client that has not adopted this is unchanged.
if [[ -f "$ROOT_DIR/scripts/check-model-cost.sh" ]]; then
  bash "$ROOT_DIR/scripts/check-model-cost.sh" || true
fi

# AGENT-BASE-VENTURE-CONTEXT:START
# Printed LAST so Purpose, Vision, Strategy, and Goals remain the agent's immediate task context.
if [[ -f "${_venture_helper:-scripts/venture.py}" ]]; then
  if _venture_context="$(python3 "${_venture_helper:-scripts/venture.py}" --root "$ROOT_DIR" context 2>&1)"; then
    step "Venture Context - use for every substantive decision (AB-VENTURE-001)"
    printf '%s\n' "$_venture_context"
  else
    echo ""
    warn "$_venture_context"
    echo -e "${DIM}  AB-VENTURE-001 requires the agent to stop before substantive work.${NC}"
    echo -e "${DIM}  Load .agents/agent-base/skills/venture-discovery/SKILL.md first; the${NC}"
    echo -e "${DIM}  current human supplies motives and Goals -- never infer them from the repo.${NC}"
  fi
  if [[ "${_venture_wiki_due:-false}" == true ]]; then
    echo -e "${DIM}  Venture wiki check is due. Load venture-wiki-sync; a clean check stays silent,${NC}"
    echo -e "${DIM}  drift produces one non-blocking reconciliation offer.${NC}"
  fi
  if [[ "${_venture_review_due:-false}" == true ]]; then
    echo -e "${DIM}  Venture hierarchy review is due. Load venture-review; a coherent review${NC}"
    echo -e "${DIM}  stays silent and any finding is non-blocking.${NC}"
  fi
else
  echo ""
  warn "scripts/venture.py is missing; Venture Context could not be loaded."
fi
# AGENT-BASE-VENTURE-CONTEXT:END
