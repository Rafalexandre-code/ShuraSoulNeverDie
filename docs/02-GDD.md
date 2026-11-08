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

### Bestiário (M1.10): cada inimigo novo traz uma mecânica
| Inimigo | Vida | Mecânica | Como responder |
|---|---|---|---|
| Kunoichi das Lâminas | 48 | Leque de 3–5 kunai/shuriken; salta para longe dos golpes | Aparar cada lâmina (devolve) e fechar a distância |
| Bombardeiro | 55 | Bombas no chão onde você pisa (pavio visível) | Sair do círculo ou esquivar na explosão |
| Tengu Espadachim | 70 | Postura de saque: golpeá-lo provoca contra-saque imediato | Esperar e aparar o saque |
| Onmyōji | 60 | Talismãs que explodem no chão; invoca fogos-fátuos (até 3) | Derrubá-lo primeiro |
| Xamã Curandeira | 65 | Aura: cura 8% e alivia a postura dos aliados a cada 2 s | Alvo prioritário; ela foge |
| Chōchin-obake | 40 | Explode ao morrer e fere também os inimigos | Matar longe de você, perto deles |
| Onryō | 45 | Etéreo: lâminas o atravessam | Parry perfeito o deixa sólido por 3 s |
| Kappa do Lago | 60 | Mergulha e irrompe sob você (círculo que te segue) | Esquivar quando o anel fechar |
| Oni Ciclope | 160 | Investida pela arena; não recua | Fazer bater em parede/estátua: fica atordoado |
| Lodo Faminto | 50 | Divide-se em dois lodos menores ao morrer | Finalização impede a divisão |
| Bárbaro Furioso | 95 | Fúria abaixo de 50%: golpes ~28% mais rápidos, sem pausa | Confiar no anel, que continua fiel |
| Ninja do Trovão | 60 | Cria 2 cópias que também atacam (1 golpe as derruba) | Achar o verdadeiro: as cópias somem com ele |
| Olho Vigia | 50 | Raios de 11 m que cruzam a arena; teleporta se você chega perto | Esquivar o raio; encurralá-lo |
| Kyūketsuki | 70 | Cura 100% do dano que causa; mordida (agarrão) | Não trocar golpes: aparar |
| Gashadokuro | 75 | Remonta os ossos uma vez (50% de vida) | Finalizar, ou golpear os ossos no chão |
| Macaco Boxeador | 60 | Fintas no meio da sequência (sem anel) | Aparar só quando o anel aparece |

Lacaios (nunca aparecem sozinhos): Hitodama (fogo-fátuo que explode ao morrer), Lodo pequeno,
cópia do Ninja do Trovão. Lacaios não deixam itens. Todos os 24 inimigos aparecem até a
profundidade 12, dois novos por sala, cada um primeiro sozinho.

### Técnicas do bestiário
Como os 8 originais, cada inimigo novo ensina uma técnica: domine o golpe-assinatura dele
(a Resposta Perfeita) e finalize-o. Bombas e talismãs também se dominam: esquive através
da explosão. Toda técnica tem um custo.

| Inimigo | Técnica | Ganho | Custo |
|---|---|---|---|
| Kunoichi | Leque de Kunai | Parry perfeito lança 3 kunai (6 dano, 8 postura) | Parry perfeito -15% postura |
| Bombardeiro | Pavio Curto | Esquivar deixa uma bomba (12/18), 1 a cada 3 s | -8 vida máx. |
| Tengu | Saque Iai | 1 s sem atacar: próximo golpe +80% dano, 2x postura | 2º/3º golpe -15% dano |
| Onmyōji | Selo Explosivo | Parry perfeito: selo explode sob o atacante (14/22) | Parry perfeito -20% postura |
| Xamã Curandeira | Bênção da Xamã | Finalização cura 10 de todo o grupo e zera sua postura | -10% dano |
| Chōchin-obake | Lanterna Viva | Todo inimigo que cai explode (12/18 nos outros) | +10% dano recebido |
| Onryō | Corpo Etéreo | Após parry perfeito, nada te acerta por 0,5 s | Esquiva recarrega 25% mais devagar |
| Kappa | Correnteza do Kappa | Esquiva perfeita devolve a carga e cura 4 | Esquivas 15% mais curtas |
| Oni Ciclope | Golpe do Oni | 2x postura em inimigos encostados em parede/estátua | -8% velocidade |
| Lodo Faminto | Fome do Lodo | Cada inimigo que cai cura 5 | -10 vida máx. |
| Bárbaro | Fúria do Bárbaro | Abaixo de 50% de vida: +40% dano | Acima de 50%: -10% dano |
| Ninja do Trovão | Cópia de Sombra | 3º golpe do combo repetido por uma sombra (8/10) | -8 vida máx. |
| Olho Vigia | Piscar do Vigia | Esquiva perfeita te teleporta às costas do atacante | Esquiva recarrega 20% mais devagar |
| Kyūketsuki | Sede de Sangue | Golpes curam 8% do dano; finalizações curam 12 | Cura de fim de sala -15 |
| Gashadokuro | Ossos Teimosos | 1x por sala, golpe fatal deixa com 1 de vida | -15 vida máx. |
| Macaco Boxeador | Contragolpe | Após bloquear: próximo golpe +50% dano, 2x postura | Bloquear desgasta 15% mais |

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

