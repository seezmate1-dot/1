#!/usr/bin/env bash
# Builds and runs the AshRules unit tests with the strict warnings Unreal uses.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/ash_rules_tests"
CXX="${CXX:-g++}"
"$CXX" -std=c++17 -O1 -Wall -Wextra -Wshadow -Wconversion -Wno-sign-conversion -Werror -fno-exceptions -fno-rtti \
  -I "$ROOT/AshRPG/Source/AshCore/Public" \
  "$ROOT/AshRPG/Tests/Rules/RulesTests.cpp" -o "$OUT"
"$OUT"
