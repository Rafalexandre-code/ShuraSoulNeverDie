# Relatórios do redesenho "Ponta-Cabeça"

Um relatório por pacote de trabalho, no formato A.5 do plano (`docs/10-Plano-Redesenho-Ponta-Cabeca.md`).

---

```
WP-00 — Feature flags e linha de base — v0.16.0
Feito:
  - Config/Feature.cs: classe estática com as 33 flags do plano (padrão = design novo;
    só ThirdWeapon = true). Ordem de leitura: padrões → StreamingAssets/features.json →
    "-shuraFeatures legacy|novo" na linha de comando. Get/Set/SetAll/Reset/Apply/Save e
    lista de nomes para o painel do WP-02.
  - StreamingAssets/features.json com todos os padrões.
  - O enum antigo "Feature" (camadas de liberação, Meta/Unlocks.cs) foi renomeado para
    "Unlockable" para o nome ficar livre para a classe do plano (troca mecânica, 11 arquivos).
  - Linha de base: AutoDriver registra por sala o tempo de simulação e as teclas usadas
    ("[Shura][Base]"); InputBuffer conta os apertos por ação. docs/baseline/ com o resumo
    e o CSV por sala dos 4 robôs de referência.
  - Determinismo: as brasas do Kagutsuchi usavam UnityEngine.Random; agora usam a semente.
  - Tag git v0.15.0-baseline no commit da v0.15.0 (91bcc30).
Flags novas: as 33 do plano (Stamina=false, LocalWounds=false, KiSealsBase=false,
  KamaeSwitch=false, CursedRing=false, DoubleRingType=false, FlowTimeScale=false,
  ShuraMeterBase=false, LegacyToolsAndKeys=false, NamedLinks45=false, StatLoot=false,
  ItemClans=false, Blessings=false, ForgeHandles=false, ExtraWeapons=false, AgeStats=false,
  SenseiStats=false, ParryWindowModifiers=false, RealtimeDayClock=false, StormWeather=false,
  InfiniteRunDefault=false, ExtraEnemies=false, AiReadsExtended=false, RivalHierarchy=false,
  BrowserClient=false, PartyMode8=false, BloodPact=false, AsyncPresence=false,
  PvpRanked=false, LegacyModes=false, LegacyTemple=false, LegacyCurrencies=false,
  ThirdWeapon=true)
Arquivos principais: Config/Feature.cs, StreamingAssets/features.json, Meta/Unlocks.cs (+10
  usos), Input/InputBuffer.cs, DevTools/AutoDriver.cs, Run/World.Bosses.cs,
  Tests/EditMode/FeatureFlagTests.cs, docs/baseline/
Testes: 197 → 201 (todos passando: s)
Bots: sementes 3, 11, 29, 41, alvo prof. 20, 0 exceções com as flags no padrão e 0 exceções
  com "-shuraFeatures legacy" (todas ligadas). Nenhum código de jogo lê as flags ainda, então
  o jogo é idêntico à v0.15.0 por construção.
Capturas: nenhuma tela foi afetada.
Números alterados: nenhum.
Pendências para o diretor: nenhuma decisão nova.
Riscos percebidos:
  - Os robôs NÃO são determinísticos entre execuções: o relógio de 24 h avança por tempo
    real (Game.Simulate → Run.Clock.Advance(Time.unscaledDeltaTime)), então hora, clima e
    elenco variam com a velocidade da máquina. Comparações de robô devem usar médias até o
    WP-12 (hora fixa por ato) remover essa fonte.
  - O ambiente de verificação perdeu arquivos de pacotes (Input System, Test Framework) após
    uma falha de licença do Unity (código 198); foi restaurado copiando o PackageCache do
    projeto principal. Nada no projeto do diretor foi alterado.
```

---

