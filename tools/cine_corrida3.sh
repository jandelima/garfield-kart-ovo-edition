#!/bin/bash
# Última leva: ovo acertando na largada, salto em câmera lenta, karts escorregando.
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
touch "$B/gkmod_vulcao" "$B/gkmod_ovo_bots"
export CINE=1 CMDMAX=4000 TMAX=2400
timeout 2500 tools/ate_pista.sh 3 \
  "ok" "espera 1" "hud on" "cam jogo" "mira on" "auto on" "gravar ovo1" "ovo frente espera" "q 4" "lento 0.35" "cam kart A 4.5 1.6 3.5 0 0.8 0 45 0.2" "q 130" "lento 1" "q 100" "parar" \
  "hud off" "mira off" "cam kart J 3.2 1.0 -4.5 0 1.0 4 58 0.15" "ate_s 1050" "gravar salto" "ate_s 1090" "lento 0.25" "q 200" "lento 1" "q 40" "parar" \
  "espera_mancha 9000" "gravar escorrega1" "q 330" "parar" \
  "espera_mancha 9000" "gravar escorrega2" "q 330" "parar" \
  "cam kart J 7 2.4 -1 0 1.6 0 55 0.3" "gravar lance" "ovo cima" "q 120" "parar" \
  "espera 1"
tools/fechar.sh >/dev/null 2>&1
grep -E "\[gravar\]|gravação|ovo usado|mira no|acertando|espera_mancha" "$B/LogOutput.log" | tail -30
