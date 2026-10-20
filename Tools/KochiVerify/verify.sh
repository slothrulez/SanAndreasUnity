#!/usr/bin/env bash
# License-free check of the Kochi prototype: compiles Kochi.Runtime against the
# Unity 2020.3 managed DLLs with Mono's mcs, then validates a district JSON.
# Not a substitute for a real Unity import/build.
# Usage: UNITY_EDITOR_DIR=/path/to/Editor Tools/KochiVerify/verify.sh [district.json]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
U="${UNITY_EDITOR_DIR:?set UNITY_EDITOR_DIR to the Unity 2020.3.26f1 Editor folder}/Data"
DISTRICT="${1:-$REPO/Assets/Kochi/Resources/Kochi/Districts/fort_kochi_test.json}"
OUT="${KOCHI_VERIFY_OUT:-$REPO/Temp/KochiVerify}"
API="$U/MonoBleedingEdge/lib/mono/4.7.1-api"
REFS=()
for f in "$API/mscorlib.dll" "$API/System.dll" "$API/System.Core.dll" "$API/Facades/netstandard.dll" \
         "$U"/Managed/UnityEngine/UnityEngine.*Module.dll "$U/Managed/UnityEngine/UnityEngine.dll"; do
  REFS+=("-r:$f")
done
mkdir -p "$OUT"
mapfile -t SRC < <(find "$REPO/Assets/Kochi/Scripts" -name '*.cs')
mcs -nostdlib -langversion:experimental -warnaserror -target:library -out:"$OUT/Kochi.Runtime.dll" "${REFS[@]}" "${SRC[@]}"
mcs -nostdlib -langversion:experimental -target:exe -out:"$OUT/KochiLayoutCheck.exe" "${REFS[@]}" \
  -r:"$OUT/Kochi.Runtime.dll" -r:"$REPO/Assets/Plugins/Newtonsoft.Json.dll" "$HERE/KochiLayoutCheck.cs"
cp "$REPO/Assets/Plugins/Newtonsoft.Json.dll" "$U"/Managed/UnityEngine/UnityEngine.*Module.dll "$OUT/"
mono "$OUT/KochiLayoutCheck.exe" "$DISTRICT"
