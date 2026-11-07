# Créditos de assets — SHURA

Todos os assets de terceiros estão em `Assets/_Shura/Resources/`. CC0 = domínio público (uso
comercial livre, sem obrigação de crédito, creditamos por gentileza). CC-BY = uso comercial
livre **com crédito obrigatório** (já presente na tela de Créditos do jogo).

## Arte (pixel art)

| Pacote | Autor | Licença | Fonte | Uso no jogo |
|---|---|---|---|---|
| 16x16 DungeonTileset II v1.7 | 0x72 (Robert) | CC0 | https://0x72.itch.io/dungeontileset-ii | Jogador (cavaleiro), 8 inimigos, armas, flechas, piso, paredes, estandartes, caveiras (`Resources/Art/0x72`) |

Arte própria do projeto (pixel art desenhada em código no `PixelArt.cs`): braseiro, chamas,
pedaços de gore, elo de corrente e ícones de itens.

## Música

| Faixa | Autor | Licença | Fonte | Uso |
|---|---|---|---|---|
| Samurai Nights (loops separados) | Maik Hoffmann — majadroid (https://www.majadroid.com) | CC-BY 3.0 / CC-BY 4.0 / OGA-BY 3.0 | https://opengameart.org/content/samurai-nights | 5 camadas da trilha dinâmica (`Resources/Audio/Music`), recortadas em loops exatos |

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
