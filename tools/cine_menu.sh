#!/bin/bash
# Grava as cenas de menu do trailer (tela inicial com o selo, 200cc, Grand Grand Prix) e as legendas (fundo preto e branco).
cd "$(dirname "$0")/.."
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
export CINE=1 CMDMAX=4000
tools/fechar.sh >/dev/null 2>&1
timeout 130 tools/jogo.sh || exit 1
timeout 2400 tools/cmd.sh "aguarda MenuHDEngagementScreen" "espera 1" "selo_reinicia" "gravar titulo" "q 540" "parar" \
  "ok_ate MenuHDMain" "ok_ate MenuHDGameType" "ok_ate MenuHDGameMode" "cc 2" "espera 1.5" \
  "gravar modo200" "q 45" "cc 3" "q 220" "parar" \
  "chamar MenuHDGameMode OnSubmitChampionShip" "aguarda MenuHDTrackSelection" "espera 2" "ggp_colher" "voltar" "aguarda MenuHDGameMode" "espera 2" "cc 3" "espera 1" \
  "gravar ggp1" "q 30" "ggp_card" "q 30" "ggp_sel 150" "q 10" "parar" \
  "chamar MenuHDGameMode OnSubmitChampionShip" "gravar ggp2" "aguarda MenuHDTrackSelection" "ggp_tela" "q 300" "parar" \
  "fundo preto" "q 3" "gravar leg_pista_preto" "q 1" "legenda 180 centro 120 VULCÃO FERVENTE | NOVA PISTA" "q 182" "parar" "fundo branco" "q 3" "gravar leg_pista_branco" "q 1" "legenda 180 centro 120 VULCÃO FERVENTE | NOVA PISTA" "q 182" "parar" "fundo preto" "q 3" "gravar leg_salto_preto" "q 1" "legenda 150 centro 120 SALTE SOBRE A LAVA!" "q 152" "parar" "fundo branco" "q 3" "gravar leg_salto_branco" "q 1" "legenda 150 centro 120 SALTE SOBRE A LAVA!" "q 152" "parar" "fundo preto" "q 3" "gravar leg_ovo_preto" "q 1" "legenda 160 centro 120 O OVO! | NOVO ITEM" "q 162" "parar" "fundo branco" "q 3" "gravar leg_ovo_branco" "q 1" "legenda 160 centro 120 O OVO! | NOVO ITEM" "q 162" "parar" "fundo preto" "q 3" "gravar leg_acertou_preto" "q 1" "legenda 130 centro 120 ACERTOU? ESTOUROU!" "q 132" "parar" "fundo branco" "q 3" "gravar leg_acertou_branco" "q 1" "legenda 130 centro 120 ACERTOU? ESTOUROU!" "q 132" "parar" "fundo preto" "q 3" "gravar leg_escorrega_preto" "q 1" "legenda 160 centro 120 TODO MUNDO ESCORREGA!" "q 162" "parar" "fundo branco" "q 3" "gravar leg_escorrega_branco" "q 1" "legenda 160 centro 120 TODO MUNDO ESCORREGA!" "q 162" "parar" "fundo preto" "q 3" "gravar leg_200_preto" "q 1" "legenda 170 centro 120 200cc | NOVO MODO" "q 172" "parar" "fundo branco" "q 3" "gravar leg_200_branco" "q 1" "legenda 170 centro 120 200cc | NOVO MODO" "q 172" "parar" "fundo preto" "q 3" "gravar leg_ggp_preto" "q 1" "legenda 170 centro 120 GRAND GRAND PRIX | NOVO MODO" "q 172" "parar" "fundo branco" "q 3" "gravar leg_ggp_branco" "q 1" "legenda 170 centro 120 GRAND GRAND PRIX | NOVO MODO" "q 172" "parar" "fundo preto" "q 3" "gravar leg_campeao_preto" "q 1" "legenda 210 centro 120 4 COPAS. 16 PISTAS. 1 CAMPEÃO." "q 212" "parar" "fundo branco" "q 3" "gravar leg_campeao_branco" "q 1" "legenda 210 centro 120 4 COPAS. 16 PISTAS. 1 CAMPEÃO." "q 212" "parar" "fundo nada"
tools/fechar.sh >/dev/null 2>&1
grep -E "\[gravar\]|gravação|ggp|selo|Exception" "$B/LogOutput.log" | tail -40
