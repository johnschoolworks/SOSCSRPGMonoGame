#!/usr/bin/env bash
set -uo pipefail
ROOT="${1:-$PWD}"
ROOT="$(realpath "$ROOT")"
T="$ROOT/Tutorial"
REPORT="$ROOT/audit-results"
mkdir -p "$REPORT/logs" "$REPORT/work"
RESULTS="$REPORT/results.tsv"
printf 'chapter\tproject\trestore\tbuild\ttests\tsmoke\tstatus\n' > "$RESULTS"
if ! command -v dotnet >/dev/null; then echo 'ERROR: dotnet not installed/in PATH' >&2; exit 2; fi
if ! command -v unzip >/dev/null; then echo 'ERROR: unzip required' >&2; exit 2; fi
if ! command -v timeout >/dev/null; then echo 'ERROR: GNU timeout required' >&2; exit 2; fi
if [[ ! -d "$T" ]]; then echo "ERROR: Missing $T" >&2; exit 2; fi
for i in $(seq -w 1 20); do
  n="$(printf '%03d' "$((10#$i))")"
  project="$T/Tutorial$n"
  if [[ ! -d "$project" ]]; then
    zipfile=""
    for candidate in "$T/SOSCSRPG-Tutorial$n-Source.zip" "$T/SOSCSRPG-CSharp-Tutorial${n#0}.zip" "$T/SOSCSRPG-CSharp-Tutorial$n.zip"; do
      if [[ -f "$candidate" ]]; then zipfile="$candidate"; break; fi
    done
    if [[ -n "$zipfile" ]]; then
      mkdir -p "$REPORT/work/$n"
      if ! unzip -oq "$zipfile" -d "$REPORT/work/$n"; then
        printf '%s\t-\tFAIL\tNOT_RUN\tNOT_RUN\tNOT_RUN\tFAIL_ARCHIVE\n' "$n" >> "$RESULTS"; continue
      fi
      project="$REPORT/work/$n/Tutorial$n"
    fi
  fi
  if [[ ! -d "$project" ]]; then
    printf '%s\t-\tNOT_RUN\tNOT_RUN\tNOT_RUN\tNOT_RUN\tMISSING_SOURCE\n' "$n" >> "$RESULTS"; continue
  fi
  mapfile -t projects < <(find "$project" -name '*.csproj' -not -path '*/obj/*' -not -path '*/bin/*' | sort)
  if (( ${#projects[@]} == 0 )); then
    printf '%s\t-\tNOT_RUN\tNOT_RUN\tNOT_RUN\tNOT_RUN\tMISSING_CSPROJ\n' "$n" >> "$RESULTS"; continue
  fi
  for csproj in "${projects[@]}"; do
    label="$(basename "$(dirname "$csproj")")"
    key="${n}_${label}"
    restore=PASS; build=NOT_RUN; tests=NOT_APPLICABLE; smoke=NOT_RUN
    if ! dotnet restore "$csproj" --nologo >"$REPORT/logs/${key}_restore.log" 2>&1; then restore=FAIL
    else
      build=PASS
      if ! dotnet build "$csproj" --no-restore --nologo >"$REPORT/logs/${key}_build.log" 2>&1; then build=FAIL; fi
    fi
    if [[ "$build" == PASS ]]; then
      if [[ "$label" == RegressionTests || "$label" == *Tests ]]; then
        tests=PASS
        if ! timeout 45s dotnet run --project "$csproj" --no-build >"$REPORT/logs/${key}_tests.log" 2>&1; then tests=FAIL; fi
        smoke=NOT_APPLICABLE
      elif [[ "$label" == RpgEngine ]]; then
        smoke=NOT_APPLICABLE
      else
        smoke=NOT_TESTED_INTERACTIVE
        # Smoke test only early, noninteractive tutorials. Interactive games need manual walkthroughs.
        if (( 10#$n <= 7 )); then
          smoke=PASS
          if ! timeout 8s bash -c 'printf "quit\nexit\n" | dotnet run --project "$1" --no-build' _ "$csproj" >"$REPORT/logs/${key}_smoke.log" 2>&1; then smoke=FAIL; fi
        fi
      fi
    fi
    status=PASS_BUILD
    [[ "$restore" == FAIL || "$build" == FAIL || "$tests" == FAIL || "$smoke" == FAIL ]] && status=FAIL
    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$n" "$label" "$restore" "$build" "$tests" "$smoke" "$status" >> "$RESULTS"
  done
done
python3 - "$RESULTS" "$REPORT/summary.md" <<'PY'
import csv,sys
rows=list(csv.DictReader(open(sys.argv[1]),delimiter='\t'))
with open(sys.argv[2],'w') as f:
 f.write('# SOSCSRPG 001–020 automated audit\n\nBuild PASS does not imply gameplay PASS. Interactive gameplay and feature parity require manual checks.\n\n')
 f.write('| Chapter | Project | Restore | Build | Tests | Smoke | Status |\n|---|---|---|---|---|---|---|\n')
 for r in rows: f.write('| '+' | '.join(r.values())+' |\n')
 f.write('\n## Manual gameplay regression checklist\n\n')
 for s in ['World navigation including invalid directions','NPC dialogue and quest acceptance','Quest objective tracking and completion','Monster combat including defend/dodge/critical hits','Guaranteed Spider Silk quest drops','Inventory quantities and item use','Trading affordability and transactions','Save/load round-trip including encounter rewards','Verify 017 → 018 → 019 → 020 feature parity','Git status clean and release archive integrity']:
  f.write('- [ ] '+s+'\n')
print('Wrote',sys.argv[2])
PY
printf '\nAudit report: %s\n' "$REPORT/summary.md"
column -t -s $'\t' "$RESULTS" 2>/dev/null || cat "$RESULTS"
