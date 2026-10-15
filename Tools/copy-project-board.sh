#!/usr/bin/env bash
# Copy the RVS GitHub Project board from the personal account to the RVIntake-App org
# (Ownership Boundaries §4.5: a project belongs to an account, not a repo, so it did
# not move with the repo transfer).
#
# Never writes to the source board or its issues' content. Every write goes to the
# target board, and every phase is safe to re-run.
#
#   Tools/copy-project-board.sh plan     # default: read-only summary of both boards
#   Tools/copy-project-board.sh copy     # create the org board (fields + views), link it to the repo
#   Tools/copy-project-board.sh items    # add every issue and copy its field values
#   Tools/copy-project-board.sh verify   # read-only: compare field values, issue by issue
#
# Before `items`: open the new board → ⋯ → Workflows and turn every workflow OFF.
# "Item added to project" would overwrite the Status the script sets, and
# "Auto-close issue" would close real issues. `items` refuses to run while any is on.
# Turn them back on after `verify` is clean.
#
# Needs: gh (logged in with the `project` scope, as an owner of the target org) and jq.

set -euo pipefail

SRC_OWNER="${SRC_OWNER:-markarnoldutah}"     # a user account
SRC_NUMBER="${SRC_NUMBER:-1}"
DST_OWNER="${DST_OWNER:-RVIntake-App}"       # an organization
DST_TITLE="${DST_TITLE:-RVS}"
DST_REPO="${DST_REPO:-RVIntake-App/RVS}"
SLEEP="${SLEEP:-1.5}"                        # seconds between items; ~80 mutations/min
FIELDS='["Status","Priority","Size","Estimate","Start date","Target date","Sequence"]'

WORK="${WORK:-${TMPDIR:-/tmp}/rvs-project-copy}"
mkdir -p "$WORK"

die() { echo "ERROR: $*" >&2; exit 1; }
log() { echo "[$(date +%H:%M:%S)] $*"; }

# gh api graphql with retries on rate limiting and transient errors.
gql() {
  local attempt out
  for attempt in 1 2 3 4; do
    if out=$(gh api graphql "$@" 2>&1); then
      printf '%s' "$out"
      return 0
    fi
    if [[ "$out" == *"rate limit"* || "$out" == *"abuse"* || "$out" == *"502"* || "$out" == *"timeout"* ]]; then
      log "  retry $attempt after: $(head -c 160 <<<"$out")"
      sleep $((attempt * 60))
    else
      echo "$out" >&2
      return 1
    fi
  done
  echo "$out" >&2
  return 1
}

