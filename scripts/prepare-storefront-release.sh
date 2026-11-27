#!/usr/bin/env bash
set -euo pipefail

tag=${1:?Pass a release tag}
output=${2:?Pass an output directory}
if [[ ! "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Expected a stable release tag such as v1.2.0." >&2
    exit 1
fi
version=${tag#v}
archive="SteadySigns-$version.zip"
mkdir -p "$output"
output=$(cd "$output" && pwd)
release=$(curl --fail --silent --show-error --retry 3 \
    "https://api.github.com/repos/jps60/valheim-steady-signs/releases/tags/$tag")
jq -e --arg tag "$tag" '.tag_name == $tag and (.draft or .prerelease | not)' <<< "$release" > /dev/null
asset=$(jq -cer --arg archive "$archive" '[.assets[] | select(.name == $archive)] | if length == 1 then .[0] else error("Expected one release ZIP") end' <<< "$release")
checksum=$(jq -r '.body // ""' <<< "$release" | tr -d '\r' |
    sed -nE 's/^SHA-256: ([0-9a-fA-F]{64})[[:space:]]*$/\1/p' | tr '[:upper:]' '[:lower:]')
if [[ ! "$checksum" =~ ^[0-9a-f]{64}$ ]]; then
    echo "Expected one SHA-256 in the GitHub release notes." >&2
    exit 1
fi
digest=$(jq -r '.digest // ""' <<< "$asset")
if [[ -n "$digest" && "$digest" != "sha256:$checksum" ]]; then
    echo "GitHub asset digest differs from release notes." >&2
    exit 1
fi

download_url=$(jq -er '.browser_download_url' <<< "$asset")
expected_url="https://github.com/jps60/valheim-steady-signs/releases/download/$tag/$archive"
if [[ "$download_url" != "$expected_url" ]]; then
    echo "Unexpected release download URL." >&2
    exit 1
fi
curl --fail --silent --show-error --location --retry 3 "$download_url" -o "$output/$archive"
(cd "$output" && printf '%s  %s\n' "$checksum" "$archive" | sha256sum --check)
manifest=$(unzip -p "$output/$archive" manifest.json)
jq -e --arg version "$version" \
    '.name == "SteadySigns" and .author == "Swiftfingers" and .version_number == $version' \
    <<< "$manifest" > /dev/null

# Thunderstore receives the original archive. Nexus receives the same DLL
# with a Valheim-relative install path for manual extraction.
staging=$(mktemp -d)
trap 'rm -rf -- "$staging"' EXIT
mkdir -p "$staging/BepInEx/plugins/SteadySigns"
unzip -p "$output/$archive" DeliberateInteract.dll > "$staging/BepInEx/plugins/SteadySigns/DeliberateInteract.dll"
test -s "$staging/BepInEx/plugins/SteadySigns/DeliberateInteract.dll"
for file in README.md LICENSE; do
    unzip -p "$output/$archive" "$file" > "$staging/$file"
done
(cd "$staging" && zip -q "$output/SteadySigns-$version-Nexus.zip" \
    BepInEx/plugins/SteadySigns/DeliberateInteract.dll README.md LICENSE)
