# SHURA — Game Design Document (Fase 2)

Valores numéricos de referência ficam em `Assets/_Shura/Scripts/Runtime/Config/GameConfig.cs`
(jogador/sensação) e `Assets/_Shura/Scripts/Runtime/Content/` (inimigos, salas, recompensas).
Tempos em **frames de simulação a 60 Hz** (1 f ≈ 16,7 ms).

## 1. Visão geral
Roguelite de ação 2D, visão de cima, duelos de parry e postura contra até 3 inimigos.
Público: fãs de soulslike e roguelite. Plataforma: PC/Steam (build web só para playtest).
Pilares: ver `01-Conceito.md`.

## 2. Jogabilidade
| Ação | Teclado + mouse | Controle |
|---|---|---|
| Mover | WASD | Analógico esquerdo |
| Mirar | Mouse | Analógico direito (sem ele, mira para onde anda) |
| Atacar / Finalizar | Botão esquerdo / J | RB / R1 |
| Aparar (toque) / Bloquear (segurar) | Botão direito / K | LB / L1 |
| Esquivar | Espaço / L | A / Cruz |
| Pausar | Esc | Start |
| Debug (dev) | F1 (F2 invencível, F3 mata tudo, F4 pula sala, F5 +Fluxo) | — |

**Verbos:** mover, mirar, atacar (combo de 3), aparar, bloquear, esquivar, finalizar.

**Regras centrais**
- **Parry direcional:** cobre um arco de 120° para onde você mira. Golpes pelas costas não
  podem ser aparados. Magnetismo leve gira a mira para o inimigo que está atacando.
- **Indicador de tempo:** durante o preparo de cada golpe, um anel encolhe sobre o
  inimigo em velocidade constante (36 f) e **fecha 4 f antes do impacto**, com um "tim".
  Apertar quando o anel fecha cai no meio da janela de parry perfeito e dentro dos frames
  de invencibilidade da esquiva. Azul = aparar; vermelho + losango = esquivar. A própria
  arma do inimigo recua durante o preparo (antecipação) e treme nos golpes segurados.
  Pode ser desligado nas configurações.
- **Parry perfeito:** primeiros 9 f após apertar (era 7; ampliado após o primeiro playtest). Causa dano de postura no atacante, zero
  desgaste no jogador. Apertar de novo em menos de 20 f reduz a janela pela metade
  (anti-spam).
- **Bloqueio:** segurar após a janela. O golpe vira desgaste de postura no jogador.
- **Esquiva:** 11 f, invencível do 2º ao 9º frame, 2 cargas que recarregam em 0,9 s.
  Esquiva cujos frames de invencibilidade absorvem um golpe = **esquiva perfeita**.
- **Contra-investida:** esquivar *em direção* a uma estocada perigosa durante os frames
  ativos dela → atravessa, atordoa e causa muita postura.
- **Postura:** cheia → inimigo **Quebrado** por 150 f (não ataca, selo de finalização).
  Atacar perto dele = **Finalizador** (mata inimigos comuns). Postura do inimigo regenera
  mais devagar quando ele está com pouca vida.
- **Postura do jogador:** cheia → **Guarda Quebrada** (60 f atordoado).
- **Ataques perigosos** (símbolo de losango + tom agudo + marca no chão) não podem ser
  aparados: **Varredura** (círculo: saia ou esquive através), **Estocada** (linha:
  contra-investida ou saia da linha).
- **Cancelamentos:** recuperação de ataque pode ser cancelada em esquiva ou parry 4 f após
  o fim dos frames ativos. Buffer de entrada: 8 f (ataque/esquiva), 3 f (parry).

## 2b. Armas e Troca Perfeita
Você leva **duas armas** (escolhidas no **Arsenal** antes da run) e troca com **Q / Y**.
- **Troca Perfeita:** trocar dentro da janela de parry (quando o anel fecha) **apara** o golpe
  (+30% de postura no atacante, +50% de Fluxo) **e** dispara o **golpe de troca** da arma que
  entra, com invencibilidade. Diferente do parry, a troca **não tem bloqueio**: errar = levar o
  golpe inteiro. Trocas erradas seguidas encurtam a próxima janela.