```
WP-01 — Telemetria local — v0.16.1
Feito:
  - Core/TelemetryService.cs: um .jsonl por run em persistentDataPath/telemetry/
    (robôs: telemetry_bots/; "-shuraTelemetry <pasta>|off" na linha de comando).
    Eventos com t (quadros), runId, seed, depth, act, room: run_start, ring_resolved
    (anel, inimigo, resposta parry/block/dodge/swap/hit, delta em quadros do fechamento,
    sucesso), player_hit (fonte, dano, anel, anel visível na tela), death (fonte, tempo,
    build), reward_offered/reward_picked, route_choice, input_use (por sala), room_end
    (duração, dano, curas, finalizações por tipo e nível), run_end (resultado, duração,
    Algoz criado).
  - Ganchos novos na simulação: World.AttackResolved (PlayerController.ReceiveAttack
    agora mede os quadros desde o aparar, a troca e o início da esquiva) e
    World.FinisherResolved (corte dirigido: perfeito/linha/limpo/falho).
  - Aviso ao jogador na primeira run e opção "Telemetria local" em Configurações → Jogo.
  - Tools/telemetry_report/telemetry_report.py: relatório Markdown + CSVs (histograma de
    delta por anel, letalidade por exposição, pick rate e win rate, durações, teclas,
    golpes com atacante fora da tela, bloqueio dominante). Amostra com 2 robôs em
    docs/redesenho/amostras/telemetria-bots-wp01.md.
  - Até o WP-12 não há atos: o campo "act" é o bioma de 5 salas (prof. ÷ 5 + 1).
Flags novas: nenhuma (a telemetria é opção do jogador: GameSettings.telemetry = true).
Arquivos principais: Core/TelemetryService.cs, Core/Game.cs, Entities/PlayerController.cs,
  Entities/PlayerController.Ki.cs, Run/World.cs, Run/Difficulty.cs, UI/GameSettings.cs,
  UI/GameUI.cs, UI/Strings.Redesign.cs, Tools/telemetry_report/telemetry_report.py,
  Tests/EditMode/TelemetryTests.cs
Testes: 201 → 202 (todos passando: s)
Bots: sementes 3 e 11 até a prof. 20 com telemetria (2 arquivos válidos, 160 e 202
  eventos, 0 exceções); semente 29 com "-shuraTelemetry off" não gravou nada; semente 41
  sem opção gravou em telemetry_bots (prova de que o "off" funciona).
Capturas: nenhuma tela nova (só um item de opção e um aviso).
Números alterados: nenhum.
Pendências para o diretor: nenhuma.
Riscos percebidos:
  - O robô apara sempre +2 quadros depois do fechamento do anel (padrão dele); os
    histogramas dos robôs não dizem nada sobre humanos.
  - "ring_visible" fica nulo nos robôs headless (sem câmera); só é medido com tela.
  - Online, só o anfitrião grava (os convidados não rodam a simulação).
```

---

```
WP-02 — Painel de afinação e Balance.json — v0.16.2
Feito:
  - StreamingAssets/Balance.json (lido no início por Config/Balance.cs, por cima dos
    padrões do código): seções player, enemies, flow e feel do GameConfig (janela de
    parry, anti-spam, buffers, anel 36 f / 4 f, hit-stop e tremor por evento, câmera
    lenta, ganho de Fluxo, postura por evento...), mais "scaling" (escala por
    profundidade e de elite, antes constantes em DifficultyRules) e "enemyOverrides"
    (vida/postura/dano por inimigo, multiplicadores). Floats arredondados no arquivo.
  - Cura do gole da cabaça saiu de uma constante para player.sipHeal (35).
  - Menu de editor "Shura/Escrever Balance.json com os padrões" (gera o arquivo do código).
  - DevTools/TuningPanel.cs (F1, só editor e build de desenvolvimento): abas Afinação
    (edição ao vivo com −/+, salvar e recarregar Balance.json), Flags (liga/desliga,
    salvar features.json, padrão novo, tudo v0.15) e Ferramentas (hitboxes, pular sala,
    matar todos, invencível, criar qualquer inimigo perto do jogador). "-shuraPanel" abre
    o painel para capturas.
Flags novas: nenhuma.
Arquivos principais: Config/Balance.cs, Config/GameConfig.cs, Run/Difficulty.cs,
  Entities/Enemy.cs, Entities/PlayerController.Ki.cs, Run/World.cs, Core/Game.cs,
  DevTools/TuningPanel.cs, Editor/ProjectSetup.cs, StreamingAssets/Balance.json,
  Tests/EditMode/BalanceTests.cs
Testes: 202 → 204 (todos passando: s)
Bots: aceite medido em build de desenvolvimento. Com Balance.json padrão o robô acertou
  54 de 66 parries tentados em anéis ciano (telemetria do WP-01); com
  player.perfectParryFrames = 1 no arquivo, 1 de 27, e o log confirma a leitura
  ("janela de parry 1 f"). 0 exceções.
Capturas: docs/redesenho/amostras/wp02-painel.png (painel aberto durante a luta).
Build final: a DLL do runtime não contém "TuningPanel" (0 ocorrências; controle:
  "DebugOverlay" aparece), então o painel não existe na release.
Números alterados: nenhum (todos os valores do arquivo = padrões do código).
Pendências para o diretor: nenhuma.
Riscos percebidos:
  - No editor, o GameConfig é um asset: editar no painel durante o Play altera o asset
    em memória (comportamento normal do Unity); o arquivo de verdade é o Balance.json.
  - Vida, dano e postura base de cada inimigo continuam em EnemyDefs (código); o
    Balance.json ajusta por multiplicador. Mover as 36 definições inteiras para dados
    seria outro pacote.
```

---

