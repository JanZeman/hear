#!/usr/bin/env bash
set -euo pipefail

# Tell a session what it is about to cost, before it spends anything (roadmap item 158).
#
# The failure this answers was observed rather than imagined: a small repair on a downstream client
# ran on an expensive model because an expensive session happened to be the one that started, and
# nobody found out until the tokens were gone. Nothing anywhere said which model is expensive.
#
# It warns and never refuses. An expensive model is the right choice often enough that stopping the
# work would cost more than the tokens, and a check that blocks gets disabled.
#
# No vendor here exports the model as an environment variable, so this cannot measure what it is
# running on. It prints the facts and names the obligation; the agent knows which model it is and
# `AB-COST-001` requires it to say so. That is weaker than a measurement and is the honest shape
# available: inventing a detection that reads an unset variable and reports "cheap" would be worse
# than admitting the gap.
#
# Silent without a ladder. A client that has not adopted this must behave exactly as before, which
# is the same guarantee staffing gives for a missing configuration.

_here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
_ladder="${_here}/model-ladder.py"

[[ -f "$_ladder" ]] || exit 0

_config="${AB_MODEL_LADDER_CONFIG:-${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/model-ladder.json}"
[[ -f "$_config" ]] || exit 0

if ! _expensive="$(python3 "$_ladder" --expensive 2>&1)"; then
  echo "Model ladder unreadable, so nothing here knows what this session costs:"
  echo "  $_expensive"
  exit 0
fi

echo "============================================================"
echo " Session cost (AB-COST-001)"
echo "============================================================"
echo "Expensive models on this operator's ladder, with what the cheapest"
echo "model from the same vendor would cost instead:"
printf '%s\n' "$_expensive" | sed 's/^/  /'
echo ""
echo "You know which model you are running on. If it is one of those, say"
echo "so in your first response, in one line, with that ratio, and let the"
echo "human decide. The ratio is a price, not a claim that the cheaper"
echo "model could do this work; that judgement is yours to state alongside"
echo "it. Do not refuse the work and do not ask permission."

if _stale="$(python3 "$_ladder" --check 2>&1 | grep '^note:' || true)"; then
  [[ -n "$_stale" ]] && { echo ""; printf '%s\n' "$_stale" | sed 's/^note: /Note: /'; }
fi

_staffing="${AB_STAFFING_CONFIG:-${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/staffing.json}"
if [[ -f "$_staffing" ]]; then
  _disagreements="$(python3 - "$_ladder" "$_config" "$_staffing" <<'PY' || true
import importlib.util, json, sys
spec = importlib.util.spec_from_file_location("model_ladder", sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
try:
    data = module.load(__import__("pathlib").Path(sys.argv[2]))
    staffing = json.loads(open(sys.argv[3], encoding="utf-8").read())
except Exception:
    raise SystemExit(0)
for line in module.disagreements(data, staffing):
    print(line)
PY
)"
  if [[ -n "$_disagreements" ]]; then
    echo ""
    echo "The staffing map and the ladder disagree. Only the operator knows which is out of date:"
    printf '%s\n' "$_disagreements" | sed 's/^/  /'
  fi
fi

echo "============================================================"
