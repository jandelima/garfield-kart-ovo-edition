#!/bin/bash
# Compila os mods com o csc.exe que já vem no Windows e instala no jogo.
set -e
cd "$(dirname "$0")"
G="/mnt/c/Program Files (x86)/Steam/steamapps/common/Garfield Kart - Furious Racing"
M="$G/Garfield Kart Furious Racing_Data/Managed"
CSC=/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe
mkdir -p build "$G/BepInEx/plugins"
w() { wslpath -w "$1"; }
refs=()
for d in "$G/BepInEx/core/BepInEx.dll" "$G/BepInEx/core/0Harmony.dll" "$M/Assembly-CSharp.dll" "$M/Assembly-CSharp-firstpass.dll" \
         "$M"/UnityEngine*.dll "$M"/Unity.*.dll "$M/PhotonUnityNetworking.dll" "$M/PhotonRealtime.dll"; do
  refs+=("/r:$(w "$d")")
done
build() { # $1 = fonte, $2 = dll, $3... = recursos embutidos
  local src="$1" dll="$2"; shift 2
  local res=(); for r in "$@"; do res+=("/resource:$(w "assets/$r"),$r"); done
  local srcs=(); for f in $src; do srcs+=("$(w "src/$f")"); done
  "$CSC" /nologo /target:library /optimize /out:"$(w build)\\$dll" "${refs[@]}" "${res[@]}" "${srcs[@]}"
  [ "$NOINSTALL" = 1 ] || cp "build/$dll" "$G/BepInEx/plugins/"
  echo "OK: $dll"
}
# build ModAtivo.cs GKModAtivo.dll   # rótulo "MOD ATIVO" removido a pedido (10/11/2026)
build Modo200cc.cs GKModo200cc.dll icon_200cc.png icon_200cc_tab.png
build Edicao.cs GKEdicao.dll selo_ovo_edition.png
build Ovo.cs GKOvo.dll item_ovo.png ovo_mancha.png ovo_casca.png ovo_gota.png
build "PistaNova.cs PistaVulcao.cs" GKPistaNova.dll card_pista.png v3_estrada_cor.png v3_estrada_nrm.png v3_estrada_mgae.png v3_estrada_alt.png v3_basalto_cor.png v3_basalto_nrm.png v3_basalto_mgae.png v3_basalto_alt.png v3_meiofio_cor.png v3_meiofio_nrm.png v3_meiofio_mgae.png v3_meiofio_alt.png v3_ruido.png v2_rocha_facetas.png v2_rocha_encosta.png v2_cinza_areia.png v2_escoria.png v2_lava_magma.png v2_lava_crosta.png v2_fumaca.png v2_ceu.png
# Ajudante de teste: só instala com TESTE=1 ./build.sh (e precisa do arquivo BepInEx/gkmod_teste)
if [ "$TESTE" = 1 ]; then build "Teste.cs Cinema.cs CinemaExtra.cs" GKTeste.dll; elif [ "$NOINSTALL" != 1 ]; then rm -f "$G/BepInEx/plugins/GKTeste.dll"; fi
