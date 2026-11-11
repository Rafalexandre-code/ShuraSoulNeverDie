# 05 — Expansão de Design (checklist)

Mapeia cada item do documento **"Shura — Análise e Expansão de Design"** (8 nov 2026) para o que está no jogo.
A implementação segue as fases do próprio documento (§11): combate → estrutura da run → builds → meta/narrativa → online.

Legenda: ✅ feito · 🟡 parcial (ver nota) · ⏳ pendente (fase indicada) · ⛔ adiado (motivo)

## Fase 1 — Combate: novas camadas (§3) — ✅ concluída

| § | Item | Estado | Notas de implementação |
|---|---|---|---|
| 3.1 | Fôlego e Respiração (Kokū) | ✅ | Fôlego 100; ataque −8, esquiva −15; regen 30/15/8 por s (parado/andando/atacando). Zerado = exausto 1,5 s (−40% velocidade, janela de parry pela metade, inimigos com leitura PRESSÃO te caçam). Brilho do Respiro após o último golpe do combo: esquivar nele devolve 70% e soma +1 na sequência. Inimigos também têm fôlego: 3 parries perfeitos seguidos os deixam **sem fôlego** (3 s sem golpes perigosos). |
| 3.2 | Selos de Ki (dano interno) | ✅ | Cada parry perfeito marca um selo (máx. 5, somem em 8 s). **G** ou ataque+aparar juntos detonam: 12 de dano × arma e 8 de postura por selo; 5 selos com postura ≥70% quebram na hora. Elemento Raio encadeia a detonação. |
| 3.3 | Posturas de guarda (Kamae) | 🟡 | 4 posturas (Jōdan/Chūdan/Gedan/Waki) com os efeitos da tabela. **Desvio:** troca por **1–4 / direcional** em vez de "segurar Q + direção", porque Q troca de arma ao apertar. Os 24 movesets por postura viraram modificadores por postura (dano, janela, esquiva, contra-investida, golpes segurados). |
| 3.4 | Novos tipos de anel | ✅ | Anel tracejado (finta — aparar abre você), duplo concêntrico, roxo pontilhado (espiritual: só Troca Perfeita ou esquiva), branco partido (golpe de grupo: dois aparando juntos), verde com marcas (maldito: bloquear amaldiçoa 4 s). Cada tipo tem padrão próprio (daltonismo). Onryō, hitodama (espirituais), Vampiro, Onmyōji, Esqueleto (malditos) e elites online (grupo) usam. |
| 3.5 | Respostas de mestre (Mikiri ampliado) | ✅ | Varredura: esquivar para cima = **Descida** (próximo golpe ×2 de postura). Agarrão: esquivar para trás = **Arremesso**. Estocada: contra-investida em Chūdan ×1,3 e empurrão; esquiva tardia = **Pisão** (desarma + 40 de postura). |
| 3.6 | Ferimentos localizados | ✅ | Braço/perna/tronco/olhos no jogador e no inimigo, com os efeitos da tabela (troca desligada, esquiva curta, sangramento/−20% postura máx., anéis somem 3 s). Bandagem (**H**) cura um. Crítico de Sombra fere os olhos do inimigo (erra flechas). |
| 3.7 | Posicionamento e ambiente | ✅ | Golpe pelas costas +50% de postura; empurrão contra parede +15 de postura; empurrar um inimigo em outro derruba os dois; **fossos** no grid (cair = morte do inimigo). Flechas desviadas que acertam outro inimigo os fazem brigar entre si. |
| 3.8 | Impasse de saque (Iai) | ✅ | ~20% das salas de combate (prof. ≥2, nunca guardião) começam em impasse: barras pretas, música abafada, 0–3 fintas. Soltar o ataque no avanço real mata o líder; com Fluxo cheio, até mais 3. Cedo ou nunca = −30 de vida. Funciona online (cada um solta o seu). |
| 3.9 | Finalização com corte dirigido | ✅ | 40 frames de mira com linha dourada: na linha = cura 10 + Fluxo; no braço = drop em dobro; na cabeça (prof. ≥12) = ecos extras. Invulnerável durante a mira. |
| 3.10 | Troca Perfeita 2.0 (Elos) | ✅ | Trocar no meio do combo dá um Elo com nome próprio (15 pares nomeados, simétricos); 3 elos = **Ciclo das Duas Lâminas** (4 s, +20% dano, alternância automática). Carga por arma (0–3) liberada ao trocar. |
| 3.11 | Medidor de Shura (修羅) | ✅ | Matar sem misericórdia sobe; parry perfeito acalma. Ira / Sede (anéis 15% menores) / Frenesi (postura não quebra, anel some) / Queda: 10 s como Asura (golpes imbloqueáveis) e depois o **Asura Interior** — cópia sombria com as suas armas, que só caça você. |
| 3.12 | Mushin | ✅ | Sequência ≥10 com Fluxo 3: HUD some (só 無心), música abafada (passa-baixa), perfeitos valem mais. Um golpe sofrido encerra. |
| 3.13 | Ferramentas e cura ativa | ✅ | Cabaça (**F**, 2 goles por sala, te deixa parado). 6 ferramentas (**R** usa, **T** troca): shuriken (interrompe saltos), fumaça (confunde e apaga a leitura da IA), sino (expõe espíritos), petardo (atordoa bestas), óleo (lâmina em chamas), corrente (puxa). Gastam papéis espirituais; finalizações devolvem. |
| — | Controles novos | ✅ | Teclado e controle (G/V/R/T/F/H/1–4; RT/LT/B/L3/direcional) no jogo e no navegador. Acordes ataque+aparar (detonar) e aparar+esquiva (kiai) com tolerância de 5 frames. |
| 5.5 | Afixos de elite (adiantado) | ✅ | Prof. ≥15: guardiões com 1–3 afixos (Vingativo, Espelhado, Possuído, Gêmeo, Velado, Ancestral), nome escrito sobre a cabeça. |
| 9.1 | Kiai em grupo e ressonância (adiantado) | ✅ | Kiai (**V**) gasta 1 Fluxo e interrompe golpes vermelhos; 3 duelistas juntos em 1 s quebram todas as posturas. Parries de vários jogadores no mesmo inimigo somam ressonância (×1,25 por aliado, até ×2). |