```
WP-03 — Núcleo do combate: cortar e fundir — v0.16.3
Feito:
  - Fôlego (Stamina=false): ataque, esquiva e bloqueio sem custo; sem exaustão nem Brilho
    do Respiro; barra de fôlego fora do HUD. Conversões (tabela E.2.2): 3 parries perfeitos
    seguidos fazem o inimigo cambalear 20 f; no Gelo a SUA postura se recupera 40% mais
    devagar; o Ittan-momen enche a sua postura (escape pelo Arremesso); na névoa corrompida
    a postura dos humanos não se recupera e os não humanos causam +25% de dano.
  - Janela constante (ParryWindowModifiers=false): Combat/ParryWindow.cs; nada da build
    mexe na janela (kamae, idade, Altar, relíquias, tsubas, itens, técnicas, exaustão,
    Mushin). Perdão de fim de janela (D-11): +2 f quando o anel ficou visível > 30 f.
  - Fluxo sem acelerar a simulação (FlowTimeScale=false): ritmo fixo 1,0; o Fluxo encurta
    só a recuperação dos golpes do jogador (6% por nível).
  - Ferimentos (LocalWounds=false): fora do jogador e do inimigo; bandagem sem efeito;
    boneco de ferimentos fora do HUD. O desarme do Mikiri continua.
  - Kamae (KamaeSwitch=false): guarda fixa por arma (tabela E.2.5: Katana e Naginata
    Chūdan; Montante, Kanabō e Shakujō Jōdan; Gêmeas, Kusarigama e Wakizashi Waki;
    Manoplas e Tessen Gedan); as teclas 1–4 deixam de trocar; a guarda vira só pose e
    cortes (sem bônus numérico).
  - Selos (KiSealsBase=false): parry não marca selo; sem tecla G nem acorde
    ataque+aparar. Técnica nova "Selo Interno" (ensinada pelo Onmyōji no lugar do Selo
    Explosivo): parry marca um selo; no 5º (4º na Forma Plena) explodem sozinhos; Eco marca
    o inimigo vizinho.
  - Mikiri (flag nova SplitCounterAndStomp=false): contra-investida e Pisão viraram uma
    resposta só (atravessa, desarma 60 f, +40 de postura). Descida e Arremesso mantidos.
  - Pedido do diretor: a trava de mira deixou de desenhar anéis sobre o alvo; agora são
    dois colchetes finos nas pontas da barra de vida e um losango pequeno.
Flags novas: SplitCounterAndStomp=false (Pisão separado; o plano manda todo corte atrás
  de flag e esta faltava na lista do WP-00).
Arquivos principais: Combat/ParryWindow.cs, Entities/PlayerController.cs,
  Entities/PlayerController.Ki.cs, Entities/Enemy.cs, Entities/Enemy.Ki.cs,
  Run/World.Conditions.cs, Run/World.Ki.cs, Input/InputBuffer.cs, Core/Game.cs,
  Content/EnemyDefs.cs (Teaches), Content/NewTechniques.cs (InnerSealTechnique),
  Content/RewardDefs.cs, UI/GameUI.Hud.cs, UI/GameUI.Controls.cs (trava), StreamingAssets
  (Balance.json e features.json), Tests/EditMode/CoreCombatRedesignTests.cs
Testes: 204 → 209 (todos passando: s). Ajustados 2 testes que verificavam o antigo
  (acorde de detonar e "cada técnica tem um professor"), agora nos dois estados da flag.
  Teste novo da janela constante: aplica TODAS as técnicas (níveis I–III, os dois ramos),
  relíquias, bênçãos, tsubas, Altar no máximo, traços e idade, mais valores extremos.
Bots: sementes 3, 11, 29, 41 até a prof. 20, 0 exceções. Profundidade 20/19/19/18
  (linha de base: 20/14/9/14): sem fôlego e ferimentos o robô vai mais fundo.
Capturas: docs/redesenho/amostras/wp03-hud.png (cartão sem fôlego e sem boneco de
  ferimentos; trava de mira discreta).
Números alterados:
  | Valor | Antes | Depois |
  | Ritmo da simulação | 0,82 → 1,03 pelo Fluxo | 1,0 fixo |
  | Janela de parry | 9 ± build (até ÷2 exausto) | 9 fixo (+2 f de perdão em anéis lidos) |
  | Contra-investida | 45 postura (×1,3 em Chūdan) | Mikiri: 40 postura + desarme 60 f |
  | Custo de ataque/esquiva | 8 / 15 de fôlego | 0 |
Pendências para o diretor:
  - Cartas que prometem mudar a janela (Ouvido Interior, Tsuba da Garça, Olhar Afiado do
    Altar, idade, alguns afixos) continuam com o texto antigo, mas não mudam mais nada. O
    plano as redesenha no WP-08 (tsubas), WP-09 (técnicas), WP-10 (relíquias) e WP-11
    (Altar e idade). Até lá o texto engana: decidir se escondo essas cartas antes do
    Playtest 1.
  - As 4 figurinhas de guarda e a bandagem (H) ainda aparecem no HUD; saem no WP-05
    (teclas) e no WP-15 (HUD).
  - O Santuário ainda oferece "Guarda padrão", que não tem efeito sem a troca de kamae
    (WP-12 revê as salas).
Riscos percebidos:
  - Sem fôlego e sem ferimentos o jogo ficou mais fácil para o robô; o balanceamento de
    dano dos inimigos precisa ser revisto com o painel (WP-02) e no WP-20.
```

