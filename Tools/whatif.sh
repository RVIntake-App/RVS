#!/usr/bin/env bash
# Run the subscription-scope what-if for one environment and save the output where
# Claude can read it, so the result no longer has to be pasted into the chat.
#
#   Tools/whatif.sh staging
#   Tools/whatif.sh prod
#
# Writes ~/.rvs-whatif/<env>.txt (outside the repo: the repo is public). The file opens
# with a header naming the commit, branch and time, so a stale run is easy to spot.
# The output is also shown in the terminal as usual.
#
# Read-only: what-if never changes anything in Azure. Deploys stay manual, from scratch.azcli.

set -euo pipefail

env="${1:-}"
case "$env" in
  staging|prod) ;;
  *) echo "usage: $0 staging|prod" >&2; exit 2 ;;
esac

repo="$(cd "$(dirname "$0")/.." && pwd)"
bicep="$repo/Docs/ASOT/Infra/Bicep.IaC"
outdir="$HOME/.rvs-whatif"
out="$outdir/$env.txt"
mkdir -p "$outdir"

dirty=""
[[ -n "$(git -C "$repo" status --porcelain -- "$bicep")" ]] && dirty=" (Bicep has uncommitted changes)"

{
  echo "# what-if: $env"
  echo "# run:     $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "# branch:  $(git -C "$repo" rev-parse --abbrev-ref HEAD)"
  echo "# commit:  $(git -C "$repo" rev-parse --short HEAD)$dirty"
  echo "# account: $(az account show --query '{sub:name, id:id}' -o tsv 2>/dev/null || echo 'not logged in')"
  echo
} > "$out"

set +e
AZURE_CORE_NO_COLOR=true az deployment sub what-if \
  --location westus3 \
  --template-file "$bicep/main.bicep" \
  --parameters "$bicep/parameters/$env.bicepparam" \
  2>&1 | tee -a "$out"
status=${PIPESTATUS[0]}
set -e

echo >> "$out"
echo "# exit: $status" >> "$out"
echo
echo "Saved to $out (exit $status)"
exit "$status"