Também: HUD do anfitrião e do navegador mostram fôlego, Shura, kamae, cabaça, ferramenta/papéis, ferimentos e carga; o navegador desenha os novos anéis, selos, afixos, a linha dourada e o impasse.

## Fase 2 — Estrutura da run e mundo (§6) — ✅ concluída

| § | Item | Estado | Notas de implementação |
|---|---|---|---|
| 6.1 | Mapa de rotas | ✅ | Entre salas, um pergaminho mostra 2–3 portas com símbolo (kanji), tipo e recompensa, e mais 2 camadas à frente (3 salas reveladas). Portas ligam-se às da camada seguinte a no máximo uma coluna: toda porta leva adiante e toda porta é alcançável (testado). Online, cada um vota (clique ou 1–3, 25 s); empate decide o anfitrião. A "Visão do Destino" (+1 sala) entra com as relíquias novas da Fase 3. |
| 6.2 | Combate | ✅ | O símbolo da porta muda a recompensa: Relíquia (só relíquias), Bênção, Item (um item entre as cartas). |
| 6.2 | Duelo | ✅ | Um contra um (também online: o inimigo não ganha escolta, só vida), arena octogonal, começa em impasse Iai. A técnica do inimigo é garantida — e, se você já a tem, ela sobe de nível. |
| 6.2 | Desafio | ✅ | Só aparar (esquivar perde), Sem tomar dano, ou 45 s. Cumprido: item raro ou melhor para cada um. Faixa no HUD mostra o estado. |
| 6.2 | Disputa | ✅ | Yokai × humanos já brigando entre si; recompensa: escolha dupla (duas cartas de quatro, para cada jogador). |
| 6.2 | Santuário | ✅ | Cura 25% ao entrar; Meditar (−20 Shura, fôlego cheio), Tratar ferimentos (+1 bandagem), Purificar maldição, Guarda padrão (a kamae com que cada sala começa). |
| 6.2 | Mercador | ✅ | **Mon** (moeda da run, por jogador): abates (3, elite 12) e salas (8 + prof./2). Vende 3 itens (cada jogador compra a sua cópia), bandagem, papéis, gole extra permanente. |
| 6.2 | Ferreiro errante | ✅ | Fundir (3 itens da mesma raridade → um acima), Gravar um kanji (2 Aço Celeste: troca o atributo mais fraco), Quebrar (item → Aço Celeste), Afiar (+6% dano). Os corrompidos se consertam pelo feito (5 finalizações sem tomar dano). |
| 6.2 | Evento | ✅ | 8 eventos (6.7). |
| 6.2 | Tesouro amaldiçoado | ✅ | Item épico ou melhor + maldição de 3 salas (Sem cura, Frágil, Desajeitado, Inquieto). |
| 6.2 | Covil do Rival | ⏳ | Depende do sistema Nêmesis (Fase 5). |
| 6.2 | Guardião / Senhor do Bioma | 🟡 | Guardião como antes; os chefes de bioma são da Fase 5. |
| 6.3 | Cinzas | ✅ | Fogo dura 2×; erupções marcadas no chão (aviso + explosão que fere todos). |
| 6.3 | Musgo | ✅ | Esquiva 30% mais longa; nuvens de esporos: dentro delas você não vê os anéis. |
| 6.3 | Sangue | ✅ | Cura −50%, roubo de vida dobrado; poças que enchem o Shura. |
| 6.3 | Abismo | ✅ | Escuridão (só o raio de 5,5 m em volta de você), anéis só a 5 m, bordas/fossos onde inimigos empurrados caem. |
| 6.3 | Gelo | ✅ | Fôlego volta 40% mais devagar; gelo fino quebra sob a Montante (e o Kanabō quando existir); 35% dos inimigos nascem congelados e despertam quando você chega perto. |
| 6.3 | Bambuzal (novo) | ✅ | Touceiras de bambu: um golpe derruba o bambu na direção do corte, ferindo e atordoando inimigos na linha. Kunoichi e Sombras descem das copas ao lado de você. |
| 6.3 | Castelo (novo) | 🟡 | Biombos de shōji que bloqueiam e rasgam com um golpe; salas mais estreitas. **Falta:** silhuetas atrás do papel e ataque através dele. |
| 6.3 | Rio Sanzu (novo) | 🟡 | Todos andam 10% mais devagar, Raio +50%, mais kappas. **Falta:** "passos fazem barulho" e os afogados (inimigos novos, Fase 5). |
| 6.3 | Yomi (novo, prof. 40+) | 🟡 | Tudo é etéreo até um parry perfeito. **Falta:** rivais mortos voltando (Nêmesis, Fase 5). |
| 6.3 | Zona corrompida | ✅ | 15% das salas de combate (prof. 6+): névoa roxa que drena o fôlego e fortalece yokai, até você quebrar a pedra-núcleo. |
| 6.4 | Hora do dia | ✅ | Manhã → tarde → crepúsculo → noite → aurora a cada 3 salas, com tinta na tela. Noite: mais yokai, menos humanos. Aurora: os mortos nascem com −25% de vida. |
| 6.4 | Clima | ✅ | Chuva (fogo −50%, raio +50%), vento (projéteis fazem curva, folhas mostram a direção), neblina (inimigos somem além de 6 m), tempestade (raios marcados que preferem quem usa aço), neve (invisíveis ficam visíveis), lua cheia (só à noite: Shura +50% para todos, inimigos +10% de dano). 🟡 O sangue lavado pela chuva é só visual da chuva. |
| 6.5 | Arenas destrutíveis | 🟡 | Shōji rasgam, bambus caem, lanternas derrubadas espalham óleo em chamas, estátuas racham após 3 golpes fortes e deixam de bloquear. **Falta:** estandartes cortados (são pintados na imagem da arena). |
| 6.6 | Kalpas | ✅ | A cada 25 salas, o Portão do Shuradō: escolha 1 de 3 regras permanentes (Lâminas Trocadas, Fluxo Inimigo, Herança dos Mortos, Marcas Antigas, Golpes Ligeiros, Guardiões Gêmeos); do Kalpa V em diante, duas por portão. Depois da prof. 25 os números sobem 40% mais devagar. O "Ashura mais forte" é da Fase 5. |
| 6.7 | Eventos | ✅ | Monge Mendigo, Kitsune Disfarçada (chá ou duelo com a raposa — inimigo novo), Jizō, Ronin Ferido (vira **aliado** por 3 salas ou te dá a técnica), Dados Chō-han, Onsen, Lâmina de Muramasa, Duelo de Honra (sem técnicas e sem itens). O **Karma** já é contado para a Fase 4. |

