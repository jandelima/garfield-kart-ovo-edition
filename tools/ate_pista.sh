#!/bin/bash
# Abre o jogo em segundo plano e vai até a apresentação da pista nova. Uso: ate_pista.sh [aba_cc] [comandos extras...]
cd "$(dirname "$0")/.."
cc=${1:-2}; shift
timeout 130 tools/jogo.sh || exit 1
sleep 1
timeout ${TMAX:-400} tools/cmd.sh "aguarda MenuHDEngagementScreen" "ok_ate MenuHDMain" "ok_ate MenuHDGameType" "ok_ate MenuHDGameMode" \
  "cc $cc" "espera 1" "corrida" "aguarda MenuHDTrackSelection" "card nova" "aguarda MenuHDKartSelection" "ok_ate MenuHDTrackPresentation" "espera 2" "$@"
