#!/bin/bash
# Abre o jogo em modo teste, em segundo plano (janela fora da tela, sem som, sem foco).
cd "$(dirname "$0")/.."
G="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing"
B="$G/BepInEx"; L="$B/LogOutput.log"
tools/fechar.sh >/dev/null 2>&1
touch "$B/gkmod_teste"; rm -f "$B"/gkmod_foto_*.png "$B/gkmod_cmd"
rm -f "$L"
BAT=tools/lancar.bat; [ "$CINE" = 1 ] && BAT=tools/lancar_cine.bat
(cmd.exe /c "$(wslpath -w $BAT)" </dev/null >/dev/null 2>&1 &)
for i in $(seq 1 90); do grep -q "Chainloader startup complete" "$L" 2>/dev/null && break; sleep 1; done
grep -q "Chainloader startup complete" "$L" && echo "jogo aberto" || { echo "jogo NAO abriu"; tail -5 "$L"; }