---

```
EXPERIMENTO DO DIRETOR — Trilha de ritmo (estilo BPM) — v0.16.4
Fora do plano: pedido direto do diretor, só para testar se o combate fica mais ritmado.
Feito:
  - Trilha embaixo, no centro da tela (UI/GameUI.Rhythm.cs). Os golpes que miram você
    entram pela esquerda ~54 quadros antes e cruzam o marcador central no instante ideal
    da resposta (o mesmo instante do aviso de tempo do anel):
      1 barra azul = aparar · 2 vermelhas = esquivar · 2 laranja = Troca Perfeita ·
      1 cinza tracejada = finta (espere) · 1 verde = amaldiçoado (aparar; não bloquear).
    Embaixo do marcador aparece a tecla da próxima resposta.
  - Depois de um parry perfeito, uma barra dourada vem da DIREITA e chega ao centro 16
    quadros depois: atacar com ±5 quadros dela dá "NO TEMPO!" e o primeiro golpe que acertar
    faz ×1,5 de postura e ×1,2 de dano. Fora da janela: "fora do tempo", sem bônus.
  - O marcador acende com a última batida (PERFEITO, TROCA, ESQUIVOU, bloqueou, levou).
  - Opções → "Trilha de ritmo (teste)" liga/desliga (vem ligada). Desligada, some a trilha e
    o bônus do contra-ataque. Só o anfitrião / jogo solo.
Arquivos: Run/World.Rhythm.cs (relógio, batidas, bônus), UI/GameUI.Rhythm.cs (desenho),
  Run/World.cs (ligações), Entities/PlayerController.cs (golpe iniciado), Core/Game.cs,
  UI/GameSettings.cs, UI/GameUI.cs (opção), UI/Strings.Redesign.cs.
Testes: 209/209. Bots: sementes 3, 11, 29, 41 — prof. 20/19/14/18, 0 exceções.
Capturas: docs/redesenho/amostras/ritmo-trilha.png (os 4 estados) e ritmo-tela.png.
Números (constantes de teste em World.Rhythm.cs; vão para o Balance.json se o diretor
  aprovar): atraso da batida 16 f, tolerância ±5 f, postura ×1,5, dano ×1,2, antecedência
  da trilha 54 f.
Para o diretor decidir: fica, sai ou muda? Se ficar, sugiro trocar os anéis sobre os
  inimigos pela trilha em uma flag (a regra A.2.8 pede tirar algo quando se acrescenta um
  medidor no HUD).
```

---

```
PEDIDO DO DIRETOR — Baú amaldiçoado vivo (mímico) — v0.16.5
Fora do plano: o baú estava feio e sem corpo.
Arte (regra nova do diretor: procurar arte gratuita antes de desenhar; se nada servir
  perfeitamente, partir da mais próxima e desenhar/animar por cima):
  - Pesquisados: "Chest and Mimic" (Redshrike/IndigoFenix, OpenGameArt, CC-BY 3.0: pequeno
    e com pouca cara de criatura), "Mimic + Chest" (Toy Master: sem licença escrita) e
    "Chest Mimic" (joraxus, itch.io: uso comercial e modificação livres, sem redistribuição
    do pacote). Escolhido o do joraxus: respira, lambe, mastiga e dá o bote com a língua.
  - Por cima: madeira recolorida em roxo, boca carmim, a fechadura virou um olho verde-ácido
    com pupila em fenda, quadro novo de piscar (Tools/art_mimic/pack.py →
    Resources/Art/Extra/mimic_purple.png). Desenhado no jogo: sombra, aura que pulsa,
    fiapos de maldição saindo da boca, brilho do olho e a maldição — um crânio em chamas
    roxas (3 quadros) com rastro.
Feito:
  - Feel/MimicChest.cs: parado ele respira e às vezes pisca, lambe ou mastiga (e olha para
    o outro lado). Ao abrir: vira para o jogador, abre a bocarra, cospe o crânio num arco
    até o jogador; o aviso "MALDIÇÃO: …" e o efeito saem quando ele acerta. Depois fica
    saciado (aura fraca) e volta a descansar. Em grupo, cada jogador que abre leva o seu.
  - Corpo sólido (Offering.Solid, raio 0,85): ninguém atravessa; usa-se encostado (o
    alcance cresce com o corpo: World.ReachOf). Sem o anel roxo no chão.
  - O item épico salta do baú, não do jogador. A regra (item e maldição) não mudou.
  - Créditos: CREDITS.md e tela de créditos.
Testes: 209 → 212 (folha existe, criatura monta, só o mímico tem corpo). Bots: 3, 11, 29, 41
  — prof. 20/19/16/18, 0 exceções (a 29 passou por um baú amaldiçoado).
Capturas: docs/redesenho/amostras/mimico-bau.png (parado, bote, maldição) e mimico-folha.png.
Limite: o convidado online vê o mímico parado e animado, mas não o bote (o anfitrião vê).
Atenção (licença): o pacote do joraxus não pode ser redistribuído, e o repositório no GitHub
  é público. Decisão do diretor antes de enviar.
```

