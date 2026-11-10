# Backlog — Garfield Kart Ovo Edition

Lista viva de tarefas. Atualizada a cada sessão de trabalho: item feito vai para **Feito** com a data; item rejeitado vai para **Rejeitado**.

- **Esforço:** P = pequeno, M = médio, G = grande.
- **Confiança:** *confirmado* = visto no código ou nos arquivos; *provável* = deduzido do código, falta ver no jogo.

Para pedir, use o código. Exemplo: "faz o B1 e o M3".

## Correções (bugs)

- [ ] **B1** (P, *confirmado*): o card da pista no menu ainda mostra a foto do oval verde antigo, mas a pista agora é o vulcão.
- [ ] **B2** (P, *provável*): os sons de ambiente da cena original (cachorro latindo, pássaros) devem tocar no vulcão.
  - Trocar por vento de caverna e lava borbulhando.
- [ ] **B3** (P, *provável*): a música da corrida no vulcão deve ser a do bairro.
  - Usar a música do deserto do jogo, ou uma gerada em IA de música.
- [ ] **B4** (M, *provável*): o voo de câmera antes da largada segue o caminho feito para a cidade original.
  - Fazer um voo próprio pelo vulcão, o lago e o salto.
- [ ] **B5** (M, *confirmado*): Grand Prix em 200cc grava e libera como 150cc.
  - Ganhar em 200cc dá o troféu do 150cc.
- [ ] **B6** (P, *confirmado*): a descrição do 200cc é a mesma do 150cc ("Participate in a difficult race").
- [ ] **B7** (M, *confirmado*): os bots usam o ovo mal.
  - A IA ainda calcula como se fosse o diamante, e eles só jogam para cima.
  - Ensinar a mirar para a frente quando houver kart à vista.

## Melhorias

- [ ] **M1** (M): ver bem a pista no vulcão. Hoje a pista e a área de fora têm quase a mesma cor, e os karts somem.
  - Balizadores brilhando nas bordas e tochas nas curvas.
  - Um pouco mais de luz.
- [ ] **M2** (M/G): cenário mais rico.
  - Formações de rocha e cristais de obsidiana.
  - Arcos de pedra sobre a pista e placas de curva.
  - Bolhas nos lagos de lava.
- [ ] **M3** (P/M): o ovo frita no chão quente.
  - A mancha cai crua e vira ovo frito, com chiado.
  - Nas outras pistas fica cru.
- [ ] **M4** (P): mais retorno visual do ovo.
  - Sombra no chão mostrando onde o ovo jogado para cima vai cair.
  - Aviso "ACERTOU!" para quem jogou.
- [ ] **M5** (M): testar o 200cc nas 16 pistas originais com piloto automático.
  - Ajustar as curvas onde os bots batem ou caem.
- [ ] **M6** (P): testar se ovo, 200cc e vulcão funcionam na tela dividida local.
  - Seria um jeito rápido de jogar com amigos na mesma sala.
- [ ] **M7** (M): contra-relógio no Vulcão Fervente, com recorde salvo.
- [ ] **M8** (M): menu "Ovo Edition" dentro do jogo para ligar e desligar ovo, vulcão e 200cc.
  - Hoje isso depende de arquivos escondidos na pasta do jogo.
- [ ] **M9** (P): pacote de instalação (.zip com tudo) para mandar aos amigos.
  - Pode virar uma Release aqui no GitHub.

## Ambiciosas

- [ ] **A1** (G): Grand Grand Prix de verdade.
  - 16 pistas, pontos acumulados, pódio final e troféu próprio.
  - Hoje existe só no trailer.
- [ ] **A2** (G): online com amigos.
  - Sincronizar o voo do ovo e o 200cc.
  - Todos precisam ter o mod; quem não tem vê um diamante.
- [ ] **A3** (G): "Copa Ovo", com o Vulcão e mais 3 pistas próprias.
- [ ] **A4** (G): vulcão vivo.
  - Erupções que lançam bolas de lava, com sombra de aviso.
  - Gêiseres que jogam o kart para o alto.
  - Uma ponte que desaba na última volta.
- [ ] **A5** (M/G): um atalho arriscado, como um túnel de lava ou a borda da cratera.
- [ ] **A6** (G): tradução do jogo inteiro para português (o jogo não tem pt-BR).
- [ ] **A7** (M): mais itens da família do ovo.
  - Uma dúzia (3 ovos seguidos).
  - Um ovo surpresa.
- [ ] **A8** (M): modo foto ou replay com as câmeras do trailer.

**Sugestão de ordem:**
1. B1 a B4 juntos. Tiram a cara de pista adaptada e cabem numa sessão.
2. Depois M1 e M3, que dão o maior ganho de beleza.

## Feito

(vazio: o histórico anterior está em [CHANGELOG.md](CHANGELOG.md))

## Rejeitado

(vazio)