| Arma | Estilo | Combo | Golpe de troca | Finalização |
|---|---|---|---|---|
| Katana | Equilibrada | 3 cortes alternados | Iaijutsu (atravessa em linha) | Corte Limpo (corpo partido ao meio) |
| Montante | Lenta, pesada, −15% velocidade | Varrido largo + golpe descendente | Queda do Céu (salto com onda) | Execução (esmagamento, poça enorme) |
| Lâminas Gêmeas | Muito rápida, +12% velocidade | 5 golpes, giro final | Dança das Lâminas (6 cortes em roda) | Mil Cortes (9 estocadas de todos os lados) |
| Naginata | Alcance | 2 estocadas + varrido de 200° | Estocada Relâmpago (investida de 3 m) | Empalar (ergue na haste e arremessa) |
| Kusarigama | Alcance e puxão | 2 chicotadas + giro (puxam inimigos) | Laço Ceifador (giro de 3 m que puxa) | Ceifa (gira o inimigo pela corrente) |
| Manoplas de Ferro | Postura | 4 socos com muita postura | Punho do Trovão (50 de postura) | Coração Partido (soco que atravessa) |

**Sangue:** golpes espirram sangue que mancha o chão até o fim da sala; finalizações inundam
o chão, com pedaços do corpo conforme a arma. Opção **Sangue: Ligado/Desligado**.

## 2c. Equipamento (itens dropados)
- Inimigos têm 18% (+1% por profundidade, máx. 40%) de deixar um item; **Guardiões sempre**.
  O item fica no chão sob um **feixe de luz** da cor da raridade (mais alto quanto mais raro);
  basta passar por cima. Ao vencer a sala, o que ficou no chão é recolhido.
- **4 espaços:** Amuleto, Anel, Bracelete, Talismã. Se o espaço está livre, equipa na hora;
  senão vai para a **mochila** (12). Os itens duram até o fim da run.
- **10 raridades:** Comum, Incomum, Raro, Épico, Lendário, Mítico, Ancestral, Divino, Celestial,
  Shura. Mais raro = mais atributos (1 → 5) e valores maiores (×1 → ×5). A profundidade e os
  Guardiões puxam a raridade para cima.
- **Atributos:** dano, postura, vida, postura máxima, recarga da esquiva, Fluxo, cura por sala,
  cura ao finalizar, velocidade, **crítico** (×1,8), **roubo de vida** e, a partir de Lendário,
  janela de parry.
- **Elementos** (a partir de Raro, mudam a cor do rastro do corte e acrescentam uma variação
  de ataque): Fogo (queima), Gelo (inimigo anda e ataca mais devagar), Raio (raio em cadeia),
  Sangue (sangramento + cura), Vento (o 3º golpe solta uma onda de corte), Sombra (parry
  perfeito invoca uma lâmina sombria).
- **Tela de Equipamento** (Tab / Select na recompensa, ou pela pausa): personagem, roda de
  armas, atributos, espaços, mochila e detalhes do item. O painel de técnicas saiu da tela
  de jogo.

## 3. Sistemas
- **Técnicas Absorvidas:** cada inimigo tem um golpe-assinatura e uma *Resposta Perfeita*.
  Executar a resposta marca o inimigo com o selo dourado; finalizá-lo absorve a técnica,
  que aparece garantida na próxima escolha de recompensa.
- **Fluxo (0–3):** pontos por ações perfeitas (parry perfeito +1, esquiva perfeita +1,
  contra-investida +2, finalizador +2). Níveis em 3/7/12 pontos. Levar dano derruba
  um nível. Efeitos: N1 +15% dano de postura; N2 esquiva recarrega 30% mais rápido;
  N3 finalizadores curam 10. Cada nível acrescenta uma camada musical.
  Além disso, **cada nível acelera o jogo** (ritmo 0,82 no Fluxo 0 → 1,03 no Fluxo 3), dá
  **+12% de dano**, mais hit-stop, tremida e faíscas nos seus golpes, aproxima a câmera, e a
  partir do nível 2 você deixa rastro dourado. No nível 3 as bordas da tela queimam em dourado.