Também: a linha do HUD mostra porta, hora, clima, Kalpa e a regra do bioma; o navegador recebe tudo (mapa e votação, zonas, objetos, escuridão, neblina, clima, Mon e maldição) e usa os objetos dos encontros com E. Correções feitas no caminho: salas escaladas para o grupo perdiam o tipo e o impasse (bug também da Fase 1); um grupo que caía no instante da vitória não leva mais "fim de jogo" na sala seguinte.

## Fase 3 — Armas, técnicas e builds (§4) — ✅ (v0.5.0 e v0.6.0; só o editor de kata ficou para depois)

| § | Item | Estado | Notas |
|---|---|---|---|
| 4.1 | Forja modular | ✅ | Botão **Forja** no Arsenal: cada arma escolhida troca a **empunhadura** (golpe da troca e finalização de qualquer uma das 10 armas) e a **tsuba** (12 passivos: Lua = +1 selo por parry, Sol, Ferro, Garça, Vento, Sangue, Trovão, Raposa, Dragão, Bronze, Carpa, Lótus). 10 lâminas × 10 empunhaduras = 100 armas, com nomes próprios para algumas (Katana + cabo de Naginata = **Nagamaki**; Daishō, Kodachi, Punhos de Oni...). Fica salvo por lâmina; a maestria segue a lâmina. Online, cada um leva a sua arma forjada. |
| 4.2 | Quatro armas novas (Tessen, Kanabō, Shakujō, Wakizashi + Kunai) | ✅ | **Tessen**: cada abano rebate flechas; troca **Vento Dobrado** (devolve todos os projéteis da sala e alivia a postura do grupo); finalização **Fechar o Leque** (rajada cortante). **Kanabō**: lentíssimo, sem bloqueio, todo golpe atordoa; troca **Terremoto**; finalização **Esmagar o Oni**. **Shakujō**: torna sólidos os etéreos, +30% de dano em yokai; troca **Badalada**; finalização **Exorcismo** (lacaios yokai somem, os outros queimam). **Wakizashi + Kunai**: o 3º golpe lança uma kunai que volta à mão, golpes pelas costas causam o dobro; troca **Sombra Dupla** (aparece nas costas do inimigo); finalização **Golpe da Verdade Oculta** (você some e os inimigos perdem você). O Arsenal agora mostra as 10 armas, e os 45 Elos entre elas têm nome próprio. |
| 4.3 | Maestria de arma | ✅ | 5 níveis por arma, por finalizações feitas com ela (5, 15, 30, 50, 80), salvos no seu progresso. I: +15% de postura; II: finalizar devolve 1 papel espiritual; III: golpe da troca +20% de dano; IV: o rastro ganha a cor do estilo; V: o **Ougi** — o primeiro golpe da troca de cada sala vira o golpe secreto da arma (2,5× dano e postura, com nome próprio). Os cartões do Arsenal mostram o nível e o caminho até o próximo. Online, cada convidado sobe a própria maestria (e a leva para a sala). |
| 4.4 | Técnicas com níveis, ramos, marcas e 8 fusões | ✅ | Absorver de novo o mesmo inimigo sobe a técnica. **II**: o preço some e as marcas se fortalecem. **III**: duas cartas lado a lado, um ramo para sempre — **Forma Plena** (números da técnica 50% maiores) ou **Eco** (sempre que a técnica age, suas marcas respondem). **Marcas** (1–2 por técnica, mostradas na carta): 斬 Corte, 守 Guarda, 影 Sombra, 霊 Espírito, 血 Sangue, 火 Fogo, 風 Vento, 雷 Trovão — cada nível acima de I dá o bônus da marca; o Eco de cada marca é um efeito (corte de vento, 0,4 s intocável, explosão de sombra, fogo-fátuo, sangria + cura, queimadura, lua de vento, raio em cadeia). **8 fusões** (carta rara quando as duas metades são suas, sem preço): Lua Sangrenta, Corvo Duplo, Sino Tempestuoso, Pavio do Oni, Lanterna Fantasma, Muralha Viva, Olho do Trovão, Correnteza Faminta. |
| 4.5 | Transformação em alma | ✅ | Uma técnica no nível III guarda a alma do inimigo. **B** (ou R3), uma vez por sala: por 8 s você vira esse inimigo (o corpo dele, também para os outros online), com +35% de dano e de postura e o jeito dele de lutar: o Onryō fica intocável, o Carrasco e o Kappa puxam o que acertam, o Olho Vigia e o Raijin soltam raios, o Arqueiro e a Kunoichi atiram, a Lanterna e o Onmyōji lançam fogos-fátuos, os voadores andam 25% mais rápido. O HUD mostra a alma pronta e o tempo que falta. |
| 4.6 | Itens com identidade (afixos únicos, sets de clã, corrompidos, forja de itens) | ✅ | **Afixo Único** em todo item Lendário ou melhor: Rosa de Ferro (bloquear vale meio parry), Olho de Hannya (anéis avisam 30% antes), Pena do Tengu (a esquiva vira teleporte curto), Sino Rachado (o Mushin aguenta o primeiro dano), Lágrima da Kitsune (o 1º golpe em cada inimigo +60%), Âncora do Kappa (finalizar devolve esquiva e 1 s intocável). **Clãs** (30% dos itens Incomuns+): Takeda, Hattori, Minamoto, Shimazu, com bônus de 2 e de 4 peças (Hattori 4 = esquivar deixa uma cópia de sombra). **Corrompidos** (12% dos Épicos+): atributos ×1,4 e um defeito (anéis invisíveis, frágil, pesado, −1 esquiva) que some ao finalizar 5 inimigos sem tomar dano. **Forja** no Ferreiro: Fundir 3 itens da mesma raridade → um acima; Gravar um kanji (2 Aço Celeste) troca o atributo mais fraco; Quebrar um item dá **Aço Celeste**. |
| 4.7 | Reações elementais | ✅ | Um inimigo marcado por um elemento (pip colorido sobre ele, ~4 s) e atingido por outro reage: Fogo+Gelo = Vapor (cega quem está perto), Gelo+Raio = Estilhaço (60 de postura), Fogo+Vento = Tornado Ígneo (puxa e queima), Sangue+Sombra = Maldição (cada golpe dele te cura), Raio+Sangue = Sobrecarga (o sangramento salta para 3), Gelo+Vento = Nevasca (cone que congela). Aço puro sobre um elemento = Ressonância (o elemento dura o dobro). |
| 4.8 | 8 relíquias novas | ✅ | Máscara Hannya (Shura 2× / Asura Interior +50% vida), Pergaminho em Branco (copia a última técnica absorvida / −1 opção), Sandálias de Palha (esquiva infinita / sem bloqueio), Voto de Silêncio (Mushin com 6 / sem interface), Corrente do Carrasco (agarrar com E / −15% velocidade), Ampulheta Quebrada (parry desacelera o tempo / anéis 10% mais rápidos), Moeda de Seis Mon (revive uma vez / metade dos Ecos), Olho do Dragão (vê o próximo golpe de cada inimigo / a IA aprende 2× mais rápido). |
| 4.9 | Editor de kata | ⏳ | Provável adiamento: exige UI de edição de combos. |
| 4.10 | Arquétipos de build | ✅ | Marcas, fusões, clãs, afixos únicos, as 10 armas, a forja e as almas formam os caminhos (parry e selos, esquiva e sombra, peso e atordoamento, espírito e yokai...). |

