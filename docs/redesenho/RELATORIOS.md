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