- **Sequência perfeita:** parries perfeitos, contra-investidas e esquivas perfeitas de golpes
  perigosos contam uma sequência; levar dano zera. A cada 5 há uma fanfarra.
- **Câmera lenta:** ao finalizar o último inimigo da sala (0,9 s) e ao quebrar postura (0,25 s).
- **Recompensas:** após cada sala, escolha 1 de 3 (técnicas pendentes primeiro, depois
  relíquias). Todas têm custo. Limpar uma sala cura 20 de vida.
- **Coordenador de ataque:** só 1 inimigo ataca por vez, com intervalo mínimo de 20 f.
- **Progressão permanente (Altar das Almas):** cada run rende **Ecos**, mesmo morrendo:
  sala vencida 15 + 5 × índice da sala, finalização 3, parry perfeito 1, contra-investida 2,
  técnica absorvida 5, vitória +40. Os Ecos compram melhorias permanentes, **todas com nível
  máximo** para a habilidade continuar sendo o principal:

  | Melhoria | Efeito por nível | Máx. | Custo inicial |
  |---|---|---|---|
  | Vigor | +10 vida | 5 | 25 |
  | Postura de Aço | +12 postura | 4 | 25 |
  | Lâmina Temperada | +8% dano | 4 | 35 |
  | Fôlego | −8% recarga da esquiva | 3 | 30 |
  | Meditação | +8 cura por sala | 3 | 30 |
  | Olhar Afiado | +1 frame de parry perfeito | 2 | 60 |
  | Despertar | começa com Fluxo (3 pts/nível) | 2 | 45 |
  | Visão do Destino | +1 opção de recompensa | 1 | 120 |

  Cada nível custa 60% a mais que o primeiro. Uma morte típica na sala 2 rende ~30–40 Ecos
  (≈ uma melhoria barata por run no começo).

## 4. Entidades
| Entidade | Vida | Postura | Forma | Golpes | Assinatura → Resposta Perfeita |
|---|---|---|---|---|---|
| Jogador | 100 | 100 | Círculo claro + cunha | Combo 3 (10 dano / 7 postura) | — |
| Ronin Errante | 60 | 60 | Quadrado | Corte Duplo; Corte Atrasado | Corte Atrasado → parry perfeito |
| Lanceiro | 55 | 50 | Triângulo | Cutucada; Estocada perigosa | Estocada → contra-investida |
| Monge do Sino | 110 | 100 | Hexágono grande | Palmas (3 golpes); Varredura perigosa | Varredura → esquiva perfeita |
| Kenkaku Veloz | 50 | 55 | Pentágono | Cortes rápidos | Tempestade (4 golpes seguidos) → parry perfeito |
| Carrasco | 150 | 130 | Octógono, não recua | Machado lento; golpe duplo | Agarrão (perigo, só esquiva) → esquiva perfeita |
| Arqueiro das Cinzas | 45 | 45 | Cápsula, fica longe | Chute; flechas | Flecha carregada → parry perfeito (devolve a flecha) |
| Sombra | 50 | 55 | Losango | Corte sombrio | Some e reaparece às suas costas → parry perfeito |
| Escudeiro | 70 | 80 | Escudo, defende 85% de frente | Golpe de escudo | Investida de escudo → parry perfeito |

**Choque de lâminas:** um parry perfeito **interrompe** o golpe — a arma do inimigo quica e ele
fica aberto (14 f; 28 f se era o último golpe do combo). Combos continuam depois do quique,
golpe a golpe. Quando o inimigo **defende** seu golpe, é a sua espada que quica: o combo é
cortado e você recupera em 7 f a tempo de aparar o contra-ataque. Só parries **errados**
sofrem a penalidade anti-spam, então rajadas podem ser respondidas golpe a golpe.
| Chefe (marco 2) | — | — | — | 2 fases | — |