## Fase 4 — Progressão, meta e narrativa (§7, §8) — ⏳

| § | Item | Estado |
|---|---|---|
| 7.1 | Templo em Ruínas (hub) | ⏳ |
| 7.2 | Moedas (Mon, Aço Celeste, Fragmentos, Pétalas) | ⏳ |
| 7.3 | Altar em árvore | ⏳ |
| 7.4 | Juramentos | ⏳ (os botões de regra já existem: regen de fôlego, ganho de Shura, velocidade de adaptação, afixos extras) |
| 7.2 | Mon | ✅ (Fase 2) — as outras moedas entram aqui |
| 7.5 | Linhagem e envelhecimento | ⏳ |
| 7.6 | Pergaminhos de inimigo | ⏳ |
| 7.7 | Liberação em camadas | ⏳ |
| 7.8 | Modos extras | ⏳ |
| 8.1–8.7 | Premissa, Casa de Chá, Karma, jisei, gestos, finais, história no ambiente | ⏳ |

## Fase 5 — Inimigos, IA e online (§5, §9) — ⏳

| § | Item | Estado |
|---|---|---|
| 5.1 | Rivais do Shuradō (Nêmesis) | ⏳ |
| 5.2 | Chefes de bioma (Kagutsuchi, Jorōgumo, Ō-Kyūketsuki, Gashadokuro Colossal, Yuki-onna, Ashura) | ⏳ |
| 5.3 | Ecologia e facções | ⏳ |
| 5.4 | Formações e combos | ⏳ |
| 5.5 | Afixos de elite | ✅ (Fase 1) |
| 5.6 | IA adaptativa 2.0 | ⏳ |
| 5.7 | Moral e rendição | ⏳ |
| 5.8 | Doze inimigos novos | ⏳ |
| 9.1 | Ações conjuntas | 🟡 kiai em grupo, ressonância e golpe de grupo feitos (Fase 1) |
| 9.2–9.7 | Ameaça e papéis, queda e resgate, Pacto de Sangue, presença assíncrona, PvP, lobby como lugar | ⏳ |

