#!/bin/bash
# Fecha o jogo e restaura as configurações de tela originais do usuário.
cd "$(dirname "$0")/.."
taskkill.exe /IM "Garfield Kart Furious Racing.exe" >/dev/null 2>&1
for i in $(seq 1 20); do tasklist.exe 2>/dev/null | grep -qi "Garfield Kart" || break; sleep 0.5; done
taskkill.exe /F /IM "Garfield Kart Furious Racing.exe" >/dev/null 2>&1
reg.exe import "$(wslpath -w backup/registro_original.reg)" >/dev/null 2>&1 && echo "fechado; registro restaurado"
B="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing/BepInEx"
rm -f "$B/gkmod_autodrive" "$B/gkmod_cmd"
