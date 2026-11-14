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