## HUD de batalha (pedido do diretor, antes da Fase 3) — ✅

- **Cartão do duelista** (canto superior esquerdo): o seu próprio personagem, grande, num medalhão de ensō, segurando a arma na pose da guarda atual (Jōdan alta, Chūdan à frente, Gedan baixa, Waki escondida atrás) — e balançando, esquivando, bebendo ou caído conforme o que você faz; as 4 guardas como figurinhas da sua silhueta (a atual acesa, com a tecla); vida, postura, fôlego e Shura; esquivas; um boneco de papel que acende em vermelho as partes feridas.
- **Cinto** (canto inferior esquerdo): as duas armas (a da mão acesa, com a carga), uma **cabaça** por gole (cheia/vazia), bandagens, a ferramenta com o seu ícone e os papéis espirituais (ofuda), e a bolsa de Mon — cada um com a tecla ao lado.
- **Registro** (canto inferior direito): itens coletados, inimigos novos, técnicas, recompensas, Fluxo, clima… tudo o que antes cruzava o centro da tela agora vira uma linha com horário; linhas iguais seguidas se juntam. A caixa de dicas foi para o centro, entre o cinto e o registro.
- O mesmo HUD (retrato, guardas, boneco, cinto, registro) no navegador.

## Ajustes do diretor (após o HUD) — ✅

