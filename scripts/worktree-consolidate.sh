#!/usr/bin/env bash
set -euo pipefail

# Generic worktree consolidation engine. Projects configure it through a thin root wrapper.
# This file is agent-base-owned and keep-in-sync; do not put project-specific values here.

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m'

info()    { printf '%b\n' "${BLUE}$1${NC}"; }
success() { printf '%b\n' "${GREEN}OK $1${NC}"; }
warn()    { printf '%b\n' "${YELLOW}WARN $1${NC}"; }
error()   { printf '%b\n' "${RED}ERROR $1${NC}" >&2; }
step()    { printf '\n%b\n' "${BOLD}${BLUE}--- $1 ---${NC}"; }

usage() {
  cat <<'USAGE'
Usage:
  worktree-consolidate.sh --integration-branch BRANCH --slot PATH [--slot PATH ...] [options]
  worktree-consolidate.sh --integration-branch BRANCH --repo-id CODE [options]

Required (one of):
  --slot PATH                  Declared slot path, relative to the primary worktree or absolute
                               (repeatable). Explicit, closed set of slots.
  --repo-id CODE               Two lowercase letters. Discover every sibling worktree whose folder
                               name is <CODE><N> (N a positive integer) and treat it as a declared
                               slot whose long-lived branch is _<code><N>. That branch carries the
                               slot's work and is merged on consolidation. Open-ended: new slots
                               are picked up automatically. Cannot be combined with --slot.

Always required:
  --integration-branch BRANCH  Branch checked out in the primary worktree

Options:
  --exclude-branch BRANCH      Do not merge a slot while it is on this idle branch (repeatable)
  --remote REMOTE              Push remote (default: origin)
  --check                      Validate and print the plan without changing anything
  --push                       Push the integration and merged slot branches after consolidation
  --to-slots-only              One-way propagation only: merge the integration branch into each
                               clean slot; never merge a slot's own commits into the integration
                               branch. With --push, only slot branches are pushed.
  --no-submodule-fetch         Forbid submodule initialization or fetching; require
                               synchronization to complete from already-local objects
  -h, --help                   Show this help

Only declared slot paths participate. Other registered worktrees are reported and ignored.
Without --push the script performs local merges but does not push. Submodule synchronization
allows initializing and fetching from remote by default; pass --no-submodule-fetch to require
already-local objects instead.

--to-slots-only exists because full consolidation folds a slot's own committed work into the
integration branch, which needs a human's judgment about whether that work is ready. Propagating
a fresh integration-branch commit (an Agent Base sync, for example) out to already-clean slots
carries no such judgment call, so it is the one direction a verified update procedure may run
unattended.

With --repo-id the engine hard-fails on a <CODE><N> worktree that is not on exactly its own
_<code><N> branch, and on an orphaned _<code><N> branch that has no matching worktree.
USAGE
}

integration_branch=''
repo_id=''
remote='origin'
check_only=false
do_push=false
to_slots_only=false
allow_submodule_fetch=true
declare -a configured_slots=()
declare -a excluded_branches=()

require_value() {
  local option="$1"
  local value="${2:-}"
  if [[ -z "$value" || "$value" == --* ]]; then
    error "$option requires a value."
    usage
    exit 2
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --integration-branch)
      require_value "$1" "${2:-}"
      integration_branch="$2"
      shift 2
      ;;
    --slot)
      require_value "$1" "${2:-}"
      configured_slots+=("$2")
      shift 2
      ;;
    --repo-id)
      require_value "$1" "${2:-}"
      repo_id="$2"
      shift 2
      ;;
    --exclude-branch)
      require_value "$1" "${2:-}"
      excluded_branches+=("$2")
      shift 2
      ;;
    --remote)
      require_value "$1" "${2:-}"
      remote="$2"
      shift 2
      ;;
    --check)
      check_only=true
      shift
      ;;
    --push)
      do_push=true
      shift
      ;;
    --to-slots-only)
      to_slots_only=true
      shift
      ;;
    --no-submodule-fetch)
      allow_submodule_fetch=false
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      error "Unknown option: $1"
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$integration_branch" ]]; then
  error '--integration-branch is required.'
  exit 2
