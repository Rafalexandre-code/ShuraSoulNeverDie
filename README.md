# SHURA — Protótipo M1

Roguelite de ação 2D (visão de cima) sobre duelos de parry e postura. Quem vence absorve a
técnica do vencido. Feito com **Unity 6 (6000.6.8f1)** + C#.

Design: [`docs/01-Conceito.md`](docs/01-Conceito.md) · [`docs/02-GDD.md`](docs/02-GDD.md) ·
[`docs/03-TDD.md`](docs/03-TDD.md) · [`docs/04-Roadmap.md`](docs/04-Roadmap.md)

## Como jogar

**No editor:** abra a pasta no Unity Hub → abra `Assets/_Shura/Scenes/Main.unity`
(ou menu **Shura → Abrir cena principal**) → **Play**.

**Executável:** menu **Shura → Build Windows (desenvolvimento)** → `Builds/Windows/Shura.exe`.

| Ação | Teclado + mouse | Controle |
|---|---|---|
| Mover | WASD / setas | Analógico esquerdo |
| Mirar | Mouse | Analógico direito |
| Atacar / Finalizar | Botão esquerdo / J | RB |
| Aparar (toque) / Bloquear (segurar) | Botão direito / K | LB |
| Esquivar | Espaço / L | A |
| Pausar | Esc | Start |

**Como ler os golpes:** um anel encolhe sobre o inimigo que vai atacar. Aperte quando ele
**fechar** (ouve-se um "tim"). Anel azul = aparar; anel vermelho com losango = esquivar.

**Progresso entre runs:** cada run rende Ecos, mesmo morrendo. Gaste-os no **Altar das Almas**
(menu principal ou tela de morte). O progresso fica em
`%USERPROFILE%\AppData\LocalLow\Shura\Shura\progress.json`; apague o arquivo para recomeçar do zero.

## Ajustar valores sem programar

Todos os números (janela de parry, dano, hit-stop, tremida, cores…) estão em
`GameConfig`. Para editar no Inspector: **Create → Shura → Game Config**, depois arraste o
asset para o componente **Game** do objeto `Game` da cena. Sem asset, valem os padrões do código.

Conteúdo (inimigos, salas, recompensas) fica em `Assets/_Shura/Scripts/Runtime/Content/`.

## Ferramentas de desenvolvimento (editor e builds de desenvolvimento)

| Tecla | Efeito |
|---|---|
| F1 | Overlay de debug (FPS, frames, estados, hurtboxes) |
| F2 | Invencível |
| F3 | Mata todos os inimigos |
| F4 | Pula a sala |
| F5 | +5 de Fluxo |

## Verificação automática

```bash
# Testes de lógica (36 testes)
"C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml

# Run completa jogada por um bot (sem janela), com resumo no log
Builds/Windows/Shura.exe -batchmode -nographics -shuraBot -shuraSeed 1 -logFile bot.log

# Tour de capturas de tela de todas as telas
Builds/Windows/Shura.exe -screen-fullscreen 0 -shuraShots capturas -shuraSeed 1
```

O Unity precisa estar **fechado** para os comandos `-batchmode` que abrem o projeto.

## Estrutura

```
Assets/_Shura/
  Scenes/Main.unity          só contém o objeto "Game"; tudo é criado por código
  Scripts/Runtime/           Core, Config, Input, Combat, Entities, Content, Run, Feel, Audio, UI, DevTools
  Scripts/Editor/            ProjectSetup (cena, build settings, builds)
  Tests/EditMode/            testes das regras de combate
docs/                        conceito, GDD, documento técnico, roadmap
```

## Estado

Arte, sons e música são **placeholders gerados por código**, e a UI é provisória (IMGUI).
Tudo isso será substituído nos marcos M2 e M4 (ver roadmap).