- **HUD com ícones**: gota de sangue (vida), tsuba (postura), redemoinho (fôlego), máscara oni que pulsa com o nível (Shura), pena (esquiva), chama de alma (ecos) — no jogo e no navegador.
- **Corte dirigido refeito**: a linha dourada sai sempre de você até um ponto aleatório do corpo do inimigo (nunca mais "na vertical com o inimigo na horizontal"). A sua mira acende dourada ao encostar na linha. Precisão: **perfeito** (≤4°: +25 vida, +Fluxo, +15 Mon, ecos extras, saque em dobro, acalma o Shura), **na linha** (≤12°), **limpo** (≤24°). O sangue cresce muito quanto mais perto da linha.
- **Golpe espiritual (anel roxo pontilhado)**: o aparar comum não segura (regra do documento), agora explicado por dica e por aviso "ATRAVESSOU!"; com um **Talismã** equipado, o aparar perfeito volta a funcionar.
- **Online pela internet via Steam (Spacewar, AppID 480)**: o anfitrião abre a sala online e ela aparece na Steam; os amigos abrem o Shura → "Entrar num duelo (Steam)" (ou aceitam o convite) e o jogo deles cria um túnel pelo relay da Steam e abre o duelo no navegador local. Sem VPN e sem abrir portas. Guia para os jogadores: `docs/COMO_JOGAR_ONLINE.txt`.

## Online nativo, sem navegador (v0.4.0) — ✅

- **Cada um na janela do próprio Shura**: quem entra num duelo (Steam, convite ou "Entrar por IP") joga dentro do jogo, não no navegador. O anfitrião continua rodando a única simulação; o convidado reconstrói a mesma arena pela semente (estátuas, lanternas, armadilhas), desenha cada duelista, inimigo, flecha, bomba, item, zona, objeto e oferenda a partir dos snapshots, e toca cada golpe, parry, sangue e finalização pelos próprios efeitos e sons do jogo. HUD, registro, dicas, cartas de recompensa (de cada um), votação de rota, menu (o duelo continua), equipamento, sala entre runs (pronto, nome, aparência, armas) e "a morte ensina" — tudo nativo.
- **Latência ao mínimo**: o seu samurai é previsto no seu PC (os passos ainda não confirmados pelo anfitrião somam-se à última posição dele, com correção suave) e vira para a mira na hora; o resto é desenhado na linha do tempo do anfitrião com o menor atraso que o jitter da rede permite (relógio estimado + buffer adaptativo); os efeitos do próprio jogador tocam na hora, os dos outros quando o golpe deles aparece. Na Steam: ICE ligado (conexão direta PC-a-PC quando as redes deixam; relay da Valve só de reserva), nada espera pelo Nagle, snapshots e entrada vão "não confiáveis" (o próximo substitui o perdido; os que levam golpes/sangue vão confiáveis), textos grandes comprimidos (deflate). A entrada sai a cada mudança (e se repete 2×), com cada botão contado — pacote perdido nunca perde um aperto. O anfitrião agora lê a entrada dos convidados antes de simular o quadro (−1 quadro) e envia 45 snapshots/s. O ping aparece no canto da tela.
- **Entrar por IP** (mesma rede / Radmin): o mesmo cliente nativo, por WebSocket direto ao anfitrião. O navegador continua funcionando para quem não tem o jogo.
- Testado: anfitrião + convidado nativo na mesma máquina (bots), salas, recompensas, rota, sem exceções.