---

```
PEDIDOS DO DIRETOR — Armadilhas samurai, floresta ao luar e ícones das maldições — v0.16.6
Fora do plano.
Armadilhas (base: "aMazing Maze Sprites" de TimberwolfGames, recolorido e redesenhado):
  - Estacas -> takeyari: fosso com moldura de madeira (uma moldura só por fora em fossos de
    várias células); no aviso as pontas de bambu tremem e avermelham; ativas, brotam em 4
    quadros, ficam e se recolhem. Erguidas, ficam de pé entre os lutadores.
  - Jato de fogo -> oni de pedra (olhos e boca em brasa no aviso) com chão chamuscado na faixa.
  - Lâmina giratória -> duas katanas presas a uma tsuba sobre pedestal de pedra, com rastro do
    giro e aço que se suja de sangue a cada golpe (TrapDef.Hits, só visual). Sem o círculo no
    chão. Quem encosta é ARREMESSADO para fora do eixo, no sentido do giro (jogador: 28 u/s,
    ~2 unidades; inimigo: empurrão de 2,2). Números em GameConfig.player / Balance.json.
Bosque Selvagem ao luar (referência do diretor: mata noturna azul):
  - Natureza do Ninja Adventure recolorida para a noite (tiles_nature_night.png): copas
    verde-azuladas, troncos de ardósia, cerejeiras rosa acesas com halo.
  - Chão: capim verde-azulado e trilhas escuras, calculado no pixel do mundo — continua sem
    costura para fora da arena e escurece aos poucos. Sem a faixa escura reta da borda.
  - Limite natural: cerca viva de moitas e samambaias em cada célula que encosta no chão, com
    sombra macia; atrás, emaranhado denso de pinheiros, árvores redondas, cerejeiras e
    pinheiros de luar, mais ralo e mais escuro quanto mais fundo.
  - Lagos (ShuraWater): reflexo escuro das copas pendurado na margem de cima, caminho da lua
    em traços que tremem, estrelas e vaga-lumes refletidos; vitórias-régias, lótus brancos
    com luz e flores rosadas acesas na margem.
  - Luz: sempre noite de lua (escuro mínimo azulado 0,42), vaga-lumes acesos, luz das
    lanternas menos alaranjada (sem manchas roxas).
  - Árvores com corpo exato: pivô no tronco medido no desenho e só a base dos troncos é
    sólida (o maciço de pinheiros tem 3 troncos). Os blocos de estátua viram chão na mata
    (sem os quadrados escuros sob as árvores).
  - Lagos, árvores e flores nunca caem sobre armadilhas.
Maldições: ícones no cartão do HUD (Nikoichu, CC0: sem cura, frágil, desajeitado, inquieto,
  Muramasa) com o número de salas; passar o mouse abre uma caixa que acompanha o cursor com
  o nome e o que a maldição faz.
Testes: 212/212. Bots: 3, 11, 29, 41 — prof. 20/20/20/20, 0 exceções.
Atenção (licença): TimberwolfGames e joraxus pedem que o pacote não seja redistribuído; o
  diretor mandou enviar tudo ao GitHub (repositório público).
Próximo pedido na fila: tirar os símbolos azuis de ataque e fazer brilhar a arma de quem ataca.
```

---

