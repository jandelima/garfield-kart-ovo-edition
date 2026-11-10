#!/bin/bash
# Teste em segundo plano: abre o jogo, entra na pista nova (aba cc = $1, padrão 3=200cc) com piloto automático.
cd "$(dirname "$0")/.."
timeout 130 tools/jogo.sh || exit 1
sleep 1
timeout 300 tools/cmd.sh "aguarda MenuHDEngagementScreen" "ok" "aguarda MenuHDMain" "ok" "aguarda MenuHDGameType" "ok" \
  "aguarda MenuHDGameMode" "cc ${1:-3}" "espera 1" "corrida" "aguarda MenuHDTrackSelection" "card nova" \
  "aguarda MenuHDKartSelection" "foto kart" "ok" "aguarda MenuHDTrackPresentation" "espera 4" "fotolimpa intro" "ok" "auto on" \
  "espera 5" "foto largada" "espera 30" "foto meio" "espera ${2:-55}" "foto fim"
