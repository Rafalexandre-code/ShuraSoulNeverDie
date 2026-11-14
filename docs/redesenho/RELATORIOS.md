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