```
PEDIDOS DO DIRETOR — Arma que brilha, fôlego de volta, tutorial e diálogo — v0.16.7
Fora do plano.
  - Aviso de tempo: o anel sobre o inimigo virou a opção "Anéis de tempo (antigo)",
    desligada. O aviso padrão é a arma de quem ataca (Feel/WeaponGlow.cs): contorno de luz de
    1 px na cor da resposta, brilho ao longo da lâmina, faíscas do cabo à ponta cada vez mais
    rápidas e, no instante de responder, o "kira" — estrela de luz na ponta. Finta: o brilho falha.
  - Fôlego de volta (DECISÃO DO DIRETOR, 15 nov 2026): Feature.Stamina = true por padrão
    (limita o ataque sem fim). Volta tudo o que a flag cobre: custo, exaustão, Brilho do
    Respiro, barra no HUD; as conversões do WP-03 saem junto.
  - Tutorial: lições e páginas do tour de mecânicas cortadas são puladas enquanto a flag
    estiver desligada (guardas, bandagem, ferimentos, selos); a contagem "lição N de M" conta
    só as dadas. O HUD não desenha mais as 4 guardas nem a bandagem com as flags desligadas.
  - Prólogo: o duelista entra pelo oeste e o mestre pelo leste na mesma linha; cada um anda até
    o seu ponto, PARA e se encaram de perfil (sem ficar andando no lugar durante a conversa).
  - Diálogo no padrão de RPG: cena visível, caixa sólida com moldura dourada dupla, retratos
    com a pixel art dos dois DE PERFIL, quem fala aceso com ensō, placa com o nome, texto
    maior; sem a numeração das falas.
  - Takeyari: o fosso ganhou uma grade de ripas de bambu com os furos das estacas (não parece
    mais um buraco).
Testes: 212/212 (os da flag ajustados ao fôlego ligado). Bots: 3, 11, 29, 41 — prof. 20/20/20/20,
  0 exceções. Tutorial completo no tour de capturas, 0 exceções.
```

---

```
PEDIDO DO DIRETOR — Trilha de ritmo que se PREVÊ, e bonita — v0.16.8
Problema: golpes de preparação curta faziam a nota nascer quase em cima do centro (dava para
  adivinhar, não prever); combos só mostravam o golpe em curso.
Feito:
  - Intenção de ataque (Entities/Enemy.Intent.cs): ao decidir o golpe, o inimigo planeja o
    combo inteiro (a preparação de cada golpe, inclusive a pausa da leitura adaptativa) e só
    começa depois de uma antecedência mínima: o primeiro golpe nunca acerta antes de
    attackIntentMinLeadFrames (54 quadros, 0,9 s; GameConfig.feel / Balance.json) depois do
    anúncio. Enquanto anuncia, a arma já acende de leve na cor do golpe. Atordoado, quebrado
    ou recuando, o anúncio é cancelado. (Treino e investidas forçadas começam na hora.)
  - A trilha mostra exatamente essa antecedência: toda nota entra pela borda esquerda e anda
    em velocidade constante até a zona de acerto. Um combo chega como uma fila de notas
    ligadas por um fio, cada uma na sua batida.
  - Visual novo (UI/GameUI.Rhythm.cs): placa laqueada com frisos dourados, marcas de compasso
    correndo na velocidade das notas, notas como cápsulas de luz que crescem ao chegar, com
    rastro (vermelha com losango de perigo; laranja dupla ligada; finta tracejada), zona de
    acerto com ensō, linha de julgamento e a faixa da janela perfeita do tamanho real; ao
    responder, anel que se abre com faíscas e o nome saltando; a tecla certa sob o centro.
Efeito no jogo: a antecedência mínima deixa os inimigos rápidos mais legíveis (e um pouco
  mais lentos). Ajustável no painel (F1).
Testes: 212/212. Bots: 3, 11, 29, 41 — prof. 20/20/20/14, 0 exceções.
```

---