### Cooperativo online (até 8)
O anfitrião abre a sala; os amigos entram pelo navegador e veem a mesma sala (nome, aparência,
armas, "pronto"). **Cada duelista é um personagem próprio:** armas, itens, mochila, técnicas,
relíquias, bênçãos, Fluxo e sequência são individuais; ao vencer uma sala cada um escolhe a sua
recompensa; itens são de quem os pega. Só a semente, as salas, a profundidade, a ascensão e os
Ecos são do grupo. Online nada pausa: menus abertos deixam o personagem exposto. Com mais gente: arena até +112% de área, ondas ~50% maiores por
jogador (com teto), inimigos +8% de vida por jogador (Guardião +35%), um atacante ativo por
jogador (até 6) e ritmo de ataques mais curto. Inimigos escolhem alvo por distância e carga,
espalhando os duelos. **Reviver:** aliado em cima do caído segurando E por 2,4 s (mais ajudantes
= mais rápido; soltar perde o progresso aos poucos); caídos voltam com 50% na sala seguinte;
derrota só quando todos caem.

### Arenas e armadilhas
Cinco formas (salão, octógono, cruz, colunas, pátio com estátua central), 22–30 × 12–17
células, sorteadas pela semente. Armadilhas por profundidade, sempre com aviso visível antes:
espinhos (prof. 1+), jatos de fogo (3+), lâminas giratórias (5+). Inimigos sofrem 1,5× o dano
das armadilhas, recompensando quem as usa a seu favor.

## 7. Conteúdo do MVP
1 bioma, 4–5 tipos de inimigo, 1 chefe com 2 fases, ~20 recompensas, 1 arma.
Protótipo atual: 3 inimigos, 4 salas, 3 técnicas, 7 relíquias.

## 8. Interface
Telas: Título → Menu (Jogar, Altar das Almas, Configurações, Créditos, Sair) → Run (HUD) ⇄
Pausa (Continuar, Configurações, Recomeçar, Menu) → Recompensa → … → Morte/Vitória (Ecos
ganhos; Nova run, Altar, Menu) → Menu.
HUD: vida (com barra "fantasma" do dano recente), postura, cargas de esquiva, Fluxo, Ecos
da run, técnicas, sala atual. Números de dano sobre os inimigos.

**Estilo (M1.8): tinta sumi-e sobre papel washi**, com vermelho *shu* e dourado — arte própria
gerada por `Tools/ui_art/gen_ui.py` (pincel simulado com fibras secas). Elementos:
- Título/menu: pintura de paisagem (montanhas em aguada, sol vermelho, pinheiro, pássaros),
  pétalas de sakura caindo, logo "修羅" a pincel com selo *hanko*.
- Painéis: folhas de washi com bordas rasgadas e chamuscadas e moldura de tinta seca; item
  selecionado ganha uma pincelada vermelha que "pinta" da esquerda para a direita.
- Pausa, recompensa, equipamento e morte: o jogo congelado sob uma aguada de tinta nas bordas.
- Recompensas como talismãs (faixa de cor por tipo, selo quando escolhida); arsenal com a
  arma sobre uma pincelada; equipamento com o samurai dentro de um *ensō*.
- HUD: barras de vida/postura como pinceladas (vermelho com rastro de dano), esquivas como
  gotas de tinta, níveis de Fluxo como *ensō* dourados girando, dicas num rolo de papel.
- Fontes: Kaushan Script (pincel) para títulos/nomes/números; Spectral para texto.

## 9. Onboarding
Sala 1 = um único Ronin, com dicas de controle no chão. Sala 2 introduz o Lanceiro
(estocada) já sabendo aparar. Sala 3 introduz a Varredura isolada. Sala 4 combina.

## 10. Direção de arte
Pixel art livre: **"Ninja Adventure" de Pixel-Boy & AAA (CC0)**, tema japonês, visão de cima,
16 px por unidade para tudo (personagens, armas, piso, paredes), filtro Point. Os personagens
têm 4 direções (cima, baixo, esquerda, direita): andar, parado, pose de golpe e caído; a arma
gira 360° na mão. Elenco:

| Entidade | Sprite | Arma |
|---|---|---|
| Jogador | samurai de chapéu de palha (Samurai) | a arma equipada |
| Ronin Errante | samurai azul (SamuraiBlue) | espada |
| Lanceiro | soldado de elmo (GladiatorBlue) | lança |
| Monge do Sino | monge careca (Monk2), 1,45× | bastão (bō) |
| Kenkaku Veloz | ninja vermelho (NinjaRed) | ninjaku |
| Carrasco | oni vermelho (DemonRed), 1,65× | kanabō (clava) |
| Arqueiro das Cinzas | esqueleto (Skeleton) | arco (flecha do pacote) |
| Sombra | ninja negro (NinjaDark) | sai |
| Escudeiro | guerreiro de armadura (Knight), 1,15× | espada |

Armas do jogador: Katana, Montante (espadão), Gêmeas (dois sai), Naginata (lança de lâmina),
Kusarigama (machadinha + corrente em elos), Manoplas (dois martelos). Arenas de templo
montadas tile a tile, uma por bioma: Cinzas (pedra escura), Musgo (ladrilho verde
ornamentado), Sangue (tijolo avermelhado), Abismo (pedra arroxeada), Gelo (ladrilho de
templo azulado); moldura de parede do pacote, estandartes *nobori* na parede do fundo e
lanternas *andon* acesas nos cantos. Peças desenhadas por nós em pixel art: pedaços de gore,
elo de corrente, ícones de itens (amuleto, anel, bracelete, talismã de papel) com metal na
cor da raridade e pedra na cor do elemento.
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

Música padrão (Ninja Adventure, CC0): "Temple" fora de combate e nos menus, "Fight" em combate e o segundo "Fight" a partir do Fluxo 2, com transições suaves. Alternativa nas Configurações: "Samurai Nights" (majadroid, CC-BY) separada em 5 camadas sincronizadas, cortadas
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
