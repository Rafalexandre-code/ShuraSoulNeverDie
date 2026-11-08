# SHURA — Documento Técnico (Fase 3)

## Estrutura
```
Assets/_Shura/
  Scenes/Main.unity              cena única; só contém o GameObject "Game"
  Scripts/Runtime/  (Shura.Runtime.asmdef)
    Core/      Game (bootstrap + máquina de estados), SimClock, SeededRandom
    Config/    GameConfig (ScriptableObject com todos os valores ajustáveis)
    Input/     InputService (ações remapeáveis), InputBuffer
    Combat/    AttackDef, CombatMath, HitResolver, PostureMeter, FlowMeter,
               AttackCoordinator   ← lógica pura, testada
    Entities/  Fighter, PlayerController, Enemy
    Content/   EnemyDefs, RoomDefs, RewardDefs   ← dados de conteúdo
    Run/       RunState, Rewards, World
    Feel/      CameraRig, Vfx, ShapeLibrary
    Audio/     SfxSynth, AudioService
    UI/        Strings, Settings, GameUI
    DevTools/  DebugOverlay
  Scripts/Editor/   ProjectSetup (cena, build settings, build Windows)
  Tests/EditMode/   testes da lógica de combate
docs/            documentos de design
```

### Arte e áudio carregados de Resources
- `PixelArt` carrega folhas de `Resources/Art/NA` (legíveis, Point, sem compressão — regra
  em `ArtImportRules`), monta `CharacterArt` em 4 direções (andar/parado/golpe/caído +
  silhuetas brancas, pivô nos pés), recorta regiões de tilesets por coordenada de tile,
  gira armas 90° para a convenção "+X, pivô na empunhadura, 1 unidade" e desenha props em grade
  de caracteres (braseiro, chamas, gore, elo, ícones). Sem a arte, cai nas formas antigas.
- `ArenaBuilder` compõe piso e paredes numa única textura por sala (tiles tingidos pelo bioma).
- `AudioService` carrega `Resources/Audio/Sfx/<categoria>` como bancos de variações por `Sfx`
  (sorteio sem repetição imediata, camada sintetizada opcional por baixo) e 5 stems de música
  com `PlayScheduled` no mesmo instante — todos múltiplos exatos de 302 400 amostras. Por
  padrão toca a trilha do Ninja Adventure (3 faixas em streaming com crossfade calma/combate/
  Fluxo alto); `GameSettings.samuraiNights` troca para os stems.

## Dependências
```
Game ─► SimClock, InputService, World, CameraRig, Vfx, AudioService, GameUI, RunState
World ─► PlayerController, Enemy, AttackCoordinator, HitResolver, Content
PlayerController/Enemy ─► Fighter ─► PostureMeter, CombatMath
Rewards ─► (ganchos do PlayerController)
GameUI ─► Game (somente leitura + comandos)
```

## Game loop
- **Timestep fixo de 60 Hz** próprio (`SimClock`), com acumulador sobre `unscaledDeltaTime`
  (limite de 0,25 s para evitar espiral). Toda a lógica de combate conta em frames.
- **Hit-stop** = ticks de simulação pulados. O buffer de entrada não envelhece durante o
  hit-stop, então um comando apertado no congelamento é executado logo depois.
- Renderização interpola posições entre o tick anterior e o atual (suave em 144 Hz).
- Poses também: `Fighter.AnimFrame(stateFrame)` devolve o quadro do estado com a fração de
  render, então ângulo de arma, extensão e inclinação andam contínuos mesmo com o tempo
  global reduzido. O ângulo final passa por suavização exponencial com `DeltaAngle` (sem
  saltos de 360°). O rastro da lâmina (`Fighter.BladeTrail`) é uma malha em espaço de mundo,
  amostrada a cada quadro de render e subdividida por ângulo; congela durante o hit-stop.
- Não usamos física do Unity: colisão é círculo × círculo e círculo × retângulo da arena.

## Máquina de estados do jogo
`Title → MainMenu → Playing ⇄ Paused → RewardSelect → Playing … → GameOver | Victory → MainMenu`.
Perda de foco da janela durante `Playing` → `Paused`.

## Comunicação entre sistemas
- **Chamadas diretas** dentro do combate (World resolve acertos e chama métodos das
  entidades): ordem determinística, fácil de depurar.
- **Ganchos de técnica** (`OnPerfectParry`, `OnPerfectDodge`, `OnCounterThrust`,
  `OnFinisher`, `OnHurt`) para recompensas: conjunto fechado, para não explodir
  combinações.
- **Eventos C#** (`World.Feedback`) para efeitos, som e câmera: o combate não conhece a
  apresentação.

## Dados
- `GameConfig` (ScriptableObject): valores do jogador, sensação, Fluxo, arena e paleta.
  Sem asset, usa os valores padrão do código. Para ajustar no Inspector:
  *Create → Shura → Game Config*, arrastar para o componente `Game`.
- Conteúdo (inimigos, salas, recompensas) em classes C# de dados em `Content/`.

## Salvamento
`Settings` em JSON no PlayerPrefs, com campo `version`; dados inválidos ou antigos voltam
para o padrão sem quebrar. Remapeamentos salvos via `SaveBindingOverridesAsJson`.
Meta-progressão usará o mesmo esquema (arquivo próprio) no marco correspondente.

