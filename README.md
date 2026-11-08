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
| Trocar arma (no tempo do anel = Troca Perfeita) | Q / botão do meio | Y |
| Reviver aliado caído (segurar, online) | E | X |
| Equipamento (na tela de recompensa) | Tab | Select |
| Pausar | Esc | Start |

**Como ler os golpes:** um anel encolhe sobre o inimigo que vai atacar. Aperte quando ele
**fechar** (ouve-se um "tim"). Anel azul = aparar; anel vermelho com losango = esquivar.

## Jogar online (até 8, pelo navegador)

Quem hospeda roda o jogo; os outros só precisam de um navegador (Chrome, Edge, Firefox).

1. Todos instalam o **Radmin VPN** e entram na mesma rede dele (nome e senha combinados).
2. O anfitrião clica em **Jogar online (até 8)** no menu principal. Na primeira vez, o Windows
   pergunta sobre o firewall: permita em **redes privadas** (a porta é a 7777).
3. A sala mostra os links. Passe aos amigos o que começa com **http://26.** (o IP do Radmin);
   na mesma casa, o link 192.168… também serve.
4. Cada um abre o link, escolhe nome e duas armas e clica em **Entrar no duelo**. Dá para
   entrar com a run já em andamento.
5. O anfitrião clica em **Começar a run**.

A equipe divide recompensas, itens e Fluxo; cada um tem as próprias armas. Arenas, ondas,
vida dos inimigos e o número de inimigos atacando ao mesmo tempo crescem com o grupo. O jogo
compensa o ping de cada jogador no tempo do anel.

**Reviver:** quem cai fica no chão marcado por um ensō. Um aliado fica em cima dele e
**segura E** (controle: X) por ~2,4 s; dois aliados juntos revivem mais rápido. Os caídos também
voltam com meia vida no começo da sala seguinte. A run só acaba quando todos caem.

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
# Testes de lógica (62 testes)
"C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml

# Bot joga até a profundidade 15 (sem janela), com resumo no log
Builds/Windows/Shura.exe -batchmode -nographics -shuraBot -shuraSeed 1 -shuraDepth 15 -logFile bot.log

# Tour de capturas começando direto numa profundidade (ex.: sala de Guardião)
Builds/Windows/Shura.exe -screen-fullscreen 0 -shuraShots capturas -shuraStartDepth 4

# Tour de capturas de tela de todas as telas
Builds/Windows/Shura.exe -screen-fullscreen 0 -shuraShots capturas -shuraSeed 1

# Online com 8: o bot hospeda e espera 7 jogadores; 7 bots de navegador (Node 22+) entram e lutam
Builds/Windows/Shura.exe -screen-fullscreen 0 -shuraBot -shuraHost 7 -shuraDepth 5 -logFile host.log
node Tools/web/bot_clients.mjs 7 150 localhost:7777
```

Depois de trocar arte, sons ou fontes em `Resources`, rode
`python -I Tools/web/sync_web_assets.py .` para atualizar a cópia usada pelo navegador.

O Unity precisa estar **fechado** para os comandos `-batchmode` que abrem o projeto.

## Estrutura

```
Assets/_Shura/
  Scenes/Main.unity          só contém o objeto "Game"; tudo é criado por código
  Scripts/Runtime/           Core, Config, Input, Combat, Entities, Content, Run, Feel, Audio, UI, Net, DevTools
  Scripts/Editor/            ProjectSetup (cena, build settings, builds), ArtImportRules
  Resources/Art/NA/          pixel art Ninja Adventure (personagens, armas, tiles) — CC0
  Resources/Audio/Sfx/       efeitos gravados por categoria (swing, clash, parry...) — CC0
  Resources/Audio/Music/     NA/ trilha padrão (CC0) · raiz: Samurai Nights em 5 camadas (CC-BY, crédito obrigatório)
  Tests/EditMode/            testes de combate, itens, arenas, escala de grupo e protocolo online
Assets/StreamingAssets/web/  cliente do navegador (HTML/JS) + cópia da arte e dos sons
Tools/web/                   sincronização dos assets web e bots de teste online
Tools/art/                   importação do bestiário e efeitos do pacote Ninja Adventure
docs/                        conceito, GDD, documento técnico, roadmap
CREDITS.md                   autores e licenças de todos os assets de terceiros
```

Para trocar um som: coloque arquivos `.ogg`/`.wav` na pasta da categoria em
`Resources/Audio/Sfx/<categoria>/` — o jogo sorteia entre todos os arquivos da pasta.

## Estado

Arte de personagens, armas e arenas: "Ninja Adventure" (CC0), elenco samurai. Sons gravados
e trilha do pacote, com "Samurai Nights" como alternativa nas Configurações (ver `CREDITS.md`). Interface pintada à mão (tinta sumi-e sobre washi,
arte própria em `Tools/ui_art`); um shader de paleta para unificar a pixel art fica para o M2.