fi
if [[ -n "$repo_id" && ${#configured_slots[@]} -gt 0 ]]; then
  error '--repo-id and --slot cannot be combined; pick one slot-declaration mode.'
  exit 2
fi
if [[ -z "$repo_id" && ${#configured_slots[@]} -eq 0 ]]; then
  error 'Declare slots with either --slot PATH (repeatable) or --repo-id CODE.'
  exit 2
fi
if [[ -n "$repo_id" && ! "$repo_id" =~ ^[a-z]{2}$ ]]; then
  error "--repo-id must be exactly two lowercase letters (got: '$repo_id')."
  exit 2
fi
if [[ "$check_only" == true && "$do_push" == true ]]; then
  error '--check and --push cannot be combined.'
  exit 2
fi
git check-ref-format --branch "$integration_branch" >/dev/null
for ((i = 0; i < ${#excluded_branches[@]}; i++)); do
  git check-ref-format --branch "${excluded_branches[$i]}" >/dev/null
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_dir="$(git -C "$script_dir" rev-parse --show-toplevel 2>/dev/null)" || {
  error 'The consolidation engine is not inside a Git worktree.'
  exit 1
}

declare -a wt_paths=()
declare -a wt_branches=()
current_path=''
current_branch=''

append_worktree() {
  [[ -n "$current_path" ]] || return 0
  if [[ ! -d "$current_path" ]]; then
    error "Registered worktree path is missing: $current_path"
    error "Remove its stale metadata with 'git worktree prune' before consolidating."
    exit 1
  fi
  wt_paths+=("$(cd "$current_path" && pwd -P)")
  wt_branches+=("$current_branch")
}

while IFS= read -r line; do
  if [[ "$line" == worktree\ * ]]; then
    append_worktree
    current_path="${line#worktree }"
    current_branch=''
  elif [[ "$line" == branch\ * ]]; then
    current_branch="${line#branch refs/heads/}"
  elif [[ "$line" == detached ]]; then
    current_branch='(detached)'
  fi
done < <(git -C "$repo_dir" worktree list --porcelain)
append_worktree

if [[ ${#wt_paths[@]} -eq 0 ]]; then
  error 'No Git worktrees were found.'
  exit 1
fi

primary_path="${wt_paths[0]}"
primary_branch="${wt_branches[0]}"
if [[ "$primary_branch" != "$integration_branch" ]]; then
  error "Primary worktree '$primary_path' is on '$primary_branch', not '$integration_branch'."
  exit 1
fi

primary_git_dir="$(git -C "$primary_path" rev-parse --path-format=absolute --git-dir)"
primary_common_dir="$(git -C "$primary_path" rev-parse --path-format=absolute --git-common-dir)"
if [[ "$primary_git_dir" != "$primary_common_dir" ]]; then
  error "Configured integration branch is not in the primary worktree: $primary_path"
  exit 1
fi

if [[ "$do_push" == true ]] &&
   ! git -C "$primary_path" remote get-url "$remote" >/dev/null 2>&1; then
  error "Remote '$remote' does not exist."
  exit 1
fi

if [[ -n "$repo_id" ]]; then
  step "Discovering '$repo_id' worktree slots"
  # Slot folders use the uppercase form (<REPO_ID><N>); slot branches use lowercase (_<repo_id><N>).
  repo_id_upper="$(printf '%s' "$repo_id" | tr '[:lower:]' '[:upper:]')"
  # The main worktree must be cloned into a folder named exactly <REPO_ID>. The whole slot naming
  # scheme is derived from that, so a mismatch is a hard stop, not a guess.
  if [[ "${primary_path##*/}" != "$repo_id_upper" ]]; then
    error "Main worktree folder is '${primary_path##*/}', not '$repo_id_upper'."
    error "Clone this repository into a folder named '$repo_id_upper' (see development-workflow.md)."
    exit 1
  fi
  declare -a discovered_numbers=()
  for i in "${!wt_paths[@]}"; do
    [[ "${wt_paths[$i]}" == "$primary_path" ]] && continue
    base="${wt_paths[$i]##*/}"
    [[ "$base" =~ ^${repo_id_upper}([1-9][0-9]*)$ ]] || continue
    n="${BASH_REMATCH[1]}"
    branch="${wt_branches[$i]}"
    expected="_${repo_id}${n}"
    # One slot is one long-lived branch: <REPO_ID><N> must be on exactly its own _<repo_id><N>.
    # Anything else -- detached, another slot's branch, the integration branch, an ad-hoc name --
    # is a convention break the engine will not guess its way through.
    if [[ "$branch" != "$expected" ]]; then
      error "Slot worktree '${wt_paths[$i]}' must be on '$expected', not '${branch:-(detached)}'."
      exit 1
    fi
    configured_slots+=("${wt_paths[$i]}")
    discovered_numbers+=("$n")
  done

  if [[ ${#configured_slots[@]} -eq 0 ]]; then
    error "No '${repo_id_upper}<N>' worktrees are registered as siblings of $primary_path."
    exit 1
  fi

  # Every local _<repo_id><N> branch must belong to a discovered worktree. A leftover slot branch
  # whose worktree was removed would otherwise sit unmergeable and invisible.
  while IFS= read -r slot_branch; do
    [[ "$slot_branch" =~ ^_${repo_id}([1-9][0-9]*)$ ]] || continue
    orphan_n="${BASH_REMATCH[1]}"
    match=false
    for ((k = 0; k < ${#discovered_numbers[@]}; k++)); do
      [[ "${discovered_numbers[$k]}" == "$orphan_n" ]] && match=true
    done
    if [[ "$match" != true ]]; then
      error "Orphaned slot branch '$slot_branch' has no '${repo_id_upper}${orphan_n}' worktree."
      exit 1
    fi
  done < <(git -C "$repo_dir" for-each-ref --format='%(refname:short)' refs/heads)

  info "Discovered ${#configured_slots[@]} slot(s): ${configured_slots[*]##*/}"
fi

canonicalize_slot() {
  local value="$1"
  local candidate
  if [[ "$value" == /* ]]; then
    candidate="$value"
  else
    candidate="$primary_path/$value"
  fi
  if [[ ! -d "$candidate" ]]; then
    error "Configured slot does not exist: $value"
    return 1
  fi
  (cd "$candidate" && pwd -P)
}

is_excluded_branch() {
  local candidate="$1"
  local excluded i
  for ((i = 0; i < ${#excluded_branches[@]}; i++)); do
    excluded="${excluded_branches[$i]}"
    [[ "$candidate" == "$excluded" ]] && return 0
  done
  return 1
}

declare -a slot_paths=()
declare -a slot_branches=()
declare -a active_paths=()
declare -a active_branches=()

for configured in "${configured_slots[@]}"; do
  canonical="$(canonicalize_slot "$configured")" || exit 1
  if [[ "$canonical" == "$primary_path" ]]; then
    error "The primary worktree cannot also be a slot: $configured"
    exit 1
  fi
  for ((i = 0; i < ${#slot_paths[@]}; i++)); do
    existing="${slot_paths[$i]}"
    if [[ "$existing" == "$canonical" ]]; then
      error "Configured slot is duplicated: $configured"
      exit 1
    fi
  done

  found=false
  for i in "${!wt_paths[@]}"; do
    if [[ "${wt_paths[$i]}" == "$canonical" ]]; then
      found=true
      branch="${wt_branches[$i]}"
      if [[ -z "$branch" || "$branch" == '(detached)' ]]; then
        error "Configured slot '$canonical' is not on an attached branch."
        exit 1
      fi
      if [[ "$branch" == "$integration_branch" ]]; then
        error "Integration branch '$integration_branch' is also checked out in slot '$canonical'."
        exit 1
      fi
      slot_paths+=("$canonical")
      slot_branches+=("$branch")
      if ! is_excluded_branch "$branch"; then
        active_paths+=("$canonical")
        active_branches+=("$branch")
      fi
      break
    fi
  done
  if [[ "$found" != true ]]; then
    error "Configured slot is not a registered worktree: $canonical"
    exit 1
  fi
done

for i in "${!wt_paths[@]}"; do
  [[ "${wt_paths[$i]}" == "$primary_path" ]] && continue
  declared=false
  for ((j = 0; j < ${#slot_paths[@]}; j++)); do
    slot_path="${slot_paths[$j]}"
    [[ "${wt_paths[$i]}" == "$slot_path" ]] && declared=true
  done
  if [[ "$declared" != true ]]; then
    warn "Ignoring undeclared worktree '${wt_paths[$i]}' (${wt_branches[$i]:-no branch})."
  fi
done

check_operation_state() {
  local wt_path="$1"
  local branch="$2"
  local marker
  for marker in MERGE_HEAD CHERRY_PICK_HEAD REVERT_HEAD rebase-merge rebase-apply; do
    if [[ -e "$(git -C "$wt_path" rev-parse --git-path "$marker")" ]]; then
      error "Worktree '$wt_path' ($branch) has an in-progress Git operation: $marker"
      exit 1
    fi
  done
}

check_submodules_syncable() {
  local wt_path="$1"
  local branch="$2"
  [[ -f "$wt_path/.gitmodules" ]] || return 0

  local line rest prefix sub_path abs attached_branch unsafe=0
  while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    prefix="${line:0:1}"
    rest="${line:1}"
    sub_path="${rest#* }"
    [[ "$sub_path" == *' ('*')' ]] && sub_path="${sub_path% (*}"
    abs="$wt_path/$sub_path"

    if [[ "$prefix" == 'U' ]]; then
      error "Submodule '$sub_path' in '$wt_path' ($branch) has a merge conflict."
      unsafe=1
      continue
    fi
    if [[ "$prefix" == '-' ]]; then
      if [[ "$allow_submodule_fetch" != true ]]; then
        error "Submodule '$sub_path' in '$wt_path' ($branch) is not initialized."
        error 'Drop --no-submodule-fetch if remote access is acceptable.'
        unsafe=1
      fi
      continue
    fi
    if [[ -n "$(git -C "$abs" status --porcelain --untracked-files=all || true)" ]]; then
      error "Submodule '$sub_path' in '$wt_path' ($branch) has uncommitted changes."
      unsafe=1
      continue
    fi
    attached_branch="$(git -C "$abs" symbolic-ref --quiet --short HEAD 2>/dev/null || true)"
    if [[ "$prefix" == '+' && -n "$attached_branch" ]]; then
      error "Submodule '$sub_path' in '$wt_path' ($branch) is attached to '$attached_branch'"
      error 'but its checkout differs from the recorded commit; reconcile it manually.'
      unsafe=1
      continue
    fi
    if [[ "$prefix" == '+' ]] &&
       [[ -z "$(git -C "$abs" for-each-ref --contains HEAD --format='%(refname)' refs/heads refs/remotes refs/tags 2>/dev/null || true)" ]]; then
      error "Submodule '$sub_path' in '$wt_path' ($branch) is at an unreferenced commit."
      unsafe=1
    fi
  done < <(git -C "$wt_path" submodule status --recursive)

  [[ "$unsafe" -eq 0 ]] || exit 1
}

sync_submodules() {
  local wt_path="$1"
  local branch="$2"
  [[ -f "$wt_path/.gitmodules" ]] || return 0
  local status_output
  status_output="$(git -C "$wt_path" submodule status --recursive)"
  [[ -n "$status_output" ]] || return 0
  grep -q '^[+-]' <<<"$status_output" || return 0

  info "Synchronizing submodules in $branch..."
  git -C "$wt_path" submodule sync --recursive --quiet
  if [[ "$allow_submodule_fetch" == true ]]; then
    git -C "$wt_path" submodule update --init --recursive
  else
    if ! git -C "$wt_path" submodule update --init --recursive --no-fetch; then
      error 'Local submodule objects were insufficient for synchronization.'
      error 'Drop --no-submodule-fetch if remote access is acceptable.'
      exit 1
    fi
  fi
  success "Submodules in $branch match the recorded commits"
}

check_clean() {
  local wt_path="$1"
  local branch="$2"
  local status_output
  if [[ "$check_only" == true ]]; then
    status_output="$(git -C "$wt_path" status --porcelain --untracked-files=all --ignore-submodules=all)"
    if ! git -C "$wt_path" diff --cached --quiet --ignore-submodules=none; then
      error "Worktree '$wt_path' ($branch) has staged changes, including a submodule pointer."
      exit 1
    fi
  else
    status_output="$(git -C "$wt_path" status --porcelain --untracked-files=all)"
  fi
  if [[ -n "$status_output" ]]; then
    error "Worktree '$wt_path' ($branch) has uncommitted changes."
    exit 1
  fi
}

trap 'printf "\n" >&2; error "Consolidation stopped. Resolve the reported state before re-running."' ERR

step 'Worktree consolidation preflight'
info "Integration: $integration_branch ($primary_path)"
if [[ "$to_slots_only" == true ]]; then
  info 'Mode:        one-way, integration -> slot only (--to-slots-only)'
fi
for i in "${!slot_paths[@]}"; do
  if is_excluded_branch "${slot_branches[$i]}"; then
    info "Idle slot:   ${slot_branches[$i]} (${slot_paths[$i]})"
  elif [[ "$to_slots_only" == true ]]; then
    info "Propagate to slot: ${slot_branches[$i]} (${slot_paths[$i]})"
  else
    info "Merge slot:  ${slot_branches[$i]} (${slot_paths[$i]})"
  fi
done

check_operation_state "$primary_path" "$integration_branch"
check_submodules_syncable "$primary_path" "$integration_branch"
for i in "${!slot_paths[@]}"; do
  check_operation_state "${slot_paths[$i]}" "${slot_branches[$i]}"
  check_submodules_syncable "${slot_paths[$i]}" "${slot_branches[$i]}"
done

if [[ "$check_only" != true ]]; then
  sync_submodules "$primary_path" "$integration_branch"
  for i in "${!slot_paths[@]}"; do
    sync_submodules "${slot_paths[$i]}" "${slot_branches[$i]}"
  done
fi

check_clean "$primary_path" "$integration_branch"
for i in "${!slot_paths[@]}"; do
  check_clean "${slot_paths[$i]}" "${slot_branches[$i]}"
done
success 'Declared worktrees passed preflight'

if [[ ${#active_paths[@]} -eq 0 ]]; then
  warn 'Every declared slot is on an excluded idle branch; there is nothing to consolidate.'
  exit 0
fi

if [[ "$check_only" == true ]]; then
  success 'Check complete; no files, refs, submodules, or remotes were changed'
  exit 0
fi

if [[ "$to_slots_only" == true ]]; then
  info "Skipping slot -> $integration_branch merges (--to-slots-only)"
else
  step "Merging declared slots into $integration_branch"
  for i in "${!active_branches[@]}"; do
    branch="${active_branches[$i]}"
    info "Merging $branch into $integration_branch..."
    git -C "$primary_path" merge "$branch" --no-edit
    sync_submodules "$primary_path" "$integration_branch"
    success "Merged $branch into $integration_branch"
  done
fi

step "Merging $integration_branch back into declared slots"
for i in "${!active_paths[@]}"; do
  path="${active_paths[$i]}"
  branch="${active_branches[$i]}"
  info "Merging $integration_branch into $branch..."
  git -C "$path" merge "$integration_branch" --no-edit
  sync_submodules "$path" "$branch"
  success "Merged $integration_branch into $branch"
done

push_branch() {
  local wt_path="$1"
  local branch="$2"
  info "Pushing $branch to $remote..."
  git -C "$wt_path" push --recurse-submodules=check "$remote" "$branch"
  success "Pushed $branch"
}

if [[ "$do_push" == true ]]; then
  step "Pushing consolidated branches to $remote"
  if [[ "$to_slots_only" != true ]]; then
    push_branch "$primary_path" "$integration_branch"
  fi
  for i in "${!active_paths[@]}"; do
    push_branch "${active_paths[$i]}" "${active_branches[$i]}"
  done
fi

printf '\n'
if [[ "$to_slots_only" == true ]]; then
  if [[ "$do_push" == true ]]; then
    success 'One-way propagation and slot push complete'
  else
    success 'One-way propagation complete; re-run with --push to publish the slot branches'
  fi
elif [[ "$do_push" == true ]]; then
  success 'Consolidation and push complete'
else
  success 'Consolidation complete; re-run with --push to consolidate and publish'
fi