## 5. Progressão (run infinita)
A run **não termina**: cada sala é gerada pela semente e pela profundidade.
- **Profundidade 1** é o Pátio das Cinzas (ensino). Das profundidades 2 a 8 entra um inimigo
  novo por sala, sempre enfrentado sozinho primeiro (Lanceiro, Kenkaku, Escudeiro, Arqueiro,
  Monge, Sombra, Carrasco).
- Ondas preenchidas por um orçamento que cresce com a profundidade (2 → 4 ondas, até 3
  inimigos ao mesmo tempo); inimigos recém-liberados aparecem com mais frequência.
- **A cada 5 salas, um Guardião**: versão maior do inimigo (×2,2 vida, ×1,6 postura, aura
  dourada), vale +15 Ecos.
- **Biomas** mudam a cada 5 salas (Cinzas, Musgo, Sangue, Abismo, Gelo): cor do chão, das
  paredes e do fogo. O tamanho da arena varia por sala.
- **Inimigos escalam**: +8% vida, +6% postura, +5% dano por profundidade, pausas menores e
  intervalo entre ataques menor.
- **O jogador escala**: **Ascensão** automática a cada sala (+4 vida, +3% dano, +2 postura),
  recompensas, e **Bênçãos** (recompensas pequenas, repetíveis e sem custo; a partir da
  profundidade 3 uma das opções é sempre uma Bênção).

### IA que aprende (Leitura)
Os inimigos observam os últimos ~12 s de combate e formam uma **leitura** do jogador. A força
com que usam a leitura cresce com a profundidade (15% → 100% na profundidade 13):

| Leitura | O que você faz | O que eles passam a fazer |
|---|---|---|
| ATRASO | Apara cedo demais | Seguram alguns golpes (olho violeta, lâmina tremendo); o anel espera junto |
| DEFESA | Ataca sem parar | Defendem mais e contra-atacam após 2 defesas |
| PRESSÃO | Espera demais | Atacam com mais frequência |
| PERSEGUIÇÃO | Esquiva muito | Golpes acompanham você por mais tempo (sempre se comprometem 6 f antes) |
| PERIGO | Apara com precisão | Mais golpes perigosos (que não se aparam) |

A leitura nunca é escondida: aparece no HUD, é anunciada quando surge e é detalhada na tela
de recompensa. O anel de tempo continua sempre fiel ao momento real do golpe. Jogo completo: 3 biomas
× ~6 salas + chefe. Ritmo: sala de ensino → mistura → pico → descanso (recompensa).

## 6. Vitória, derrota, reinício
Vitória: limpar a última sala (no MVP: derrotar o chefe). Derrota: vida 0 → tela "A morte
ensina" com estatísticas → nova run com nova semente em 1 botão.

## 7. Conteúdo do MVP
1 bioma, 4–5 tipos de inimigo, 1 chefe com 2 fases, ~20 recompensas, 1 arma.
Protótipo atual: 3 inimigos, 4 salas, 3 técnicas, 7 relíquias.

## 8. Interface
Telas: Título → Menu (Jogar, Altar das Almas, Configurações, Créditos, Sair) → Run (HUD) ⇄
Pausa (Continuar, Configurações, Recomeçar, Menu) → Recompensa → … → Morte/Vitória (Ecos
ganhos; Nova run, Altar, Menu) → Menu.
HUD: vida (com barra "fantasma" do dano recente), postura, cargas de esquiva, Fluxo, Ecos
da run, técnicas, sala atual. Números de dano sobre os inimigos.
UI do protótipo é provisória (IMGUI); UI final na Fase 6.

## 9. Onboarding
Sala 1 = um único Ronin, com dicas de controle no chão. Sala 2 introduz o Lanceiro
(estocada) já sabendo aparar. Sala 3 introduz a Varredura isolada. Sala 4 combina.

## 10. Direção de arte
Pixel art livre: **0x72 "16x16 DungeonTileset II" (CC0)**, 16 px por unidade para tudo
(personagens, armas, piso, paredes), filtro Point, visão de cima em 3/4 (o personagem vira
para a esquerda/direita; a arma gira 360° na mão). Elenco:

| Entidade | Sprite | Arma |
|---|---|---|
| Jogador | cavaleiro (`knight_m`) | a arma equipada |
| Ronin Errante | orc guerreiro | espada comum |
| Lanceiro | orc mascarado | lança |
| Monge do Sino | golem de pedra (grande) | cajado rubro |
| Kenkaku Veloz | demônio vermelho (chort) | espada de duelo |
| Carrasco | ogro (grande) | machado de guerra |
| Arqueiro das Cinzas | esqueleto | arco (flechas em pixel art) |
| Sombra | necromante encapuzado | faca |
| Escudeiro | anão de armadura | espada de cavaleiro |

Armas do jogador: Katana (katana), Montante (espadão), Gêmeas (dois facões), Naginata
(lança), Kusarigama (lâmina + corrente em elos), Manoplas (duas maças). Arenas montadas
tile a tile (laje limpa, rachaduras raras, caveiras, parede de tijolos com estandartes),
tingidas pela cor do bioma. Peças desenhadas por nós em pixel art no mesmo estilo:
braseiro, chamas animadas, pedaços de gore, ícones de itens (amuleto, anel, bracelete,
talismã de papel) com metal na cor da raridade e pedra na cor do elemento.
Flash de dano e "calor" do ataque são uma silhueta branca/colorida sobre o sprite.
Telegraphs, anéis de timing e partículas continuam feitos por código (são leitura de
jogo, não entidades). Créditos completos em `CREDITS.md`. Paleta de UI:
| Uso | Hex |
|---|---|
| Fundo | `#15141B` |
| Chão | `#23212C` |
| Borda da arena | `#3D3847` |
| Jogador | `#E8E2D0` |
| Destaque do jogador / parry | `#6FC3DF` |
| Inimigos | `#C9A66B` (Ronin), `#9DB4C0` (Lanceiro), `#B48EAD` (Monge) |
| Perigo | `#FF5A3C` + losango |
| Faísca | `#FFE08A` |
| Selo de técnica / Fluxo | `#F2C14E` |

## 11. Áudio
SFX gravados (CC0): ~140 variações em 14 categorias — cortes no ar, golpes pesados,
choques de lâmina, parry (katana contra lâmina, gravações profissionais), bloqueio
(cabo/guarda contra lâmina), impactos pesados e esmagamento, carne, sangue,
desembainhar/embainhar, passos de couro, esquiva. Cada evento sorteia uma variação (nunca
a mesma duas vezes seguidas) com leve variação de tom; eventos-chave somam por baixo o som
sintetizado antigo (o "ting" do parry perfeito, o baque da quebra de postura). Interface e
avisos continuam sintetizados.

Música: "Samurai Nights" (majadroid, CC-BY) separada em 5 camadas sincronizadas, cortadas
em loops exatos de 4 compassos a 140 BPM: qin (base, sempre) → percussão + baixo (combate)
→ erhu (Fluxo 1, cresce no 2) → flauta dizi (Fluxo 3 e menus).

## 12. Acessibilidade
Perigo nunca só por cor (forma + som + marca no chão). Indicador de tempo liga/desliga.
Intensidade de tremida 0–100%.
Opção de reduzir flashes. Controles remapeáveis (persistência pronta; tela de remapear
na Fase 6). Textos centralizados para tradução.

## 13. Balanceamento inicial
Ver tabela da seção 4 e `GameConfig.cs`. Hit-stop: acerto 4 f, bloqueio 3 f, parry
perfeito 7 f, contra-investida 10 f, finalizador 12 f, dano recebido 5 f.

## 14. Escopo (MoSCoW)
- **Must:** combate completo, 4–5 inimigos, 1 chefe, Técnicas Absorvidas, Fluxo,
  recompensas, telas obrigatórias, controle, remapeamento, Altar das Almas (feito).
- **Should:** desbloqueios horizontais (novas técnicas/relíquias no conjunto), corredores
  entre arenas, música real em camadas.
- **Could:** Rival/Nêmesis, segunda arma, biomas 2 e 3.
- **Won't (agora):** metroidvania, várias armas, multiplayer.
