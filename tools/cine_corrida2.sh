#!/bin/bash
# Regrava cenas específicas de corrida do trailer.
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
touch "$B/gkmod_vulcao" "$B/gkmod_ovo_bots"
export CINE=1 CMDMAX=4000 TMAX=2400
timeout 2500 tools/ate_pista.sh 3 \
  "ok" "espera 1.2" "hud off" \
  "cam pfixa 46 5 1.7 110 -1 1.6 52" "auto on" "gravar largada" "q 380" "parar" \
  "cam pfixa 1106 21 9 1106 0 2.5 50" "ate_s 1040" "gravar salto" "ate_s 1086" "lento 0.3" "q 220" "lento 1" "q 30" "parar" \
  "cam kart J -1 3 -7.5 0 1 12 62 0.3" "gravar passarela" "q 170" "parar" \
  "hud on" "cam jogo" "mira on" "ate_s 150" "gravar ovo1" "q 10" "ovo frente espera" "q 4" "lento 0.4" "cam kart A 5 1.8 3 0 0.8 0 45 0.3" "q 120" "lento 1" "cam mancha 15 2.2 -3 6 55" "q 260" "parar" \
  "hud off" "mira off" "cam kart J 7 2.4 -1 0 1.6 0 55 0.3" "gravar ovo2" "ovo cima espera" "q 60" "cam mancha 12 2.6 -2 5 55" "q 300" "parar" \
  "espera 1"
tools/fechar.sh >/dev/null 2>&1
grep -E "\[gravar\]|gravação|ovo usado|mira no|acertando|escorregou" "$B/LogOutput.log" | tail -40