## Interface por resolução e visão limpa (v0.4.1) — ✅

- **HUD em duas camadas**: menus continuam numa página 1920×1080 ajustada à tela; o HUD da luta usa a tela inteira presa às bordas e cresce mais devagar que a resolução (em 2K/4K sobra espaço, as coisas não ficam gigantes). Novo padrão 80% e opção **Tamanho da interface** (50–130%).
- **Nada do HUD tapa a luta**: cada painel (cartão, cinto, título, Fluxo, grupo, registro, dica) fica quase transparente enquanto um duelista ou inimigo está embaixo dele, em qualquer posição do mapa.
- **Câmera mais afastada** (125% por padrão) e opção **Campo de visão** (90–170%).
- **Dicas** compactas à esquerda, sob o cartão (nunca no centro), e opção para desligá-las.
- **Registro começa recolhido** numa aba no canto (com o número de linhas novas); clique ou C / R3 abre e fecha.
- Configurações novas: **Resolução** (a do monitor ou qualquer outra que ele ofereça), tamanho da interface, campo de visão, dicas. Textos flutuantes no mesmo ponto agora se empilham em vez de se sobrepor.

## Telas refeitas (v0.4.2) — ✅

- **Menu inicial**: o quadro cresce com a lista; nada sai para fora. A versão aparece no canto.
- **Mapa de rotas**: lido da esquerda para a direita — você (o seu samurai), as portas de agora em cartões grandes, e mais duas camadas em círculos de tinta. Caminhos de tinta ligam cada porta às que ela leva; os caminhos da porta destacada ficam vermelhos. Cima/baixo, setas ou mouse escolhem; online mostra os votos e o tempo. O mesmo mapa para o convidado.
- **Configurações**: seções (Som, Imagem, Jogo e acessibilidade), barras de tinta que se arrastam com o mouse, interruptores, escolha de resolução com setas, os controles à direita e uma nota explicando a opção destacada.
- **Créditos**: em duas colunas (quem fez / do que é feito), com a direção criativa de Rafael Alexandre de Pinho Lopes.

## Sala online, convites e Altar de cada um (v0.4.3) — ✅

- **Sala online refeita**: à esquerda o que você faz e uma linha de situação (sozinho / esperando N prontos / todos prontos); à direita os 8 lugares do grupo (personagem, armas, anfitrião / ✓ pronto / escolhendo, ping) e "Como os amigos entram": Steam (situação e passos) e mesma rede/Radmin (endereços que se copiam com um clique). O convidado vê a mesma sala, com o próprio ping.
- **Convidar amigos (Steam) consertado**: o botão abria a sobreposição da Steam, que não aparece num jogo aberto fora da Steam. Agora abre a lista de amigos dentro do jogo (foto, quem está com o Shura aberto, online, ausente) e envia o convite direto no chat da Steam; aceitar com o Shura aberto entra na sala. "Entrar no jogo" pela lista de amigos da Steam também leva à sala.
- **Altar das Almas de cada um**: cada convidado entra com as próprias melhorias (enviadas ao anfitrião), quem joga pelo navegador entra sem nenhuma, e os Ecos da run vão para o Altar de cada convidado ao fim dela (ou ao sair). O Altar fica acessível na sala do convidado.
- **Nome inicial = nome da Steam** para quem nunca escolheu um nome.

## Guardas que se veem e o Dojo (v0.4.4) — ✅

- **Cada guarda tem o seu corpo e os seus cortes** (antes só mudavam números):
  - **Jōdan** — a lâmina fica erguida acima da cabeça; os cortes descem em diagonal longa (kesa-giri), as estocadas vêm de cima, e o passo no golpe é 30% mais fundo.
  - **Chūdan** — a ponta aponta para o inimigo desenhando um pequeno círculo; cortes de lado a lado, estocadas retas.
  - **Gedan** — a lâmina pende baixa, para o chão à frente; todo corte sobe (kiri-age), no sentido contrário; passos curtos.
  - **Waki** — a lâmina se esconde atrás do quadril, meio transparente; os cortes saem de trás num chicote largo, como um saque iai.
  - O aparar também muda de lugar em cada guarda, e trocar de guarda gira a lâmina uma volta inteira com um rastro de luz. Alcance, dano e tempo de cada golpe não mudam com a pose (nenhuma guarda é um enfraquecimento escondido). Online, todos veem as poses.
