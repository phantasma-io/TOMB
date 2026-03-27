#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pack_dir="${1:-$repo_root/output/nupkgs}"
project="$repo_root/Compiler/TombCompiler.csproj"

if [[ -f "$repo_root/.env" ]]; then
  # shellcheck disable=SC2046
  export $(grep -v '^#' "$repo_root/.env" | xargs || true)
fi

: "${NUGET_API_KEY:?Missing NUGET_API_KEY in .env or environment}"

mkdir -p "$pack_dir"
rm -f "$pack_dir"/pha-tomb.*.nupkg "$pack_dir"/pha-tomb.*.snupkg

echo "Restoring $project"
dotnet restore "$project"

echo "Building $project"
dotnet build "$project" --configuration Release --no-restore

echo "Packing $project"
dotnet pack "$project" --configuration Release --no-build --output "$pack_dir"

package="$(find "$pack_dir" -maxdepth 1 -type f -name 'pha-tomb.*.nupkg' ! -name '*.snupkg' | sort | tail -n 1)"

if [[ -z "$package" ]]; then
  echo "ERROR: package not found in $pack_dir" >&2
  exit 1
fi

echo "Publishing $package"
dotnet nuget push "$package" \
  --api-key "$NUGET_API_KEY" \
  --source "https://api.nuget.org/v3/index.json" \
  --skip-duplicate \
  --no-symbols

echo "publish-nuget: DONE"