```
WP-04 — Anéis e Troca Perfeita como terceira resposta — v0.16.9
Feito:
  1. Quatro anéis (Combat/Rings.cs, fonte única para anel, brilho da arma, trilha de ritmo, som
     e telemetria): ciano liso = aparar · vermelho com losango = esquivar · roxo pontilhado =
     Troca Perfeita · tracejado = esperar. Cada um com um id de som (anel_ciano, anel_vermelho,
     anel_roxo, anel_finta) e um som provisório distinto ao fechar ("tim", batida grave, sino,
     batida abafada); a finta agora também soa. Os sons de verdade entram no WP-17.
  2. Roxo = Troca Perfeita: aparar atravessa a lâmina; esquivar só evita, SEM ganho (resultado
     novo "Evaded": sem Fluxo, sem selo, sem efeito de esquiva perfeita). A exceção do Talismã
     saiu. Ambos voltam com a flag nova LegacySpiritAnswers.
  3. CursedRing=false: golpes "malditos" mostram ciano e bloquear não amaldiçoa mais.
     DoubleRingType=false: o golpe segurado pela leitura adaptativa é só um ciano que fecha mais
     tarde (a trilha já mostra a batida no lugar certo).
  4. Branco partido: já só existia com 2+ jogadores (conferido).
  5. Elos (Combat/EloRules.cs): NamedLinks45=false → 10 comportamentos, um por arma que ENTRA:
     Katana Fio Duplo (corta 2×) · Montante Peso da Montanha (postura ×2) · Gêmeas Enxame (3
     cortes + sangria) · Naginata Varrida do Horizonte (círculo largo, +35% alcance) ·
     Kusarigama Laço da Corrente (+50% alcance, puxa) · Manoplas Punho Atordoante (atordoa) ·
     Tessen Vento do Leque (corte em círculo, afasta) · Kanabō Terremoto do Oni (onda de choque)
     · Shakujō Sino do Templo (+25 de postura, −8 Shura) · Wakizashi Golpe Furtivo (sai na hora,
     +30%). O nome do par (os 45) aparece menor, como sabor.
  6. Distribuição por ato: o Ato I (prof. 0–4) já não tinha roxo nem finta nos inimigos e no
     Senhor; a brecha era o evento da Kitsune (assinatura roxa) a partir da prof. 2 — agora só
     do Ato II em diante (RunEvents.MinDepth).
  7. A IA não lê mais a TROCA (adiantado do WP-13; volta com AiReadsExtended).
Capturas: docs/redesenho/amostras/wp04_aneis.png (em cima: brilho da arma + trilha; embaixo: com
  os anéis antigos ligados, opção nova de linha de comando -shuraRings) e wp04_aneis_daltonico.png
  (a mesma folha em simulação de deuteranopia: as notas se distinguem pela forma).
  Falta a captura do anel ciano no modo "anéis ligados" (essa rodada não fotografou um).
Testes: 221/221 (novos: RingsAndLinksTests — 4 anéis distintos, maldito/duplo, roxo, Ato I sem
  roxo nem finta, Kitsune fora do Ato I, 10 Elos distintos, IA sem TROCA).
Bots: 3, 11, 29, 41 — prof. 20/20/20/20 (antes 20/20/20/14), 0 exceções, 10–18 Trocas Perfeitas.
Fora do plano (não feito): na tabela de E.2.4, "esquiva evita sem ganho" também aparece na linha
  do ciano; o WP-04 só pede isso no roxo, então a esquiva perfeita do ciano continua como era.
  Algozes (rivais) de inimigos roxos ainda podem surgir cedo; decisão do WP-14.

PEDIDOS DO DIRETOR (no meio do WP-04) — mesma versão
  - Cerejeira rosa: virou sakura ao luar com tronco, galhos e copa com volume
    (Tools/art_traps/gen_night_nature.py); a colisão das árvores cobre a base inteira desenhada
    (antes era 0,8 m de 1,6 m).
  - Chefe/rival travado na árvore: o caminho dos inimigos ignorava troncos e lanternas (só via
    as células do chão) e mirava através da árvore. Agora células com corpo sólido saem da
    navegação, e quem está numa delas sai pela vizinha livre.
  - "Buracos enormes": eram as trilhas de terra em azul-marinho chapado (pareciam fossos) e a
    faixa de chão nu entre a cerca viva e a mata. Trilhas viraram capim gasto que se mistura; a
    faixa ganhou mata fechada (moitas e árvores baixas).
  - Baú/mímico: o corpo era um círculo de 0,85 m que passava da base do desenho; virou uma caixa
    medida nos pixels — os pés param na base do baú.
```

---

```
WP-05 — A Arte (技) e o esquema de 7 teclas — v0.16.10
Feito (tudo atrás de LegacyToolsAndKeys=false; ligar a flag devolve R/T, papéis, V, B, G):
  1. Arte (Run/Arts.cs, Entities/PlayerController.Art.cs): um encaixe, uma tecla (R / RT), 2
     cargas; cada finalização devolve 1 (no lugar dos papéis espirituais e do custo em Fluxo).
     Só gasta carga quando faz algo (o Kiai sem golpe vermelho por perto não gasta).
  2. As 7 Artes da 1.0: Kiai (inicial: interrompe o golpe vermelho em preparo do inimigo mais
     perto) · Shuriken (inicial: leque de 3) · Fumaça (Dojo: zera as leituras e te põe 3 m atrás)
     · Sino (Biblioteca: etéreos sólidos) · Corrente (Dojo: puxa um inimigo) · Ruptura (carta que
     vem com a técnica Selo Interno: detona os selos) · Alma (carta quando uma técnica chega ao
     nível III: vira aquele inimigo por 8 s — a antiga Transformação, sem a tecla B).
     Petardo e óleo ficam só no jogo antigo.
  3. Escolha no Arsenal ("Arte: Kiai", ao lado de Começar; as abertas pelo Templo entram na
     roda). No caminho, uma carta de Arte pode trocá-la (Ruptura/Alma garantidas quando o gatilho
     acontece; às vezes outra Arte aberta). A carta toma o lugar de uma relíquia.
  4. Teclas: Atacar, Aparar, Esquivar, Trocar (Q), Cabaça (F), Arte (R), Interagir (E) + trava
     de mira. G, V, B, H, T e 1–4 não fazem mais nada; a tela de Controles só lista essas ações;
     no controle, B virou só a cabaça (sem esperar o "toque"), sem bandagem e sem L3.
     Reviver oferecendo técnica: E + R (X + RT). Espírito caído grita com a Arte.
  5. Kiai em grupo: 2 duelistas em até 1 s (eram 3).
  6. Dojo: a lição "Ferramentas" virou "A Arte" (Shuriken no mestre); a do Kiai ensina o Kiai
     como Arte. Dicas, tour do HUD, reviver e o pátio de duelo não citam mais teclas removidas.
HUD: o encaixe da Arte (ícone ou kanji, nome, tecla R e duas cargas douradas).
  Amostras: docs/redesenho/amostras/wp05_hud_arte.png, wp05_arsenal.png, wp05_controles.png.
Testes: 227/227 (novos: ArtTests — 7 Artes, desbloqueio, carta de Arte, Ruptura do Selo
  Interno, 7 ações de combate, nenhum texto de teclado cita G/V/B/H/T/1–4).
Bots: 3, 11, 29, 41 — prof. 20/20/16/18, 0 exceções. Telemetria input_use dos 4 bots: só
  Attack, Parry, Dodge, Swap, Gourd e Art. Um bot curto com cada Arte (shuriken, fumaça, sino,
  corrente): prof. 8, 0 exceções.
Não feito: a opção "segurar ou alternar" para bloqueio e trava (E.17.3) — fica para o WP-17
  (acessibilidade); os ícones Xbox/PlayStation já existiam e continuam.
```

