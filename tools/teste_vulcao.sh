#!/bin/bash
# Teste em segundo plano da pista do vulcão: vistas aéreas + corrida com piloto automático. Uso: teste_vulcao.sh [aba_cc] [seg_corrida]
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
touch "$B/gkmod_vulcao"
timeout 500 tools/ate_pista.sh ${1:-2} "espera 2" \
  "vista -520 260 -520 0 30 60 v_geral 55" \
  "vista -190 24 -10 60 140 680 v_largada 60" \
  "vista 70 22 -215 23 2 -163 v_salto 60" \
  "vista 12 30 -75 10 28 30 v_cachoeira 60" \
  "vista 200 32 10 205 16 160 v_canion 60" \
  "vista 0 45 120 60 160 680 v_encosta 60" \
  "vista -330 60 -200 -345 0 60 v_lagooeste 60" \
  "vista 100 7 -150 0 4 -166 v_passarela 62" \
  "vista -60 40 260 200 20 120 v_flanco 60" \
  "fotolimpa intro" "ok" "auto on" "espera 6" "foto largada" "espera 14" "foto c1" "espera 14" "foto c2" "espera 14" "foto c3" "espera 14" "foto c4" "espera ${2:-20}" "foto c5"