- **O Dojo (tutorial)**: abre sozinho na primeira vez em "Jogar" e fica no menu ("Tutorial (o dojo)"). São 18 lições, e cada uma só termina quando você faz o que ela pede: andar até as marcas, atacar, o combo, bloquear, o aparar perfeito, o golpe vermelho, quebrar a postura, finalizar com a linha dourada, trocar de arma, a Troca Perfeita, as quatro guardas, a cabaça, as ferramentas, os selos, o kiai e uma luta de verdade contra um ronin. Nos golpes de tempo (aparar, esquivar, Troca Perfeita), as primeiras vezes **param o tempo no instante exato** e esperam o botão ("AGORA!"), e depois você repete sozinho. Um Mestre do Dojo só ataca quando a lição pede, ninguém morre, não há armadilhas nem explosões. Na pausa dá para pular uma lição ou o dojo inteiro.

## Fase 3, parte 1 (v0.5.0) — ✅

- **Maestria de arma** (4.3), **técnicas que evoluem e se fundem** (4.4), **itens com identidade** (4.6), **reações elementais** (4.7) e **8 relíquias novas** (4.8) — detalhes na tabela da Fase 3 acima.
- A parte 2 veio na v0.6.0.

## Fase 3, parte 2 (v0.6.0) — ✅

- **Quatro armas novas** (4.2), **forja modular** (4.1) e **transformação em alma** (4.5) — detalhes na tabela da Fase 3.

## Controle, luz e acabamento (v0.7.0) — ✅

- **Controle de Xbox e PlayStation**: layout de soulslike (RB atacar, LB aparar, A esquivar, Y trocar, X interagir, R3 travar, L3 alma, B toque = cabaça / segurar = bandagem, RT/LT ferramenta, direcional = guardas, View: toque = equipamento / segurar = registro). Os nomes dos botões na tela mudam sozinhos entre Xbox (A, RB...) e PlayStation (✕, R1...), e vibração no aparar perfeito, na Troca Perfeita, ao ser ferido, na quebra de guarda e nas finalizações.
- **Trava de mira**: no controle é sempre ligada (escolhe o alvo sozinha; R3 escolhe de novo à frente; empurrar o analógico direito pula para o inimigo daquele lado; se o alvo cai, passa para o mais próximo). No teclado e mouse é opcional nas configurações (Z / mouse 5 trava, a roda troca de alvo); desligada, a mira é pelo mouse como sempre. Retícula vermelha no alvo, câmera que enquadra os dois (opcional) e funciona também para convidados online.
- **Configurar controles**: tela com abas Teclado e mouse (principal e alternativa) e Controle; escolha a célula e aperte a nova tecla. Se ela já era de outro comando, os dois trocam; Delete / Y limpa; cada dispositivo volta ao padrão sozinho. Salvo por espaço de tecla.
- **Manoplas do Leão** (antes "de Ferro"): arte nova (punho de aço saindo da boca de um leão de bronze) e luta de boxeador — um punho guarda o rosto enquanto o outro soca (recolhe, gira o quadril, estica, volta), jab, direto, gancho e cruzado; finalização "Coração Partido" com oito golpes no corpo, o mundo desacelera, e um direto que solta o **Rugido do Leão**.
- **Lanterna de pedra (tōrō)** no lugar da antiga, com versão com musgo, e colisão tirada dos pixels da base.
- **Luz**: as lanternas são o fogo da sala. Cada uma ilumina e tremula; quebrar uma escurece a sala, mais forte do lado dela; com todas quebradas a sala fica realmente escura (resta um pequeno halo em volta de cada duelista).
- **Pós-processamento** (opção em Imagem): brilho (bloom) no fogo e no aço, gradação de cor por hora do dia (manhã luminosa, tarde dourada, crepúsculo rosado, noite azul, aurora perolada), sombras frias e luzes quentes, vibrância, vinheta, aberração cromática leve e grão de filme.
- **Legibilidade**: textos que saltam sobre a cabeça agora formam uma coluna (sem um em cima do outro, e repetidos se juntam); nomes longos de itens e armas não saem mais das caixas (Equipamento, cinto do HUD, Arsenal, controles).

## Apresentação e acessibilidade (§10) — ⏳

10.1 tabela de impacto · 10.2 câmera e finalizações em tinta · 10.3 som que informa · 10.4 música adaptativa (🟡 música abafada em Mushin/Iai já feita) · 10.5 interface diegética · 10.6 luz 2D (provável adiamento: o projeto usa Built-in RP; luz 2D exige URP) · 10.7 acessibilidade (🟡 anéis com padrão próprio por tipo) · 10.8 replay e foto.