---

```
WP-06 — Sensação: hierarquia de impacto, finalização, Iai, Mushin, ataques fora da tela — v0.16.11
Feito:
  1. Tabela única de impacto (Config/GameConfig.cs → ImpactTable; Balance.json → feel.impact),
     com os números do plano: golpe 2 f leve · bloqueio 3 f · parry 6 f médio · Troca 8 f médio
     + rastro de tinta 4 f (sem a câmera lenta de antes) · resposta de mestre 8 f + kanji dourado
     (見切 Mikiri, 降 Descida, 投 Arremesso) · quebra de postura 10 f forte SEM câmera lenta (era
     0,25 s) · finalização rápida 6 f · dirigida 12 f · último inimigo 14 f + 0,9 s · quebra de
     chefe 16 f máximo + 0,6 s (破) · tomar dano 4 f forte + borda manchada. O Fluxo não soma
     mais ao hit-stop (a escada fica estável); continua nas faíscas e no som.
  2. Finalização: TOQUE = rápida (resultado "limpo" automático, 18 f); SEGURAR atacar (10 f) =
     mira dirigida com os três níveis. Elites, chefes, Guardião e último inimigo da sala abrem a
     dirigida direto. Corte perfeito: +15 vida (era 25), sobe um nível de Fluxo, saque em dobro;
     sem Mon (nem no perfeito, nem no "na linha").
  3. Iai: errar o saque não custa vida — o líder ataca primeiro (anel normal) e a sala começa sem
     o bônus. Frequência: um por bloco de 4 profundidades (saco embaralhado), nunca dois seguidos.
     Os números antigos voltam por dado (player.standoffMissDamage=30, standoffEvery=0).
  4. Mushin: o HUD some, a leitura não — os anéis/brilho da arma e a trilha continuam; as cargas
     de esquiva aparecem sob os pés.
  5. Seta fora da tela: inimigo fora da tela que começa um golpe contra você ganha uma seta na
     borda, com a cor e a forma do anel (lê-se também sem cor).
  6. Anti-spam: quando a janela do aparar (ou da Troca) é cortada por apertar cedo de novo, a
     lâmina fica cinza por 20 f e toca um "tic" seco.
  7. Posicionamento: 背 (golpe pelas costas, no máximo 1 por segundo por inimigo), 壁 (empurrado
     na parede), 倒 (derrubou outro), com som.
Testes: 231/231 (novos: ImpactTests — a tabela e a escada, nenhuma câmera lenta comum, Iai no
  máximo 1 a cada 4 e nunca seguido, padrões da finalização e do Iai).
Bots: 3, 11, 29, 41 — prof. 20/20/20/20, 0 exceções. Telemetria: finalizações "rapida/limpo" e
  "dirigida/perfeito" aparecem nas runs (os dois caminhos funcionam).
Capturas: docs/redesenho/amostras/wp06_finalizacao_rapida.png. A seta fora da tela e o Mushin
  NÃO apareceram nas rodadas automáticas (as arenas cabem quase inteiras na tela; o Mushin pede
  sequência 10 com Fluxo cheio) — o AutoDriver agora fotografa os dois quando acontecerem.
  A revisão "60 s quadro a quadro" do aceite é do playtest humano.
Fora do WP-06: o Arremesso priorizar fossos e armadilhas (E.2.7) não foi feito.
```
