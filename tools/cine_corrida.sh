#!/bin/bash
# Grava as cenas de corrida do trailer (200cc, Vulcão Fervente). Uso: tools/cine_corrida.sh
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
touch "$B/gkmod_vulcao" "$B/gkmod_ovo_bots"
export CINE=1 CMDMAX=4000 TMAX=2400
timeout 2500 tools/ate_pista.sh 3 \
  "ok" "espera 1.2" "hud off" \
  "cam pfixa 22 6 2.4 125 -1 2 48" "auto on" "gravar largada" "q 360" "parar" \
  "cam kart J 5 1.3 6 0 0.8 1 48 0.3" "ate_s 250" "gravar flanco" "q 220" "parar" \
  "cam kart J 0 1.8 -5 0 1.2 9 66 0.25" "ate_s 610" "gravar canion" "q 200" "parar" \
  "cam pfixa 1126 9 3 1099 0 2.2 52" "ate_s 1050" "gravar salto" "ate_s 1086" "lento 0.3" "q 210" "lento 1" "q 30" "parar" \
  "cam kart J -2 5 -11 0 1 14 60 0.35" "gravar passarela" "q 170" "parar" \
  "cam trilho 360 55 -430 150 -280 60 130 680 -310 95 -80 40 100 600 -235 48 55 -110 25 260" "gravar aereo" "q 362" "parar" \
  "hud on" "cam jogo" "mira on" "ate_s 300" "gravar ovo1" "q 20" "ovo frente" "q 4" "lento 0.4" "cam kart A 5 1.8 3 0 0.8 0 45 0.3" "q 110" "lento 1" "cam mancha 15 2.2 -3 6 55" "q 300" "parar" \
  "hud off" "mira off" "cam kart J 7 2.4 -1 0 1.6 0 55 0.3" "ate_s 820" "gravar ovo2" "ovo cima" "q 70" "cam mancha 11 3 -2 4 55" "q 260" "parar" \
  "espera 1"
tools/fechar.sh >/dev/null 2>&1
grep -E "\[gravar\]|gravação|ate_s|\[ovo\]|ovo usado|mira|Exception" "$B/LogOutput.log" | tail -60