# Items of a board, normalised to [{num, cid, v: {Field: value}}], limited to $FIELDS.
# $1 = user|organization, $2 = login, $3 = project number
fetch_items() {
  local kind=$1 login=$2 num=$3
  gh api graphql --paginate -F num="$num" -f login="$login" -f query="
    query(\$login:String!, \$num:Int!, \$endCursor:String) {
      $kind(login:\$login) { projectV2(number:\$num) {
        items(first:100, after:\$endCursor) {
          pageInfo { hasNextPage endCursor }
          nodes {
            content { ... on Issue { id number } ... on PullRequest { id number } }
            fieldValues(first:50) { nodes {
              ... on ProjectV2ItemFieldSingleSelectValue { name   field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldNumberValue       { number field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldDateValue         { date   field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldTextValue         { text   field { ... on ProjectV2FieldCommon { name } } }
            } }
          }
        } } } }" \
    --jq ".data.$kind.projectV2.items.nodes[] | select(.content.number != null)
          | {num: .content.number, cid: .content.id,
             v: ([.fieldValues.nodes[] | select(.field.name != null)
                  | {key: .field.name, value: (if has(\"name\") then .name elif has(\"number\") then .number
                                               elif has(\"date\") then .date else .text end)}]
                 | from_entries | with_entries(select(.key as \$k | $FIELDS | index(\$k))))}" \
    | jq -s 'sort_by(.num)'
}

# Number of the org board titled $DST_TITLE, or empty.
dst_number() {
  gh project list --owner "$DST_OWNER" --format json --limit 100 \
    --jq ".projects[] | select(.title == \"$DST_TITLE\") | .number" | head -1
}

dst_meta() {
  local num=$1
  gql -F num="$num" -f login="$DST_OWNER" -f query='
    query($login:String!, $num:Int!) { organization(login:$login) { projectV2(number:$num) {
      id
      workflows(first:30) { nodes { name enabled } }
      fields(first:50) { nodes {
        ... on ProjectV2Field             { id name dataType }
        ... on ProjectV2SingleSelectField { id name dataType options { id name } }
      } }
    } } }' --jq '.data.organization.projectV2'
}

summary() {
  jq -r --argjson f "$FIELDS" '
    "  items: \(length)",
    ($f[] as $k | "  \($k): " + ([.[] | .v[$k] // empty | tostring] | group_by(.) | map("\(.[0])=\(length)")
                                  | if length > 8 then (.[0:8] + ["…"]) else . end | join(", ")))' "$1"
}

phase_plan() {
  log "Source: $SRC_OWNER project #$SRC_NUMBER"
  fetch_items user "$SRC_OWNER" "$SRC_NUMBER" > "$WORK/source.json"
  summary "$WORK/source.json"
  local n; n=$(dst_number)
  if [[ -z "$n" ]]; then
    log "Target: no '$DST_TITLE' board in $DST_OWNER yet. Next: copy"
  else
    log "Target: $DST_OWNER project #$n"
    fetch_items organization "$DST_OWNER" "$n" > "$WORK/target.json"
    summary "$WORK/target.json"
    dst_meta "$n" | jq -r '"  workflows on: " + ([.workflows.nodes[] | select(.enabled) | .name] | join(", ") | if . == "" then "none" else . end)'
  fi
}

phase_copy() {
  local n; n=$(dst_number)
  if [[ -n "$n" ]]; then
    log "Target already exists: $DST_OWNER project #$n. Not copying again."
  else
    log "Copying $SRC_OWNER #$SRC_NUMBER → $DST_OWNER '$DST_TITLE' (fields, views, workflows; no items)"
    n=$(gh project copy "$SRC_NUMBER" --source-owner "$SRC_OWNER" --target-owner "$DST_OWNER" \
          --title "$DST_TITLE" --format json --jq .number)
    log "Created $DST_OWNER project #$n"
  fi
  gh project link "$n" --owner "$DST_OWNER" --repo "$DST_REPO" >/dev/null 2>&1 \
    && log "Linked to $DST_REPO" || log "Link to $DST_REPO: already linked or not permitted (check the repo's Projects tab)"
  log "Next: open https://github.com/orgs/$DST_OWNER/projects/$n → ⋯ → Workflows, turn them all OFF, then run: items"
}

phase_items() {
  local n; n=$(dst_number)
  [[ -n "$n" ]] || die "No target board. Run: copy"
  dst_meta "$n" > "$WORK/target-meta.json"
  local on; on=$(jq -r '[.workflows.nodes[] | select(.enabled) | .name] | join(", ")' "$WORK/target-meta.json")
  [[ -z "$on" ]] || die "Workflows still on in the target board: $on. Turn them off first (⋯ → Workflows)."

  local pid; pid=$(jq -r .id "$WORK/target-meta.json")
  fetch_items user "$SRC_OWNER" "$SRC_NUMBER" > "$WORK/source.json"
  local total; total=$(jq length "$WORK/source.json")
  log "Copying $total items into $DST_OWNER #$n"

  local i=0 warned="$WORK/warnings.txt"; : > "$warned"
  while IFS= read -r item; do
    i=$((i + 1))
    local num cid iid mutation
    num=$(jq -r .num <<<"$item"); cid=$(jq -r .cid <<<"$item")
    iid=$(gql -f p="$pid" -f c="$cid" -f query='
      mutation($p:ID!, $c:ID!) { addProjectV2ItemById(input:{projectId:$p, contentId:$c}) { item { id } } }' \
      --jq .data.addProjectV2ItemById.item.id) || die "add failed for #$num"

    # One mutation per item, one aliased field update per value that has a target field.
    mutation=$(jq -r --argjson item "$item" --arg pid "$pid" --arg iid "$iid" '
      . as $meta
      | [ $item.v | to_entries[] as $e
          | ($meta.fields.nodes[] | select(.name == $e.key)) as $f
          | if $f.dataType == "SINGLE_SELECT" then
              ([$f.options[] | select(.name == $e.value) | .id][0]) as $o
              | if $o == null then {warn: "#\($item.num) \($e.key)=\($e.value): no such option"}
                else {fid: $f.id, val: "{singleSelectOptionId: \($o | tojson)}"} end
            elif $f.dataType == "NUMBER" then {fid: $f.id, val: "{number: \($e.value)}"}
            elif $f.dataType == "DATE"   then {fid: $f.id, val: "{date: \($e.value | tojson)}"}
            else {fid: $f.id, val: "{text: \($e.value | tostring | tojson)}"} end ]
      | (map(select(.warn)) | .[].warn | "WARN " + .),
        (map(select(.fid)) | if length == 0 then empty else
           "mutation {" + (to_entries | map(
             "f\(.key): updateProjectV2ItemFieldValue(input:{projectId: \($pid | tojson), itemId: \($iid | tojson), fieldId: \(.value.fid | tojson), value: \(.value.val)}) { clientMutationId }"
           ) | join(" ")) + "}" end)' "$WORK/target-meta.json")

    grep '^WARN ' <<<"$mutation" >> "$warned" || true
    mutation=$(grep -v '^WARN ' <<<"$mutation" || true)
    if [[ -n "$mutation" ]]; then
      gql -f query="$mutation" >/dev/null || die "field update failed for #$num"
    fi
    (( i % 25 == 0 || i == total )) && log "  $i/$total (#$num)"
    sleep "$SLEEP"
  done < <(jq -c '.[]' "$WORK/source.json")

  if [[ -s "$warned" ]]; then log "Values with no matching target option:"; cat "$warned"; fi
  log "Done. Next: verify"
}

phase_verify() {
  local n; n=$(dst_number)
  [[ -n "$n" ]] || die "No target board. Run: copy"
  fetch_items user "$SRC_OWNER" "$SRC_NUMBER" > "$WORK/source.json"
  fetch_items organization "$DST_OWNER" "$n" > "$WORK/target.json"
  jq -n -r --slurpfile s "$WORK/source.json" --slurpfile t "$WORK/target.json" '
    ($t[0] | map({key: (.num | tostring), value: .v}) | from_entries) as $tv
    | ($s[0] | map(.num | tostring)) as $snums
    | [ $s[0][] | (.num | tostring) as $k
        | if ($tv | has($k)) | not then "MISSING  #\($k)"
          elif .v != $tv[$k] then "DIFFERS  #\($k)  source=\(.v | tojson)  target=\($tv[$k] | tojson)"
          else empty end ] as $bad
    | [ $t[0][] | (.num | tostring) as $k | select($snums | index($k) | not) | "EXTRA    #\($k)" ] as $extra
    | "source items: \($s[0] | length)   target items: \($t[0] | length)",
      ($bad[], $extra[]),
      (if ($bad | length) == 0 then "OK: every source item is on the target board with the same field values." else "\($bad | length) problem(s)." end)'
  log "EXTRA items are fine if they were added to the repo after the copy."
}

case "${1:-plan}" in
  plan)   phase_plan ;;
  copy)   phase_copy ;;
  items)  phase_items ;;
  verify) phase_verify ;;
  *) die "usage: $0 [plan|copy|items|verify]" ;;
esac
