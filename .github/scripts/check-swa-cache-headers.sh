#!/usr/bin/env bash
# Fails the deploy if a Blazor SWA's Cache-Control headers would let a browser keep an
# old build after a deploy. Rules live in each app's wwwroot/staticwebapp.config.json.
#
#   check-swa-cache-headers.sh <site-url> <published-wwwroot-dir>
#
# Fixed-name files (index.html, any SPA route, dotnet.js, blazor.webassembly.js, _content/*)
# must revalidate: no-cache. Fingerprinted _framework/* files change name every build, so
# they must be immutable — the dir argument is used to find one by its real name.
set -euo pipefail

site="${1%/}"
wwwroot="$2"
failures=0

cache_control() {
  curl --silent --show-error --retry 5 --retry-delay 5 --retry-all-errors \
       --output /dev/null --dump-header - "$site$1" \
    | tr -d '\r' | grep -i '^cache-control:' | cut -d' ' -f2- || true
}

expect() {
  local path="$1" want="$2" got
  got="$(cache_control "$path")"
  if [[ "$got" == *"$want"* ]] && { [[ "$want" == immutable ]] || [[ "$got" != *immutable* ]]; }; then
    echo "ok    $path -> $got"
  else
    echo "FAIL  $path -> '${got:-<none>}' (expected $want)"
    failures=$((failures + 1))
  fi
}

fingerprinted="$(cd "$wwwroot" && find _framework -name '*.wasm' -print -quit)"

expect "/"                                      no-cache
expect "/header-check-spa-route"                no-cache
expect "/_framework/dotnet.js"                  no-cache
expect "/_framework/blazor.webassembly.js"      no-cache
expect "/_content/MudBlazor/MudBlazor.min.css"  no-cache
expect "/$fingerprinted"                        immutable

# Manager's build ID, polled by open tabs to offer a reload (issue #859).
if [[ -f "$wwwroot/version.json" ]]; then
  expect "/version.json"                        no-cache
fi

if (( failures > 0 )); then
  echo "::error::$failures Cache-Control check(s) failed for $site — see staticwebapp.config.json"
  exit 1
fi