## Online (M1.9)
- **Host autoritativo.** Só o jogo do anfitrião simula. `Net/WebServer` é um servidor HTTP +
  WebSocket próprio sobre `TcpListener` (porta 7777, threads de leitura/envio, fila
  `ConcurrentQueue` lida no thread principal). Serve `StreamingAssets/web` e `/arena.png`
  (a arena desenhada pelo host).
- **Protocolo (JSON):** cliente → `join` (nome, aparência, 2 armas), `loadout`, `ready`, `in`
  (eixos + contadores de toques, para nenhum toque se perder), `pick` (recompensa), `equip` /
  `unequip` / `inv` (equipamento), `pong`. Servidor → `welcome`, `room` (decoração, armadilhas e
  grade de chão), `s` (estado ~30/s: jogadores com Fluxo/sequência/pronto/aparência próprios,
  inimigos, projéteis, explosões, itens, armadilhas, eventos), mensagens pessoais `rewards`,
  `picked`, `inv`, `toast`, e `ping`. Envio com descarte dos quadros mais velhos se o cliente atrasa.
- **Build por jogador (M1.11):** `PlayerBuild` guarda armas, itens, mochila, recompensas,
  técnicas pendentes, efeitos, modificadores, Fluxo, sequência e estatísticas de cada duelista.
  `RunState` fica com o que é do grupo (semente, salas, ascensão, Ecos, leitura dos inimigos) e
  expõe o build do anfitrião (`Main`) pelos atalhos antigos. Ganchos de combate usam o build de
  quem está agindo (`World.Player.Build`). Online, pausa/equipamento/configurações do anfitrião
  não param a simulação (`Game.WorldKeepsRunning`).
- **Jogadores remotos** são `PlayerController` comuns com `NetInput` como fonte de input.
  `World.actor` diz quem está agindo; efeitos e sons gravados pelo `FeedbackTap` vão para os
  clientes, e no host o "feel" de jogadores remotos sai mudo.
- **Ping:** o impacto do golpe inimigo atrasa `LatencyTicks` do alvo, e o anel fecha nesse
  instante: o jogador remoto aperta quando vê o anel fechar, como no local.
- **Arenas:** `ArenaLayout` (grade com forma, estátuas, armadilhas, colisão por células) e
  navegação por campo de caminhos (BFS por destino, em cache) em `Steer`.

## Bestiário (M1.10)
- Mecânicas são **dados**: traços em `EnemyDef` (etéreo, voador, roubo de vida, fúria, esquiva,
  remontagem, divisão, explosão ao morrer, aura de cura, invocação/cópias, teleporte) e flags
  em `AttackDef` (leque de projéteis, explosão no chão, mergulho, investida com atordoamento,
  finta, postura de saque, invocação). `Enemy` interpreta; novos inimigos são só tabelas.
- O anel de tempo só aparece para golpes reais (`ShowsTimingRing`): fintas, posturas e
  ataques à distância nunca mentem.
- Inimigos nascidos no meio da luta (invocações, divisões) entram numa fila e só se juntam à
  lista no fim do tick: nenhuma morte dentro de um laço sobre `Enemies` altera a lista.
- `World.Blasts`: círculos com pavio (bombas, talismãs, explosão da lanterna); vão no snapshot
  online (`B`) junto da arte dos projéteis (`R[6]`).

## Desempenho
Pool de partículas (sprites reutilizados) e de fontes de áudio. Meta: 60 FPS estáveis.
Até 8 jogadores e ~20 inimigos: sem culling. Online: ~45 KB/s por cliente a 30 estados/s.

## Riscos técnicos
| Risco | Mitigação |
|---|---|
| Não vejo o editor | Cena gerada por código; testes automáticos; captura de tela automática do build |
| Precisão do parry | Lógica em frames fixos; testes do HitResolver |
| Pipeline de render | Protótipo no Built-in RP; migração para URP 2D no polimento, junto da arte |
| UI provisória (IMGUI) | Substituir por UI Toolkit/uGUI na Fase 6 |

## Registro de decisões
| Decisão | Alternativas | Justificativa |
|---|---|---|
| Unity 6 + C# | Godot, Phaser | Já instalado; Input System robusto; foco Steam |
| Timestep próprio 60 Hz | FixedUpdate + física | Frames exatos, hit-stop sem mexer em `timeScale` |
| Sem física do Unity | Rigidbody2D | Poucas entidades; controle total e testável |
| Cena montada por código | Cena editada à mão | Eu não vejo o editor; YAML de cena é frágil |
| Built-in RP no protótipo | URP 2D | Zero configuração; migrar junto da arte |
| Placeholders e áudio sintetizados | Assets já agora | Validar diversão antes de conteúdo |
| IMGUI no protótipo | uGUI/UI Toolkit | Rápido de iterar; UI final no polimento |
| Online pelo navegador, host autoritativo | Netcode for GameObjects, Mirror, Steam | Amigos entram por um link do Radmin sem instalar nada; zero dependências |
| Servidor HTTP/WebSocket próprio | HttpListener, pacote externo | HttpListener exige permissão de admin para IP externo; TcpListener não |
