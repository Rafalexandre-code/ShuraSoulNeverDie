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

## Desempenho
Pool de partículas (sprites reutilizados) e de fontes de áudio. Meta: 60 FPS estáveis.
Contagem de entidades é baixa (≤ 4 lutadores), então sem culling.

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
