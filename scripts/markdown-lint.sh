#!/usr/bin/env bash
set -euo pipefail

# markdown-lint.sh - Lint and auto-fix Markdown files using markdownlint.
#
# Usage (from the repo root):
#   bash scripts/markdown-lint.sh [--fix] [--changed|--staged] [file1.md file2.md ...]

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

# --- Path resolution ---
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && cd .. && pwd)"
cd "$ROOT_DIR"

CONFIG_FILE=".markdownlint.jsonc"

ensure_markdownlint_in_path() {
  if command -v markdownlint >/dev/null 2>&1; then
    return 0
  fi

  local nvm_dir="${NVM_DIR:-$HOME/.nvm}"
  local candidate_bin

  for candidate_bin in "$nvm_dir"/versions/node/*/bin; do
    if [[ -x "$candidate_bin/markdownlint" ]]; then
      export PATH="$candidate_bin:$PATH"
      return 0
    fi
  done

  return 1
}

if ! ensure_markdownlint_in_path; then
  error "markdownlint is not installed or not in PATH."
  echo "GUI git clients often run with a stripped PATH and may not load nvm from shell startup files."
  info "Install: npm install -g markdownlint-cli"
  exit 1
fi

print_usage() {
  cat <<'USAGE'
Usage:
  bash scripts/markdown-lint.sh [--fix] [--changed|--staged] [file1.md file2.md ...]

Options:
  --fix       Apply auto-fixes where markdownlint supports it
  --changed   Lint changed/untracked markdown files only
  --staged    Lint staged markdown files only

Examples:
  bash scripts/markdown-lint.sh --changed
  bash scripts/markdown-lint.sh --staged
  bash scripts/markdown-lint.sh --fix --changed
  bash scripts/markdown-lint.sh --fix --staged
  bash scripts/markdown-lint.sh AGENTS.md README.md
USAGE
}

apply_fix="false"
changed_only="false"
staged_only="false"
declare -a files=()

for arg in "$@"; do
  case "$arg" in
    --fix)
      apply_fix="true"
      ;;
    --changed)
      changed_only="true"
      ;;
    --staged)
      staged_only="true"
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      files+=("$arg")
      ;;
  esac
done

if [[ "$changed_only" == "true" && "$staged_only" == "true" ]]; then
  error "--changed and --staged cannot be combined."
  exit 1
fi

if [[ ("$changed_only" == "true" || "$staged_only" == "true") && ${#files[@]} -gt 0 ]]; then
  error "--changed/--staged cannot be combined with explicit file arguments."
  exit 1
fi

if [[ "$changed_only" == "true" ]]; then
  declare -a changed_files=()

  while IFS= read -r file; do
    [[ -n "$file" ]] && changed_files+=("$file")
  done < <(git diff --name-only --diff-filter=ACMRTUXB -- '*.md')

  while IFS= read -r file; do
    [[ -n "$file" ]] && changed_files+=("$file")
  done < <(git ls-files --others --exclude-standard -- '*.md')

  if [[ ${#changed_files[@]} -eq 0 ]]; then
    success "No changed markdown files found."
    exit 0
  fi

  while IFS= read -r file; do
    [[ -n "$file" ]] && files+=("$file")
  done < <(printf '%s\n' "${changed_files[@]}" | awk '!seen[$0]++')
fi

if [[ "$staged_only" == "true" ]]; then
  declare -a staged_files=()

  while IFS= read -r file; do
    [[ -n "$file" ]] && staged_files+=("$file")
  done < <(git diff --cached --name-only --diff-filter=ACMRTUXB -- '*.md')

  if [[ ${#staged_files[@]} -eq 0 ]]; then
    success "No staged markdown files found."
    exit 0
  fi

  while IFS= read -r file; do
    [[ -n "$file" ]] && files+=("$file")
  done < <(printf '%s\n' "${staged_files[@]}" | awk '!seen[$0]++')
fi

if [[ ${#files[@]} -eq 0 ]]; then
  while IFS= read -r file; do
    [[ -n "$file" ]] && files+=("$file")
  done < <(git ls-files '*.md')
fi

declare -a existing_files=()
for file in "${files[@]}"; do
  [[ -f "$file" ]] && existing_files+=("$file")
done

if [[ ${#existing_files[@]} -eq 0 ]]; then
  success "No markdown files to lint."
  exit 0
fi

info "Linting ${#existing_files[@]} markdown file(s)..."

if [[ "$apply_fix" == "true" ]]; then
  markdownlint --config "$CONFIG_FILE" --fix "${existing_files[@]}"
else
  markdownlint --config "$CONFIG_FILE" "${existing_files[@]}"
fi
