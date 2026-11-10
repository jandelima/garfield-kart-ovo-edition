# Garfield Kart Ovo Edition

Modpack para **Garfield Kart - Furious Racing** (Steam), feito com [BepInEx 5](https://github.com/BepInEx/BepInEx).

Projeto de fãs, sem ligação com a Microids. Você precisa ter o jogo original. Este repositório não contém código nem arquivos do jogo.

## O que o modpack traz

- **Item Ovo:** substitui o diamante.
  - Jogue para a frente em arco, ou para cima.
  - Quem é acertado roda na hora.
  - O ovo deixa uma mancha no chão por 8 s, e quem passa por ela escorrega.
- **Modo 200cc:** aba nova ao lado de 150cc, com todos os karts mais rápidos.
- **Pista nova "Vulcão Fervente":**
  - lava, salto sobre o lago e vulcão fumegante;
  - fica em Corrida Única, num card extra na escolha de pista.
- **Selo "OVO EDITION"** na tela inicial.

O que vem por aí está em [BACKLOG.md](BACKLOG.md). O que já mudou está em [CHANGELOG.md](CHANGELOG.md).

## Instalar (jogador)

1. Instale o **BepInEx 5 x64** na pasta do jogo.
   - A pasta é `...\steamapps\common\Garfield Kart - Furious Racing`.
   - Ao abrir o jogo uma vez, ele cria `BepInEx\plugins`.
2. Copie as DLLs para `BepInEx\plugins`:
   - `GKEdicao.dll`
   - `GKModo200cc.dll`
   - `GKOvo.dll`
   - `GKPistaNova.dll`
3. Para a pista ser o vulcão, crie um arquivo vazio chamado `gkmod_vulcao` em `BepInEx\`. Sem ele, a pista nova é um oval verde.

Por enquanto o modpack funciona **offline**. Online ainda não é suportado.

## Compilar (desenvolvimento)

Precisa de WSL ou bash no Windows, com o jogo instalado no caminho padrão da Steam.

```bash
./build.sh                # compila com o csc.exe do Windows e instala em BepInEx/plugins
NOINSTALL=1 ./build.sh    # só compila (em build/)
TESTE=1 ./build.sh        # inclui o plugin de teste/filmagem (GKTeste.dll)
```

- **Linguagem:** C# 5, sem `?.` e sem interpolação de strings.
- **Texturas:** as próprias são geradas por scripts Python em `tools/make_*.py` (Pillow e numpy) e embutidas nas DLLs.
- **Texturas recoloridas:** algumas (`assets/v_*`) partem de texturas do jogo. Os originais extraídos não estão no repositório.

## Estrutura

| Pasta/arquivo | O que é |
|---|---|
| `src/Ovo.cs` | item ovo |
| `src/Modo200cc.cs` | modo 200cc |
| `src/PistaNova.cs` | card da pista nova, troca da cena, traçado, caminhos da IA |
| `src/PistaVulcao.cs` | visual e física da pista do vulcão |
| `src/Edicao.cs` | selo "OVO EDITION" |
| `src/Teste.cs`, `src/Cinema*.cs` | testes em segundo plano e filmagem do trailer (só com `TESTE=1`) |
| `tools/` | geração de texturas, automação de testes, montagem do trailer |
| `assets/` | texturas e ícones embutidos nas DLLs |
