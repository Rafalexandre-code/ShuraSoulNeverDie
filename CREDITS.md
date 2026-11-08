# Créditos de assets — SHURA

Todos os assets de terceiros estão em `Assets/_Shura/Resources/`. CC0 = domínio público (uso
comercial livre, sem obrigação de crédito, creditamos por gentileza). CC-BY = uso comercial
livre **com crédito obrigatório** (já presente na tela de Créditos do jogo).

## Arte (pixel art)

| Pacote | Autor | Licença | Fonte | Uso no jogo |
|---|---|---|---|---|
| Ninja Adventure — Asset Pack | Pixel-Boy & AAA | CC0 | https://pixel-boy.itch.io/ninja-adventure-asset-pack | Jogador (Samurai), 8 inimigos (SamuraiBlue, GladiatorBlue, Monk2, NinjaRed, DemonRed, Skeleton, NinjaDark, Knight), 13 armas, flecha, piso de templo, paredes, estandartes nobori, lanternas andon, 7 personagens do grupo online (SamuraiRed, NinjaBlue, Monk, Master, NinjaGreen, Hunter, FighterWhite), estátuas, piso de masmorra, chamas das armadilhas (`Resources/Art/NA`; cópia em `StreamingAssets/web/art` para o navegador) |

Arte própria do projeto (pixel art desenhada em código no `PixelArt.cs`): pedaços de gore,
elo de corrente e ícones de itens.

## Interface (arte própria + fontes)

A arte da interface (papel washi, pinceladas sumi-e, ensō, selo, logo "修羅", paisagem do
título, pétalas) é **do projeto**, gerada por `Tools/ui_art/gen_ui.py` (pincel simulado).

| Fonte | Autor | Licença | Uso |
|---|---|---|---|
| Kaushan Script | Impallari Type | SIL OFL 1.1 | Títulos, nomes, números de dano (`Resources/UI/Fonts`) |
| Spectral (Medium, SemiBold) | Production Type | SIL OFL 1.1 | Textos e menus (`Resources/UI/Fonts`) |
| Yuji Syuku | Kinuta Font Factory | SIL OFL 1.1 | Só para pintar o logo e o selo "修羅" (não vai no jogo) |

Textos das licenças em `Licenses/`.

## Música

| Faixa | Autor | Licença | Fonte | Uso |
|---|---|---|---|---|
| "12 - Temple", "17 - Fight", "34 - Fight" | Pixel-Boy & AAA (Ninja Adventure) | CC0 | https://pixel-boy.itch.io/ninja-adventure-asset-pack | Trilha padrão: calma / combate / Fluxo alto (`Resources/Audio/Music/NA`) |
| Samurai Nights (loops separados) | Maik Hoffmann — majadroid (https://www.majadroid.com) | CC-BY 3.0 / CC-BY 4.0 / OGA-BY 3.0 | https://opengameart.org/content/samurai-nights | Trilha alternativa (Configurações): 5 camadas recortadas em loops exatos (`Resources/Audio/Music`) |

**Crédito exigido:** "Samurai Nights" by Maik Hoffmann (majadroid), licensed CC-BY.

## Efeitos sonoros

| Pacote | Autor | Licença | Fonte | Pasta |
|---|---|---|---|---|
| 20 Sword Sound Effects (Attacks and Clashes) | StarNinjas | CC0 | https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes | `slash`, `clash` |
| Swishes Sound Pack | (OpenGameArt) | CC0 | https://opengameart.org/content/swishes-sound-pack | `swing` |
| Squish Sounds Effects | EZduzziteh | CC0 | https://opengameart.org/content/squish-sounds-effects | `flesh`, `splat` |
| Fantasy Weapons and Apparel SFX Library | (OpenGameArt) | CC0 | https://opengameart.org/content/fantasy-weapons-and-apparel-sfx-library | `clash`, `unsheathe`, `sheathe`, `step`, `dodge`, `pickup` |
| Medieval sound effects — Weapon impacts | Still North Media (Ben Jaszczak & Brian Nelson) | CC0 | https://opengameart.org/node/146863 | `parry`, `block`, `heavy`, `crunch` (golpes isolados das gravações longas) |

Processamento: mono, 44,1 kHz, silêncio aparado, volume normalizado e convertido para OGG
(script `prep_assets.py`, fora do repositório). Os sons sintetizados do protótipo continuam
como camada extra em alguns eventos e como reserva.
