#!/bin/bash
# Manda comandos ao plugin de teste e espera terminar. Ex: cmd.sh "ok" "espera 2" "foto menu"
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"; L="$B/LogOutput.log"
OUT="${OUT:-fotos}"; mkdir -p "$OUT"
antes=$(grep -c "cmd fim" "$L")
printf '%s\n' "$@" > "$B/gkmod_cmd"
for i in $(seq 1 ${CMDMAX:-600}); do [ "$(grep -c "cmd fim" "$L")" -gt "$antes" ] && break; sleep 0.5; done
for f in "$B"/gkmod_foto_*.png; do [ -f "$f" ] && mv "$f" "$OUT/$(basename "$f" | sed 's/gkmod_foto_//')" && echo "$OUT/$(basename "$f" | sed 's/gkmod_foto_//')"; done
grep "selecionado:" "$L" | tail -1
