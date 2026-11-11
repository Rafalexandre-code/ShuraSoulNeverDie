# Créditos de assets — SHURA

**Direção criativa:** Rafael Alexandre de Pinho Lopes  ·  **Design, programação e efeitos:** Claude (Anthropic)  ·  **Motor:** Unity 6

Todos os assets de terceiros estão em `Assets/_Shura/Resources/`. CC0 = domínio público (uso
comercial livre, sem obrigação de crédito, creditamos por gentileza). CC-BY = uso comercial
livre **com crédito obrigatório** (já presente na tela de Créditos do jogo).

## Arte (pixel art)

| Pacote | Autor | Licença | Fonte | Uso no jogo |
|---|---|---|---|---|
| Ninja Adventure — Asset Pack | Pixel-Boy & AAA | CC0 | https://pixel-boy.itch.io/ninja-adventure-asset-pack | Jogador (Samurai), 24 inimigos e 3 lacaios (SamuraiBlue, GladiatorBlue, Monk2, NinjaRed, DemonRed, Skeleton, NinjaDark, Knight, NinjaMasked, NinjaBomb, Tengu, SorcererBlack, Shaman, Caveman, NinjaThunder, Vampire, SkeletonDemon, MonkeyBoxerRed, LanternRed, Spirit, KappaGreen, Cyclope, Slime, Eye, Flam), efeitos (explosão, fumaça, aura, kunai, shuriken, bomba, pergaminho), 13 armas, flecha, piso de templo, paredes, estandartes nobori, lanternas andon, 7 personagens do grupo online (SamuraiRed, NinjaBlue, Monk, Master, NinjaGreen, Hunter, FighterWhite), estátuas, piso de masmorra, chamas das armadilhas (`Resources/Art/NA`; cópia em `StreamingAssets/web/art` para o navegador) |

Arte própria do projeto: as Manoplas do Leão (`weapon_LionGauntlet.png`), a lanterna de pedra
tōrō (`prop_toro*.png`) e o leque de guerra (desenhado em código). Pixel art desenhada em código no `PixelArt.cs`: pedaços de gore,
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
Todas as faixas foram convertidas para .ogg e normalizadas em volume (loudnorm −20 LUFS, com
entrada e saída suaves) para tocar em playlist sem picos. Pastas `Resources/Audio/Music/Calm` e
`Resources/Audio/Music/Fight` (cópia em `StreamingAssets/web/music`).

| Faixa | Autor | Licença | Fonte | Uso |
|---|---|---|---|---|
| Asianoriental1 | Tozan | CC0 | https://opengameart.org/content/asianoriental1 | Calma |
| Oriental Somber | Tozan | CC0 | https://opengameart.org/node/155193 | Calma |
| Yoiyami Core Theme – Deep Blue Ambient Piano | Yoiyami | CC0 | https://opengameart.org/content/yoiyami-core-theme-%E2%80%93-deep-blue-ambient-piano | Calma |
| Self Discipline | Tsorthan Grove | CC-BY 4.0 | https://opengameart.org/content/self-discipline | Calma |
| Samurai | TAD | CC-BY 4.0 | https://opengameart.org/content/samurai | Combate |
| Taiko Drums (seamless loop) | jobro | CC-BY 3.0 | https://opengameart.org/content/taiko-drums-seamless-loop (original: https://freesound.org/people/jobro/sounds/112248/) | Combate |
| Blackmoor Ninjas (versão sem vocais) | Matthew Pablo (http://www.matthewpablo.com) | CC-BY 3.0 | https://opengameart.org/content/blackmoor-ninjas-battle-theme | Combate |

**Crédito exigido (CC-BY):** "Self Discipline" by Tsorthan Grove (CC BY 4.0); "Samurai" by TAD
(CC BY 4.0); "Taiko Drums" by jobro (CC BY 3.0); "Blackmoor Ninjas" by Matthew Pablo,
www.matthewpablo.com (CC BY 3.0). Também na tela de Créditos do jogo.

As trilhas anteriores (Ninja Adventure e "Samurai Nights") saíram do jogo no M1.11.

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
