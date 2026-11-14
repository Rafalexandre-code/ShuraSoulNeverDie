# SHURA — Plano de Redesenho "Ponta-Cabeça" (v0.16 → v1.0)

> **Documento-prompt para a IA de código.** Leia inteiro antes de mexer em qualquer arquivo.
> Autor: diretor do projeto (Rafael), com análise do Claude · 14 nov 2026
> Base: `Shura — Relatório Completo do Jogo v0.15.0` + `Guia Completo: Como Criar um Bom Jogo que Vende na Steam` (Partes 1 a 12 e 14; marketing fica de fora) + literatura de game design e lições de jogos publicados.

---

## Sumário

- **PARTE A** — Instruções de execução para a IA (leia primeiro)
- **PARTE B** — O diagnóstico em uma página
- **PARTE C** — A nova visão: high concept, pilares, loops e o Orçamento de Complexidade
- **PARTE D** — Princípios da literatura transformados em regras do Shura
- **PARTE E** — Auditoria completa, sistema por sistema (o que é, veredito, por quê, o que fazer)
- **PARTE F** — O que funcionou e o que falhou em outros jogos (e o que o Shura tira disso)
- **PARTE G** — As poucas adições que valem a pena
- **PARTE H** — Pacotes de trabalho (WP-00 a WP-20) com passos e critérios de aceite
- **PARTE I** — Telemetria, balanceamento e protocolo de playtest
- **PARTE J** — Decisões reservadas ao diretor, riscos e glossário
- **PARTE K** — Fontes

---

# PARTE A — Instruções de execução para a IA de código

## A.1 Seu papel

Você é o programador principal do Shura (Unity 6000.6.8f1, C#, projeto montado em código, sem cenas nem prefabs relevantes). Já conhece a base: `World` (parciais), `PlayerController`, `Enemy`, `PlayerBuild.RecomputeModifiers`, `RewardRegistry`, `RoomGenerator`, `MetaProgress`/`MetaStore`, `GameUI` (IMGUI) + `UiKit`, `Strings`, `NetSession`/`RemoteSession`, `DuelSim`/`DuelRollback`, `GameConfig.cs`, `SimClock`, `verify.ps1`, `package.ps1`.

Seu trabalho agora **não é acrescentar conteúdo**. É **reduzir, fundir, clarear e medir**, para transformar um jogo com ~15 camadas de combate num jogo com um núcleo pequeno, legível e profundo, e com as camadas restantes reorganizadas como escolhas de build.

## A.2 Regras inegociáveis de trabalho

1. **Nada é apagado de primeira.** Todo corte entra primeiro atrás de uma *feature flag* (WP-00). O código antigo só é removido no WP-20 (Limpeza), depois que o diretor confirmar com playtest humano. Exceção: código morto óbvio e sem uso.
2. **Um pacote de trabalho (WP) por vez**, na ordem da Parte H, salvo indicação. Cada WP termina com: testes passando, `verify.ps1` verde, bots sem exceção, capturas de tela das telas afetadas e um relatório curto (formato em A.5).
3. **Determinismo é sagrado.** A simulação fixa a 60 Hz, a aleatoriedade por semente e o `DuelSim` em inteiros não podem quebrar. Qualquer mudança em geração de sala precisa manter `Mix(seed, depth)` reproduzível.
4. **Saves antigos continuam abrindo.** Suba `MetaProgress` para versão 3 com migração em `Sanitize` (ex.: moedas extintas convertidas em Ecos com taxa documentada; itens de mochila convertidos; ver WP-11).
5. **Números ficam em dados, não espalhados no código.** Todo valor novo ou alterado vai para `GameConfig.cs` (ou para o novo `Balance` em JSON, WP-02) com comentário em português dizendo *o que* controla e *em que unidade* (quadros, %, segundos).
6. **Textos novos vão para `Strings`**, nunca literais na UI. A partir do WP-17, para as tabelas de localização.
7. **Não invente decisões reservadas ao diretor** (Parte J.1). Implemente o padrão recomendado atrás de flag e registre a pendência no relatório.
8. **Não aumente o número de teclas, medidores no HUD, moedas, tipos de anel ou modos** sem que algo equivalente saia (regra "um entra, um sai", Parte C.5).
9. **Mantenha o português do jogo** e os termos japoneses já usados. Comentários de código em português.
10. **Versões:** cada WP concluído = um *patch* (v0.16.0, v0.16.1…). Ao fim do WP-10, v0.17.0; ao fim do WP-20, v0.20.0 (candidata a beta).

## A.3 Como ler os vereditos da Parte E

| Veredito | Significado | O que você faz |
| --- | --- | --- |
| **MANTER** | Está certo e serve aos pilares | Não mexa, só corrija bugs |
| **POLIR** | Certo, mas mal comunicado ou mal sentido | Ajuste feedback, som, UI, números |
| **SIMPLIFICAR** | Ideia boa, execução com regras demais | Remova sub-regras listadas, mantenha o núcleo |
| **FUNDIR** | Duplica outro sistema | Junte no sistema indicado, uma só regra |
| **REBAIXAR** | Bom como opção, ruim como regra base | Tire da base e transforme em técnica, relíquia, aspecto, juramento ou modo |
| **ADIAR** | Bom, mas fora do escopo da 1.0 | Desligue por flag, mantenha o código, liste no backlog pós-lançamento |
| **CORTAR** | Não serve aos pilares ou custa mais do que entrega | Desligue por flag agora; remova no WP-20 se o playtest confirmar |

## A.4 Ordem de prioridade quando houver conflito

1. Clareza do combate (o jogador entende por que morreu).
2. Sensação (cada golpe é sentido).
3. Decisões interessantes na run.
4. Variedade entre runs.
5. Progressão e narrativa.
6. Online.

## A.5 Formato do relatório ao fim de cada WP

```
WP-XX — <nome> — v0.16.N
Feito: <lista curta>
Flags novas: <nome = padrão>
Arquivos principais: <lista>
Testes: <antes> → <depois> (todos passando? s/n)
Bots: <sementes, profundidade, exceções>
Capturas: <telas>
Números alterados: <tabela antigo → novo>
Pendências para o diretor: <lista>
Riscos percebidos: <lista>
```

---

# PARTE B — O diagnóstico em uma página

**Em 9 dias o Shura ganhou o conteúdo de um jogo de 2 anos, mas ainda não provou que é divertido para um humano que nunca o viu.** O relatório v0.15.0 já admite isso: "quase tudo foi validado por robôs", "o maior risco é excesso de sistemas" e "a próxima etapa é jogar, medir e cortar". Este plano concorda e vai além: **o problema não é só balanceamento; é arquitetura de experiência.**

## B.1 O que o guia diz e onde o Shura está

| Princípio do guia | Estado do Shura | Gravidade |
| --- | --- | --- |
| O micro loop de 5 s tem que ser bom sozinho (Parte 5) | O micro loop é bom no papel (anel → parry → postura → finalização), mas está soterrado por fôlego, selos, kamae, ferimentos, Shura, leituras da IA | **Crítica** |
| Ideias são baratas; o trabalho é testar, medir e cortar (Ideia central 3) | Muito foi adicionado, quase nada foi medido com humanos; não há telemetria | **Crítica** |
| Decisões interessantes: informadas, com custo, visíveis (Sid Meier, 1.3) | Muitas decisões são invisíveis (Karma por ação, leituras da IA, elementos, ferimentos) ou triviais (atributos +3% de item) | Alta |
| Flow exige metas claras e feedback imediato (1.5) | ~15 teclas e ~8 medidores competem pela atenção; o jogador lê HUD em vez de ler o golpe | **Crítica** |
| Contraste: se tudo grita, nada grita (1.6 e 7.5) | Fluxo, Shura, Mushin, Ciclo, Ougi, reações, finalização dirigida: muitos "picos" competindo | Média |
| Meta-progressão desbloqueia variedade, não poder (9.3) | Altar, idade, Senseis, relíquias e itens alteram janela de parry e números; parte da vitória vira grind | Alta |
| Run de 20 a 45 minutos (9.3) | Run infinita; o Ashura só na profundidade 49; não há "uma run completa" definida | Alta |
| Dificuldade multifatorial, não só números (8.3) | Bem resolvido pelos Kalpas e afixos; manter | Baixa |
| Aleatoriedade com proteção contra sequências (8.7) | Não documentado; drops a 18–40%, eventos e salas sem *streak breaker* | Média |
| Onboarding: uma ideia por vez, deixe pular (Parte 10) | Bom (dojo com tempo congelado, liberação em camadas), mas 21 lições é muito e ensina sistemas que não deveriam ser base | Média |
| Inspire-se no princípio do Nemesis, não na implementação patenteada (2.3) | O sistema de Rivais replica hierarquia Capitão/General, promoção e rivalidade — **muito próximo da patente da Warner Bros.** | **Crítica (jurídica)** |
| Escopo: multiplayer online é o item que mais ameaça o prazo (A.4) | Coop até 8 + navegador + nativo + Steam + PvP rollback + presença assíncrona: quatro pilhas de rede para manter | Alta |
| Arte coerente, legível, com identidade; shader unificador (Parte 12) | Pacote pixel art + arte procedural + chefes ampliados e tingidos; duelistas parecidos | Alta |
| Itens obrigatórios de jogo lançado (14.3): localização, acessibilidade, telemetria | Texto preso no código; sem legendas de som; sem telemetria | Alta |

## B.2 A tese do redesenho

> **O Shura deve ser simples de aprender e profundo de dominar. Hoje ele é difícil de aprender e largo de dominar.**

Profundidade = quantas situações diferentes nascem de poucas regras (Tynan Sylvester chama isso de *elegância*). Largura = quantas regras existem. O Shura acumulou largura. O redesenho troca largura por profundidade:

1. **Um núcleo de quatro verbos** (ler, responder, quebrar, finalizar) com três respostas (aparar, esquivar, trocar), cada uma ligada a **um** tipo de anel.
2. **Tudo o que não é núcleo vira escolha de build** dentro da run (técnica, relíquia, aspecto de arma) ou **escolha de desafio** fora dela (juramento, modo). O conteúdo continua no jogo; ele só deixa de ser obrigatório para todo mundo o tempo todo. É o princípio da *New World Order* de Magic: The Gathering: complexidade baixa no que é comum, complexidade alta no que é raro e escolhido.
3. **A Troca Perfeita vira o gancho**. É a única mecânica realmente autoral do jogo. Ela deve ser a terceira resposta do núcleo, não um extra.
4. **Medir antes de cortar de vez.** Telemetria, painel de afinação e playtest humano vêm *antes* do balanceamento.

---

# PARTE C — A nova visão

## C.1 High concept (uma frase)

> **"Um roguelite de duelos de samurai visto de cima, em que você vence lendo o golpe — aparando, esquivando ou trocando de arma no instante exato — e absorve a técnica de cada adversário que domina."**

Teste da troca (guia 3.4): substitua "Shura" por "Hades" ou "Sekiro". Não serve para nenhum dos dois. Passa.

**Lei dos opostos** (o que o Shura *não* é): não é hack-and-slash de apertar botão; não é looter de atributos; não é jogo de gerenciar HUD.

**Frase de prateleira** ("é X, mas Y"): *"Sekiro em runs de 40 minutos, visto de cima, onde trocar de arma é o seu parry."*

## C.2 Os três pilares (mantidos, com um filtro novo)

1. **Ler, não reagir às cegas.** Todo golpe é anunciado; a morte é culpa do jogador. *Filtro:* uma mecânica que tira atenção do inimigo e põe no HUD falha neste pilar.
2. **Domínio vira poder.** A build nasce do que você dominou. *Filtro:* poder que vem de grind, idade ou números permanentes falha neste pilar.
3. **Cada golpe é sentido.** Hit-stop, faísca, som, tremor, Fluxo. *Filtro:* um efeito que não reforça um momento de domínio é ruído.

## C.3 Estéticas-alvo (MDA, guia 1.1) e público (Quantic Foundry, guia 1.7)

| Prioridade | Estética MDA | Como o Shura entrega | Sistema responsável |
| --- | --- | --- | --- |
| 1ª | **Desafio** | Parry de quadro, chefes, respostas de mestre | Núcleo do combate, chefes |
| 2ª | **Sensação** | Hit-stop, faíscas, som, Fluxo | Feel, áudio |
| 3ª | **Expressão** | Builds que mudam *como* você defende e pune | Técnicas, aspectos, relíquias |
| Apoio | Fantasia, Narrativa | Samurai no Shuradō, morte que conta história | Templo, Casa de Chá, jisei |
| Apoio | Companheirismo | Coop até 4 | Online |

**Público:** jogadores com motivação forte em **Maestria/Desafio** e **Ação/Excitação** (perfil soulslike e roguelite de ação), com **Imersão/Fantasia** em segundo plano. Isso significa: eles toleram dificuldade alta, *não* toleram injustiça, falta de clareza ou dificuldade que vem de números; valorizam builds que mudam o estilo de jogo.

## C.4 Os loops como frases de verbos (guia 5.4)

- **Micro (segundos):** *ler o anel → responder (aparar / esquivar / trocar) → quebrar a postura → finalizar e absorver.*
- **Meso (minutos):** *limpar a sala → escolher 1 de 3 → escolher a porta no mapa → enfrentar o Guardião → derrotar o Senhor do Bioma.*
- **Macro (horas):** *morrer ou vencer → voltar ao Templo → desbloquear aspectos, técnicas e histórias → jurar mais dificuldade → descer de novo.*

Se uma mecânica não aparece nem serve diretamente a um desses verbos, ela é candidata a REBAIXAR ou CORTAR.

## C.5 O Orçamento de Complexidade (regras numéricas, valem para todo WP)

| # | Regra | Limite | Hoje | Alvo |
| --- | --- | --- | --- | --- |
| R1 | Verbos de resposta do núcleo | 3 (aparar, esquivar, trocar) + atacar | ~9 (aparar, bloquear, esquivar, trocar, kiai, detonar, kamae, ferramenta, alma) | 4 |
| R2 | Teclas de combate (sem contar mover/mirar/pausa) | **≤ 7** | ~15 | 7 |
| R3 | Medidores do jogador no HUD durante a luta | **≤ 3** + goles de cura + cargas da Arte | ~8 (vida, postura, fôlego, Shura, esquivas, ferimentos, Fluxo, leituras) | 3 |
| R4 | Tipos de anel | **≤ 4**, cada um com cor + forma + som | 7 | 4 |
| R5 | Janela de parry e tempo do anel | **constantes** em todo o jogo; só a opção de acessibilidade muda | variam por kamae, idade, altar, relíquias, itens | constantes |
| R6 | Moedas permanentes | **≤ 2** + 1 cosmética | 4 + Mon | Ecos + Aço (ou só Ecos) + Pétalas |
| R7 | Telas de gestão durante a run | **zero** além de escolher 1 de 3 e o mapa | inventário, mochila, equipamento, forja | zero |
| R8 | Modos no menu principal na 1.0 | **≤ 4** | 7 | Jornada, Desafio Diário, Dojo, Duelo (amigos) |
| R9 | Jogadores no coop com suporte oficial | **≤ 4** (5–8 experimental) | 8 | 4 |
| R10 | Sistema novo | só entra se outro sair ou se for opção de build | — | "um entra, um sai" |

**Três testes que toda mecânica precisa passar** (aplique na Parte E e em qualquer ideia futura):

1. **Teste da regra (guia 3.3):** o jogador toma decisões diferentes por causa disso? Se não, é cosmético.
2. **Teste da visibilidade:** o jogador vê a causa e o efeito em até 1 segundo, sem abrir menu? Se não, é invisível e não ensina (Koster: diversão é aprender padrões; padrão invisível não se aprende).
3. **Teste do minuto:** dá para ensinar numa lição de até 60 segundos, jogando? Se não, é complexo demais para a base.

---

# PARTE D — Princípios da literatura transformados em regras do Shura

Cada linha abaixo é uma regra de design **executável**. Quando a Parte E cita "(D-n)", é esta tabela.

| ID | Princípio | Fonte | Regra concreta no Shura |
| --- | --- | --- | --- |
| D-1 | O jogador sente de Estética para Mecânica; o designer constrói de M para E | MDA (Hunicke, LeBlanc, Zubek, 2004); guia 1.1 | Toda mecânica do combate precisa ser descrita pelo que o jogador *sente* ("me sinto lendo o oponente"). Se a descrição só existe em números, ela não chega ao jogador. |
| D-2 | Diversão é aprender padrões; padrão dominado vira tédio | Koster, *A Theory of Fun* (2004); guia 1.2 | O jogo deve sempre ter um padrão novo a aprender: um inimigo novo por profundidade no início, combinações (formações, afixos, Kalpas) depois. Nunca introduzir duas regras novas na mesma sala. |
| D-3 | Memória de trabalho comporta cerca de 4 blocos de informação ao mesmo tempo | Cowan (2001); Miller (1956); Hodent, *The Gamer's Brain* (2017) | Durante a luta, o jogador acompanha no máximo: o anel do atacante, a própria vida, a própria postura e o Fluxo. Tudo o mais sai do campo central ou vira consequência visível no próprio inimigo. Base do Orçamento R3. |
| D-4 | Atenção é seletiva; o que não está no foco não é visto (cegueira por desatenção) | Hodent (2017); Simons e Chabris (1999) | Informação crítica fica **no inimigo ou no samurai**, não nos cantos da tela. Medidores de canto só para o que pode ser lido entre salas. |
| D-5 | Uma decisão é interessante quando não há resposta óbvia, há custo, há informação e a consequência é visível | Sid Meier (GDC 2012); guia 1.3 | Toda carta de recompensa mostra ganho e custo em uma linha cada. Nenhuma recompensa "+3% de algo" sem efeito perceptível. |
| D-6 | Tempo de decisão cresce com o número de opções | Lei de Hick-Hyman | Escolhas de 3 opções (já é assim). No máximo 4 com Visão do Destino. Nunca listas longas no meio da run. |
| D-7 | Flow: metas claras, feedback imediato, desafio um pouco acima da habilidade, num canal que sobe | Csikszentmihalyi; modelo de 8 canais; guia 1.5 | Curva em degraus (ensina → testa → respiro → novo). Santuário/Mercador depois de Guardião, nunca dois combates difíceis seguidos sem opção de respiro no mapa. |
| D-8 | Autonomia, competência, pertencimento retêm jogadores | Deci e Ryan; Rigby e Ryan, *Glued to Games* (2011); guia 1.7 | Autonomia = rota e build. Competência = feedback claro e morte justa. Pertencimento = coop até 4 e os hóspedes da Casa de Chá. Nada pode minar competência (ex.: mudar a janela de parry sem o jogador perceber). |
| D-9 | Contraste: se tudo grita, nada grita | MagmaDev; Vlambeer, *The Art of Screenshake*; guia 1.6 e 7.5 | Uma hierarquia única de impacto (WP-14): golpe < parry < Troca Perfeita < quebra de postura < finalização < chefe. Só os dois últimos têm câmera lenta. |
| D-10 | Game feel: controle em tempo real + espaço simulado + polimento | Swink, *Game Feel* (2008); Jonasson e Purho, *Juice it or lose it* (2012) | Todo valor de sensação editável em tempo real (painel do WP-02). Cada canal (som, imagem, vibração) deve funcionar sozinho. |
| D-11 | Designers mentem a favor do jogador | Celeste (Maddy Thorson); guia 7.4 | Manter buffer de 8 f e anel que fecha 4 f antes. Acrescentar: hurtbox do jogador menor que o sprite; perdão de 2 f no fim da janela de parry contra golpes que já mostraram anel por mais de 30 f. |
| D-12 | Os jogadores otimizam a diversão para fora do jogo se você deixar | Soren Johnson (2010, "Water Finds a Crack") | Procurar estratégias dominantes e seguras (ex.: bloquear sempre, esquivar sempre, ficar de costas para a parede). O jogo deve punir a passividade como o Sekiro faz (postura que sobe no bloqueio) e recompensar o risco. |
| D-13 | Cada mecânica é um "átomo de habilidade" que precisa de ciclo próprio de ação, simulação, feedback e domínio | Dan Cook, *The Chemistry of Game Design* (2007) | Para cada mecânica do núcleo existe: ação clara, feedback no mesmo quadro, um modo de ensinar (lição de dojo) e um uso que se renova (inimigos que a exigem). Mecânica sem inimigo que a exija = candidata a corte. |
| D-14 | Elegância: muita profundidade com poucas regras | Tynan Sylvester, *Designing Games* (2013) | Preferir regras que interagem com o que já existe (ex.: Troca Perfeita interage com armas, anéis e técnicas) a regras isoladas (ex.: ferimentos com bandagem). |
| D-15 | Complexidade no que é raro, simplicidade no que é comum | Mark Rosewater, "New World Order" (Magic: The Gathering, 2011) | Regra base simples; efeitos complexos só em técnicas de nível III, fusões, aspectos e relíquias lendárias. |
| D-16 | Restrição gera escolha | Hollow Knight (entalhes de amuleto); Slay the Spire (3 de energia) | Limitar o que se carrega: 2 armas, 1 Arte, 1 tsuba, espaços de relíquia limitados. Sem mochila. |
| D-17 | Meta-progressão deve dar variedade, não poder; dificuldade opcional só depois da primeira vitória | Hades (Espelho da Noite com alternativas, Pacto de Punição após a 1ª vitória); Slay the Spire (Ascensão); guia 9.3 | Altar com teto baixo e nós de escolha excludente; Juramentos liberados só após derrotar o Ashura uma vez. |
| D-18 | A morte precisa avançar algo | Hades (Greg Kasavin, GDC 2021); guia 9.3 | Toda morte entrega: uma fala nova, um jisei, progresso de pergaminho ou de maestria. Nunca uma tela de morte "vazia". |
| D-19 | Telegrafe tudo; informação perfeita gera decisões táticas | Into the Breach (GDC 2019); guia 8.4 | Todo golpe tem anel + pose + som. Golpes de grupo e de chefe mostram área no chão. Nenhum dano vindo de fora da tela sem aviso. |
| D-20 | Breakpoints: o número que importa é "golpes para matar" | Lutki; guia 8.5 | Balancear por tempo-alvo (Parte I.3), não por dano bruto. |
| D-21 | Aleatoriedade crua é sentida como injusta | Tetris (saco de 7), Dota 2 (PRD); guia 8.7 | Saco embaralhado para tipos de sala; *pity timer* para técnica nova; *streak breaker* para drops e eventos. |
| D-22 | Cinco usuários revelam a maioria dos problemas de usabilidade | Jakob Nielsen (2000) | Rodadas de 5 playtesters, repetidas a cada WP grande (Parte I.5). |
| D-23 | Laço positivo dentro da run, reinício na morte | MDA; guia 8.6 | Ascensão por sala e Fluxo são laços positivos: ok dentro da run. Fora da run, nada de laço positivo de poder. |
| D-24 | Corte conteúdo antes de cortar qualidade | Guia 14.2 | Um bioma excelente vence três medíocres: preferir 6 biomas bem feitos a 10 com arte reaproveitada. |

---

# PARTE E — Auditoria completa, sistema por sistema

Formato de cada sistema: **Hoje** (o que o relatório v0.15.0 descreve) · **Veredito** · **Por quê** (com referência à Parte D ou ao guia) · **O que fazer** (especificação para o WP indicado).

## E.1 Núcleo do combate

### E.1.1 Anel de aviso — **MANTER + POLIR** (WP-04, WP-17)

- **Hoje:** encolhe 36 f, fecha 4 f antes do impacto com um "tim"; ciano = aparar, vermelho com losango = esquivar; padrões além da cor.
- **Por quê:** é a melhor decisão de design do jogo. Resolve o maior risco da câmera de cima (guia A.3: golpes são difíceis de ler de cima) com telegrafia redundante (D-19). É o "anel de intenção" do Shura, como as intenções de Slay the Spire.
- **O que fazer:** manter o tempo fixo de 36 f para todos os golpes comuns (R5). Golpes de chefe podem ter anéis mais longos, nunca mais curtos que 24 f. Cada tipo de anel ganha **som próprio** (WP-17) e o anel **não some no Mushin, no escuro do Abismo nem na neblina** (só no Juramento "Anel Silencioso", que é escolha do jogador). Golpes vindos de fora da tela: seta na borda da tela com a cor do anel (WP-06).

### E.1.2 Parry perfeito — **MANTER**

- **Hoje:** arco de 120° para a mira; janela de 9 f; apertar de novo em menos de 20 f corta a janela pela metade (anti-spam).
- **Por quê:** é o átomo de habilidade central (D-13). 9 f (150 ms) é generoso o bastante para top-down e curto o bastante para ser maestria. O anti-spam é o mesmo princípio do Sekiro (D-12: impedir a estratégia de martelar o botão).
- **O que fazer:** (1) a janela **nunca mais** é alterada por kamae, idade, Altar, relíquia, tsuba ou item (R5). Mapear e remover todos os modificadores de janela em `PlayerModifiers` (WP-03). (2) Dar feedback visível do anti-spam: quando a janela está cortada, a lâmina do samurai brilha em cinza por 20 f e toca um "tic" seco. (3) Perdão de fim de janela (D-11): +2 f no fim da janela quando o anel ficou visível por mais de 30 f. (4) Arco de 120° está bom; no controle, com trava de mira, o arco segue o alvo travado.

### E.1.3 Bloqueio — **MANTER**

- **Hoje:** segurar aparar depois da janela; o golpe vira desgaste da sua postura; postura cheia = Guarda Quebrada (60 f atordoado).
- **Por quê:** é a rede de segurança que não pode ser estratégia dominante (D-12). Igual ao Sekiro: bloquear é seguro a curto prazo e perigoso a longo prazo.
- **O que fazer:** nada no núcleo. Verificar por telemetria se o bloqueio domina (meta: < 35% das respostas a anéis ciano sejam bloqueio em jogadores com mais de 5 runs).

### E.1.4 Esquiva — **MANTER**

- **Hoje:** duas cargas, invencível do 2º ao 9º quadro de 11, recarga 0,9 s; esquivar dentro do golpe = esquiva perfeita.
- **O que fazer:** as cargas aparecem **no samurai** (dois pontinhos sob os pés), não no cartão do HUD (D-4). Remover o ícone de pena do HUD.

### E.1.5 Contra-investida e respostas de mestre — **FUNDIR em três "Mikiri"** (WP-03)

- **Hoje:** contra-investida (esquivar em direção a estocada perigosa nos quadros ativos), Pisão (esquiva tardia numa estocada: desarma, +40 postura), Descida (esquivar para cima de uma varredura: postura ×2), Arremesso (esquivar para trás de um agarrão). São duas respostas diferentes para a estocada.
- **Por quê:** duas regras para o mesmo golpe confundem (D-14). As respostas de mestre são a camada de profundidade do anel vermelho: transformam "fuja" em "puna", exatamente o Mikiri do Sekiro.
- **O que fazer:** uma resposta de mestre por tipo de golpe vermelho, todas com o mesmo verbo (**esquivar no fechamento do anel, na direção certa**):

| Golpe vermelho (pose distinta) | Resposta de mestre | Direção | Efeito |
| --- | --- | --- | --- |
| Estocada | **Mikiri** (funde contra-investida + Pisão) | para dentro do golpe | atravessa, desarma por 60 f, +40 de postura |
| Varredura | **Descida** | por cima / através | salto curto, próximo golpe com postura ×2 |
| Agarrão | **Arremesso** | para as costas do inimigo | projeta o inimigo (em fosso, armadilha ou outro inimigo) |

Cada resposta de mestre conta para o **selo dourado** (absorção de técnica, E.5.1). Lição de dojo única: "Mikiri — três golpes, uma esquiva".

### E.1.6 Postura do inimigo — **MANTER**

- **Hoje:** enche com parries e golpes; cheia = Quebrado por 150 f, marcado para finalização; regenera mais devagar com pouca vida.
- **O que fazer:** a barra de postura do inimigo fica **sob o inimigo**, com o mesmo desenho de tsuba do HUD do jogador (leitura imediata de que é o mesmo recurso). Ver alvos de "parries até quebrar" na Parte I.3.

### E.1.7 Finalização com corte dirigido — **SIMPLIFICAR** (WP-06)

- **Hoje:** 40 f de mira com linha dourada; perfeito ≤ 4° (+25 vida, +Fluxo, +15 Mon, saque em dobro), na linha ≤ 12°, limpo ≤ 24°; invulnerável enquanto mira.
- **Por quê:** é ótima expressão de habilidade e fantasia, mas 40 f de mira em **todo** inimigo comum quebra o ritmo de salas com várias ondas (D-7) e banaliza o momento (D-9). A recompensa do perfeito é grande demais para ser obrigatória em todo inimigo; isso transforma a finalização numa tarefa.
- **O que fazer:**
  - **Toque** em atacar sobre inimigo quebrado = finalização rápida (resultado "limpo" automático, animação curta de 18 f).
  - **Segurar** atacar = abre a mira dirigida de 40 f com os três níveis.
  - Elites, Guardiões, último inimigo da sala e chefes: a mira dirigida abre por padrão (o momento merece).
  - Recompensa do perfeito: +15 vida (era 25), +1 ponto de Fluxo, saque em dobro; remover o "+15 Mon" (moeda no momento de clímax é ruído).
  - A decisão "rápido e seguro" × "lento e recompensador" é uma decisão interessante (D-5) no lugar de uma obrigação.

### E.1.8 Cancelamentos e buffer — **MANTER**

- **Hoje:** recuperação cancelável em esquiva ou parry 4 f depois do golpe; buffer de 8 f (ataque e esquiva) e 3 f (parry).
- **Por quê:** está dentro das faixas recomendadas (guia 7.4: 6 a 12 f). O buffer de parry curto (3 f) é correto: buffer longo de parry vira spam involuntário.

### E.1.9 Coordenador de ataques — **MANTER + ajustar para coop** (WP-19)

- **Hoje:** só um inimigo ataca por vez, intervalo mínimo de 20 f.
- **Por quê:** é o que torna grupos legíveis (mesmo princípio dos combates em grupo do Sekiro e de *Batman: Arkham*). Sem ele, o pilar 1 morre.
- **O que fazer:** no coop, um "token de ataque" por jogador (máx. 3 atacantes simultâneos com 4 jogadores), cada token mirando jogadores diferentes. Nunca dois anéis fechando sobre o **mesmo** jogador com menos de 20 f de diferença.

## E.2 Camadas adicionadas na Fase 1 (Expansão §3)

### E.2.1 Troca Perfeita — **MANTER e PROMOVER a gancho do jogo** (WP-04, WP-08)

- **Hoje:** trocar de arma dentro da janela apara (+30% postura, +50% Fluxo) e dispara o golpe de troca da arma que entra, com invencibilidade; não há bloqueio (errar = levar o golpe). Trocar no meio do combo forma um Elo (45 pares nomeados); 3 Elos = Ciclo das Duas Lâminas (4 s, +20% dano).
- **Por quê:** é a única mecânica autoral do Shura, passa no teste da regra (muda decisões) e é visível num GIF de 3 s (guia 3.4). Windblown (2024) validou no mercado a ideia vizinha de trocar de arma no fim do combo para soltar um golpe especial. **Ela deve ser a terceira resposta do núcleo, com um anel próprio** (o roxo, E.2.4).
- **O que fazer:**
  - Manter a regra de risco: Troca Perfeita não tem bloqueio. Recompensa alta, risco alto (D-5).
  - Os 45 Elos nomeados viram **10 comportamentos de Elo, um por arma que entra**, com o nome do par mantido só como texto de sabor. Motivo: 45 comportamentos são impossíveis de balancear e de aprender (D-2); 10 são aprendíveis.
  - Ciclo das Duas Lâminas: manter.
  - A IA **não pode punir a Troca Perfeita** como hábito (cortar a leitura TROCA, E.8). Punir o gancho é punir o jogador por jogar o jogo que você vende.

### E.2.2 Fôlego (Kokū) — **CORTAR** (WP-03)

- **Hoje:** 100 pontos; atacar 8, esquivar 15; zerado = exausto 1,5 s (−40% velocidade, janela de parry pela metade); Brilho do Respiro devolve 70%; inimigos sem fôlego após 3 parries.
- **Por quê:**
  1. **Duplica a postura.** O jogo já tem um recurso que pune agir demais e defender demais: a postura. O Sekiro tirou a barra de stamina do jogador de propósito para empurrar a agressividade e deixar a postura como único recurso de duelo; foi uma das decisões mais elogiadas do jogo.
  2. **Viola R5:** exaustão corta a janela de parry pela metade. O jogador erra um parry "certo" e não entende por quê (D-8, competência).
  3. **Viola R3:** é o quarto medidor.
  4. O Brilho do Respiro é uma terceira janela de timing (além de parry e Troca) que compete pela atenção.
- **O que fazer:** flag `Feature.Stamina = false`. Ataque e esquiva sem custo. A agressão excessiva já é limitada pelo coordenador de ataques e pela postura. **Efeitos que dependiam do fôlego são convertidos:**

| Antes (fôlego) | Depois |
| --- | --- |
| Inimigo "sem fôlego" após 3 parries seguidos | Inimigo **cambaleia** (20 f sem atacar) após 3 parries perfeitos seguidos: mesma recompensa, ligada à postura |
| Bioma Gelo: fôlego volta 40% mais devagar | Gelo: **sua postura** se recupera 40% mais devagar |
| Ittan-momen sufoca o fôlego | Ittan-momen: agarrão que enche a sua postura enquanto preso (escape com Arremesso) |
| Zona corrompida drena fôlego | Zona corrompida: a postura dos humanos não regenera; yokai ganham +25% de dano |

### E.2.3 Selos de Ki — **REBAIXAR a técnica** (WP-03, WP-09)

- **Hoje:** cada parry perfeito marca um selo (máx. 5, 8 s); detonar (G ou ataque + aparar) causa 12 de dano × arma e 8 de postura por selo.
- **Por quê:** é uma boa ideia de build (Nine Sols faz isso no núcleo, mas lá o jogo é construído em volta). No Shura, ela acrescenta uma tecla, um recurso no inimigo e uma decisão de tempo a mais em *toda* luta, e compete com a postura como "barra que enche com parries" (D-14).
- **O que fazer:** remover a tecla G e o recurso base. Criar a técnica **"Selo Interno"** (do Onmyōji ou do Sacerdote Corrompido): parries perfeitos marcam selos; ao 5º selo, eles detonam sozinhos. No nível III, ramo Forma Plena (detonam no 4º) ou Eco (a detonação marca o inimigo vizinho). Transformar a detonação manual numa **Arte** opcional ("Ruptura", E.2.10) para quem quer o controle.

### E.2.4 Novos tipos de anel — **SIMPLIFICAR para 4** (WP-04)

- **Hoje:** tracejado (finta), duplo, roxo pontilhado (espiritual: só Troca Perfeita ou esquiva, ou parry com Talismã), branco partido (golpe de grupo, coop), verde (maldito: bloquear amaldiçoa 4 s). Mais ciano e vermelho = 7 tipos.
- **Por quê:** a linguagem de ameaça tem que ser consistente e pequena (guia 10.3). Sete tipos de anel, muitos com exceções ("só Troca *ou* esquiva *ou* parry com Talismã"), não cabem na leitura em 150 ms. A melhor versão é uma **tríade**: cada resposta do núcleo tem um anel.
- **O que fazer:**

| Anel | Forma + cor + som | Resposta correta | Outras respostas |
| --- | --- | --- | --- |
| **Ciano liso** | círculo contínuo, "tim" metálico | Aparar | Troca Perfeita também apara; esquiva evita sem ganho |
| **Vermelho com losango** | círculo com losango, batida grave de taiko | Esquivar (resposta de mestre conforme a pose) | Aparar = levar o golpe |
| **Roxo pontilhado** | círculo pontilhado, sino de templo | **Troca Perfeita** | Esquiva evita sem ganho; aparar = atravessa a lâmina |
| **Tracejado (finta)** | círculo tracejado que se desfaz, batida abafada | Esperar o anel real que vem logo depois | Aparar na finta abre você por 30 f |

  - **Duplo:** deixa de ser tipo. É só dois anéis em sequência.
  - **Verde maldito:** cortar. A regra "bloquear amaldiçoa" é invisível e pune a rede de segurança.
  - **Branco partido:** fica como variante **só no coop** (ciano com rachadura, exige dois aparando juntos). Não conta no limite R4 porque não existe no solo.
  - O Talismã que permite aparar o roxo: cortar (exceção da exceção).
  - Distribuição recomendada por profundidade: Ato 1 só ciano e vermelho; roxo entra com o primeiro yokai espiritual; finta a partir do Ato 2.

### E.2.5 Kamae (4 guardas) — **FUNDIR nas armas** (WP-03, WP-08)

- **Hoje:** Jōdan (+25% dano, janela −2 f), Chūdan (equilíbrio), Gedan (janela +2 f, esquiva curta), Waki (cortes em chicote); poses e cortes próprios; troca por 1–4 ou direcional.
- **Por quê:** (1) viola R5 (Jōdan e Gedan mudam a janela de parry). (2) Acrescenta 4 teclas. (3) Cria uma terceira camada de "modo" além das duas armas: o jogador já troca de arma o tempo todo por causa da Troca Perfeita, e cada arma já é um estilo. Ghost of Tsushima usou posturas contra tipos de inimigo, mas sem duas armas e sem anel por tipo de golpe; no Shura, a função de "resposta certa por inimigo" já é do anel.
- **O que fazer:** remover as teclas 1–4 e a troca de guarda em combate. **Cada arma tem uma guarda fixa** que define pose e cortes (a arte já existe e é reaproveitada): Katana = Chūdan, Montante = Jōdan, Naginata = Chūdan longa, Lâminas Gêmeas = Waki, Manoplas = Gedan, Kusarigama = Waki, Tessen = Gedan, Shakujō = Jōdan. Diferenças entre armas vêm de dano, alcance, velocidade e postura, nunca da janela de parry. Os nomes das guardas continuam no texto (identidade e fantasia).

### E.2.6 Ferimentos localizados e bandagem — **CORTAR no jogador; SIMPLIFICAR no inimigo** (WP-03)

- **Hoje:** braço, perna, tronco e olhos, no jogador e no inimigo, com efeitos próprios; bandagem (H) cura um.
- **Por quê:** é uma camada de gestão que o jogador não acompanha no meio da luta (D-3), exige um boneco no HUD e uma tecla. Falha no teste da visibilidade. O próprio relatório lista como candidato a corte.
- **O que fazer:** remover ferimentos do jogador, a bandagem, a tecla H e o boneco de papel do HUD. No inimigo, manter **um** efeito visível: o **desarme** do Mikiri (o inimigo larga a arma 60 f). As cicatrizes dos Algozes (E.9) continuam como dado narrativo e visual, sem regra mecânica escondida.

### E.2.7 Posicionamento — **MANTER + POLIR**

- **Hoje:** costas +50% de postura; parede +15; empurrar um inimigo em outro derruba os dois; fossos matam; flecha desviada que acerta outro inimigo os faz brigar.
- **Por quê:** é elegância pura (D-14): regras simples que geram situações novas com o espaço da arena. Nenhuma tecla nova.
- **O que fazer:** feedback visível de cada caso, com kanji curto e som: 背 (costas), 壁 (parede), 倒 (derrubada). Garantir que o Arremesso (Mikiri do agarrão) priorize fossos e armadilhas próximos (o jogador sente que planejou).

### E.2.8 Impasse de saque (Iai) — **MANTER + SIMPLIFICAR a punição** (WP-06)

- **Hoje:** ~20% das salas de combate começam com barras pretas e fintas; soltar o ataque no avanço real mata o líder; cedo ou tarde demais custa 30 de vida.
- **Por quê:** é um momento de contraste perfeito (tensão parada antes do caos, D-9) e pura fantasia de samurai. Mas −30 de vida no início da sala é uma punição numérica que não ensina (o jogador perde vida sem jogar).
- **O que fazer:** errar o saque = o líder ganha o primeiro ataque (com anel normal) e a sala começa sem o bônus. Acertar continua matando o líder. Frequência: no máximo 1 a cada 4 salas de combate (saco embaralhado, D-21), nunca na sala logo depois de outro impasse.

### E.2.9 Medidor de Shura (修羅) — **REBAIXAR a caminho opcional** (WP-07)

- **Hoje:** matar sem misericórdia sobe; parry perfeito acalma. Ira, Sede, Frenesi e Queda: 10 s como Asura e depois o Asura Interior.
- **Por quê:** é a ideia mais temática do jogo, mas como regra base é um **segundo medidor de momento** competindo com o Fluxo (D-3, D-9), e sobe e desce por regras que o jogador não vê. Como **escolha** é excelente: um caminho de risco que o jogador decide trilhar (Curse of the Dead Gods e o "Juramento de Sangue" de muitos roguelites mostram que corrupção escolhida é divertida; corrupção imposta é punição).
- **O que fazer:** `Feature.ShuraMeterBase = false`. O medidor só existe quando o jogador **ativa o Caminho do Asura**: pegando a relíquia Máscara Hannya, a Lâmina de Muramasa (evento) ou o Juramento "Caminho do Asura". Nesses casos, o medidor aparece no HUD (a única exceção a R3, porque foi escolhida) e o Asura Interior continua como clímax. A Lua Cheia (clima) só sorteia quando o Caminho está ativo.

### E.2.10 Ferramentas, papéis espirituais, Kiai, alma — **FUNDIR numa única "Arte"** (WP-05)

- **Hoje:** 6 ferramentas (shuriken, fumaça, sino, petardo, óleo, corrente) em R/T gastando papéis espirituais; Kiai (V, gasta 1 Fluxo, interrompe vermelhos); Transformação em alma (B); detonar selos (G).
- **Por quê:** quatro sistemas de "habilidade ativa" com quatro teclas e dois recursos (papéis, Fluxo). Hades resolveu o mesmo problema com **um** botão de habilidade especial por arma e **um** de lançamento; o jogador escolhe *qual* é, não acumula todas.
- **O que fazer:** criar o sistema **Arte** (技):
  - **Um encaixe, uma tecla** (R no teclado, RT no controle). O jogador escolhe 1 Arte no Arsenal antes da run entre as desbloqueadas; pode trocar por outra encontrada em carta.
  - **2 cargas**; finalizações recarregam 1 carga (substitui os papéis espirituais e o custo em Fluxo).
  - **Lista de Artes da 1.0:**

| Arte | Origem | Efeito |
| --- | --- | --- |
| Kiai | inicial | Interrompe o golpe vermelho em preparo do inimigo mais próximo (o "parry do imparável") |
| Shuriken | inicial | Leque de 3; interrompe saltos e investidas |
| Fumaça | Dojo | Zera as leituras da IA à sua volta e reposiciona você 3 m |
| Sino | Biblioteca | Etéreos ficam sólidos por 5 s numa área |
| Corrente | Dojo | Puxa um inimigo até você (ou você até um ponto) |
| Ruptura | técnica Selo Interno | Detona os selos manualmente |
| Alma | técnica no nível III | Vira o inimigo dono da técnica por 8 s (a antiga Transformação) |

  - **Cortar** petardo e óleo (duplicam elementos e o Kiai).
  - Kiai em grupo (coop): 2 jogadores (era 3) usando Kiai em até 1 s quebram a postura de todos os inimigos na tela.

### E.2.11 Mushin (無心) — **MANTER + POLIR** (WP-06)

- **Hoje:** sequência ≥ 10 com Fluxo 3 apaga o HUD, abafa a música e vale mais; um golpe sofrido encerra.
- **Por quê:** é o melhor exemplo de contraste do jogo: a recompensa pela maestria é **menos** interface (D-9). Barato e memorável.
- **O que fazer:** garantir que os anéis, as posturas sob os inimigos e as cargas de esquiva **continuam visíveis** (o HUD some, a leitura não). O bônus de dano fica; nenhuma mudança de janela.

### E.2.12 Cabaça — **MANTER**

- **Hoje:** F, 2 goles por sala.
- **Por quê:** cura ativa é uma decisão de risco clássica (guia 8.1: curar agora ou continuar atacando). Fica com tecla própria porque é a decisão defensiva mais importante depois da resposta ao anel.
- **O que fazer:** goles mostrados no cinto como cabacinhas. Valor de cura e recarga entram na planilha (Parte I.3); hipótese inicial: 2 goles por sala, 30% da vida máxima cada, animação de 30 f interrompível por esquiva (sem cura se interromper).

## E.3 Fluxo e sensação

### E.3.1 Fluxo (0–3) — **MANTER, com uma correção crítica** (WP-03, WP-06)

- **Hoje:** sobe com ações perfeitas (3, 7, 12 pontos), cai um nível a cada dano; cada nível **acelera o jogo (ritmo 0,82 no Fluxo 0 até 1,03 no 3)**, +12% de dano, mais hit-stop, tremor, faíscas, câmera mais perto e uma camada de trilha; no 3, bordas douradas.
- **Por quê:** o Fluxo é o pilar 3 em forma de sistema: desempenho vira música e imagem. **Mas mudar a velocidade global da simulação muda o tempo real do anel e da janela de parry** conforme o nível de Fluxo. Um jogador no Fluxo 0 vê um anel de 36 f durar ~730 ms; no Fluxo 3, ~580 ms. Isso viola R5 e quebra a memória muscular exatamente quando o jogador mais precisa dela (quando acabou de tomar dano e caiu de nível).
- **O que fazer:** **velocidade da simulação fixa em 1,0** em todos os níveis. A sensação de aceleração passa para: animações de ataque do *jogador* 6% mais rápidas por nível (só recuperação, nunca janelas), música com mais camadas, câmera mais perto, rastros. Manter +12% de dano por nível. Comparar as duas versões no playtest (flag `Feature.FlowTimeScale`, padrão `false`).

### E.3.2 Câmera lenta, sangue, hit-stop — **POLIR com hierarquia única** (WP-06)

Ver a tabela de impacto da Parte H, WP-06. Câmera lenta só em: finalização do último inimigo da sala, quebra de postura de chefe e Ponto de Ruptura. Quebra de postura comum perde a câmera lenta de 0,25 s (hoje acontece toda hora e perde valor, D-9).

## E.4 Armas

### E.4.1 As 10 lâminas — **SIMPLIFICAR para 8 armas + aspectos** (WP-08)

- **Hoje:** Katana, Montante, Lâminas Gêmeas, Naginata, Kusarigama, Manoplas do Leão, Tessen, Kanabō, Shakujō, Wakizashi + Kunai.
- **Por quê:** duas sobreposições claras. **Kanabō** é uma Montante mais lenta, sem bloqueio e que atordoa: é uma *variação de regra* de arma pesada, não uma arma com outra fantasia. **Wakizashi + Kunai** é uma arma curta rápida de costas: é uma variação das Lâminas Gêmeas. Variações de regra são exatamente o que os **Aspectos** de Hades resolvem: a mesma arma, uma regra diferente, desbloqueada por progresso (D-17).
- **O que fazer:** 8 armas: Katana, Montante, Lâminas Gêmeas, Naginata, Kusarigama, Manoplas, Tessen, Shakujō. Kanabō vira o **Aspecto do Oni** da Montante; Wakizashi + Kunai vira o **Aspecto da Sombra** das Lâminas Gêmeas. Arte e código reaproveitados.

### E.4.2 Forja modular (100 combinações) — **SUBSTITUIR por Aspectos (3 por arma)** (WP-08)

- **Hoje:** cada arma troca a empunhadura (golpe de troca e finalização de qualquer uma das 10) e a tsuba; 100 combinações, algumas com nome.
- **Por quê:** 100 combinações não são 100 decisões; são uma tabela que ninguém lê e que ninguém balanceia (D-5, guia 8.5). Variedade combinatória sem curadoria gera muitas combinações sem graça e algumas quebradas. Hades tem 6 armas × 4 aspectos = 24 variantes curadas, cada uma com uma regra clara.
- **O que fazer:** 8 armas × 3 aspectos = **24 variantes curadas**. Aspecto I = a arma padrão. Aspectos II e III desbloqueados pela Maestria (nível II e IV). Cada aspecto muda **uma** regra, escrita numa frase. As empunhaduras atuais viram matéria-prima: o Nagamaki vira o Aspecto do Rio da Katana (golpe de troca de estocada). Proposta inicial (a IA ajusta à arte e aos golpes existentes):

| Arma | Aspecto I (padrão) | Aspecto II | Aspecto III |
| --- | --- | --- | --- |
| Katana | Iaijutsu | **Rio** (Nagamaki: golpe de troca vira estocada de 3 m) | **Lua** (antiga Waki: cortes em chicote, finalização perfeita recarrega a Arte) |
| Montante | Queda do Céu | **Oni** (antigo Kanabō: sem bloqueio, todo golpe atordoa 10 f) | **Avalanche** (golpes carregados empurram em cone) |
| Lâminas Gêmeas | Dança das Lâminas | **Sombra** (antigo Wakizashi + Kunai: 3º golpe lança kunai que volta, costas ×2) | **Tempestade** (cada Elo acrescenta 1 golpe ao combo) |
| Naginata | Estocada Relâmpago | **Guarda** (varrido de 200° apara projéteis) | **Cavaleiro** (investida longa após esquiva perfeita) |
| Kusarigama | Laço Ceifador | **Ceifador** (puxão traz o inimigo quebrado direto à finalização) | **Âncora** (puxa você até o alvo) |
| Manoplas | Punho do Trovão | **Leão** (cada parry perfeito dá +1 golpe de combo) | **Pedra** (sem dano, postura ×2) |
| Tessen | Vento Dobrado | **Garça** (projéteis rebatidos perseguem o alvo) | **Brisa** (cura postura dos aliados próximos; coop) |
| Shakujō | Badalada | **Exorcista** (+50% em yokai, −20% em humanos) | **Peregrino** (cada finalização toca o sino: etéreos sólidos por 3 s) |

### E.4.3 Tsubas — **SIMPLIFICAR e transformar em "Lembrança" pré-run** (WP-08)

- **Hoje:** 12 passivos + 3 lendárias da Casa de Chá.
- **Por quê:** um passivo escolhido antes da run é o "Keepsake" de Hades: funciona porque é **uma** escolha, visível e com efeito claro.
- **O que fazer:** 1 tsuba por run (não por arma). Revisar as 15: qualquer uma que mexa em janela de parry, velocidade global ou tempo de anel é redesenhada (R5). Reduzir para 10 + 3 lendárias, removendo as que repetem bônus planos de atributo.

### E.4.4 Maestria — **MANTER e reorientar** (WP-08)

- **Hoje:** 5 níveis por arma (5, 15, 30, 50, 80 finalizações): +15% postura, papel espiritual, golpe de troca +20%, rastro colorido, Ougi no V.
- **O que fazer:** maestria desbloqueia **variedade**, não poder (D-17): I rastro; II **Aspecto II**; III segunda animação de finalização; IV **Aspecto III**; V **Ougi** (mantido: o primeiro golpe de troca da sala vira o golpe secreto). Remover "+15% de postura" e "golpe de troca +20%" permanentes. Remover "papel espiritual" (papéis deixam de existir).

## E.5 Técnicas absorvidas

### E.5.1 Absorção (selo dourado + finalização) — **MANTER: é o segundo gancho**

- **Hoje:** responder certo ao golpe-assinatura marca o inimigo com o selo dourado; finalizá-lo absorve a técnica, que aparece garantida nas cartas seguintes.
- **Por quê:** é o pilar 2 inteiro numa regra só (domínio vira poder), e é narrativa emergente ("aprendi isso com aquele Lanceiro"). Thymesia tentou algo parecido com armas da praga roubadas e recebeu críticas por pouca profundidade e pouca variedade; o Shura acerta onde ele errou porque a técnica exige **ler a assinatura** antes, não só matar.
- **O que fazer:** nada no núcleo. POLIR: quando o selo dourado aparece, o anel da próxima assinatura daquele inimigo ganha um contorno dourado (o jogador entende que existe "algo a aprender" ali).

### E.5.2 Espaços de técnica por verbo — **ESTRUTURAR (nova regra, substitui o acúmulo livre)** (WP-09)

- **Hoje:** técnicas se acumulam sem limite declarado; cada uma tem ganchos (`ModifyAttackDamage`, `OnPerfectParry`, `OnDealtDamage`…).
- **Por quê:** builds sem estrutura viram soma de números e são impossíveis de ler (D-16). Hades organizou as bênçãos por **verbo** (ataque, especial, lançamento, avanço) e isso tornou as builds legíveis à primeira vista: "meu avanço é de Atena".
- **O que fazer:** 4 espaços de **Técnica de Verbo** + passivas livres.

| Espaço | Gancho principal | Exemplo |
| --- | --- | --- |
| Aparar | `OnPerfectParry` | Estocada Espelhada (parry em estocada devolve o golpe) |
| Esquivar | `OnPerfectDodge`, Mikiri | Passo Fantasma |
| Trocar | `OnPerfectSwap`, Elos | Saque Iai |
| Finalizar | `OnFinisher` | Sede de Sangue |
| Passiva (sem limite) | atributos, eventos de sala | Muralha |

Cada uma das técnicas recebe um `Slot` em `TechniqueDef`. Ao absorver uma técnica para um espaço ocupado, a carta oferece **substituir** (a antiga vira Ecos). A interface mostra os 4 espaços como 4 kanji no cartão de pausa, não no HUD de luta.

### E.5.3 Níveis I–III e ramos — **MANTER**

Ramo no III (Forma Plena × Eco) é uma decisão interessante e permanente na run. Manter. "No II o preço some" está bom.

### E.5.4 Marcas (8) e elementos — **FUNDIR num só sistema de marcas** (WP-09, WP-10)

- **Hoje:** marcas 斬 Corte, 守 Guarda, 影 Sombra, 霊 Espírito, 血 Sangue, 火 Fogo, 風 Vento, 雷 Trovão, cada uma com bônus e efeito de Eco; à parte, elementos (Fogo, Gelo, Raio, Sangue, Vento, Sombra) que marcam o inimigo e reagem; à parte, clãs de itens.
- **Por quê:** três sistemas de "etiqueta de sinergia" (marcas, elementos, clãs) com nomes que se repetem (Fogo é marca e elemento; Sangue também). O jogador não sabe qual conta para quê (D-14, D-3).
- **O que fazer:** **um** sistema de 8 marcas. As 4 marcas elementais aplicam o elemento no inimigo; as 4 marcas de estilo dão bônus de estilo. Clãs saem (E.6.3).

| Marca | Tipo | Efeito base no inimigo / no jogador |
| --- | --- | --- |
| 火 Fogo | elemental | queima (dano ao longo do tempo) |
| 氷 Gelo (substitui 風 Vento) | elemental | lentidão de 15% na recuperação do inimigo (nunca no preparo do anel, R5) |
| 雷 Raio | elemental | corrente para 1 inimigo próximo |
| 血 Sangue | elemental | sangramento; finalizar um sangrando cura |
| 斬 Corte | estilo | +dano de golpe |
| 守 Guarda | estilo | +postura causada no parry |
| 影 Sombra | estilo | +costas, +esquiva |
| 霊 Espírito | estilo | +contra yokai e etéreos |

Reações elementais com 4 elementos = 6 pares, todos com nome e efeito: Fogo+Gelo **Vapor** (cega 2 s), Gelo+Raio **Estilhaço** (+40 postura), Raio+Sangue **Sobrecarga** (sangramento salta), Sangue+Fogo **Ebulição** (dano = 8% da vida atual), Fogo+Raio **Trovoada** (explosão em área pequena), Gelo+Sangue **Coágulo** (postura do inimigo não regenera por 4 s). Sombra e Vento deixam de ser elementos; Ressonância do aço sai.

### E.5.5 As 8 fusões — **MANTER**

São as "bênçãos duplas" de Hades: a recompensa por apostar em duas marcas. Revisar só para que cada fusão caiba numa frase e não altere janela de parry.

### E.5.6 Transformação em alma — **FUNDIR na Arte "Alma"** (E.2.10)

Sai a tecla B. Quando uma técnica chega ao nível III, a carta oferece "tornar a alma deste inimigo a sua Arte".

## E.6 Itens, relíquias, bênçãos

### E.6.1 Itens com atributos (4 encaixes, mochila de 12, 10 raridades, 12 atributos, valores até ×5) — **CORTAR o loot de atributos** (WP-10)

- **Hoje:** inimigos têm 18% de deixar item (+1% por profundidade, até 40%); 10 raridades (Comum até Shura); 1 a 5 atributos; mochila de 12; tela de equipamento.
- **Por quê:**
  1. **Duas economias de recompensa competindo.** O Shura já tem a escolha 1-de-3 (técnica, relíquia) depois de cada sala, que é o que faz roguelites funcionarem (D-6, guia 9.3). Um segundo fluxo de loot com atributos transforma a run num looter (Diablo), que é outro gênero com outro público (motivação "Conquista/Poder", não "Maestria/Desafio").
  2. **Decisões não interessantes:** comparar "+4% crítico, +3 vida" com "+5% velocidade" é uma conta, não uma decisão (D-5).
  3. **Viola R7:** tela de mochila durante a run.
  4. **10 raridades** é uma escala que ninguém memoriza; o próprio relatório sugere 5 ou 6. Diablo III precisou de uma reforma inteira ("Loot 2.0", 2014) para cortar volume e aumentar o significado de cada drop.
  5. **No online,** loot individual por jogador multiplica a interface e o tráfego.
- **O que fazer:** `Feature.StatLoot = false`.
  - Inimigos comuns deixam só **Mon**.
  - Os **afixos únicos** dos itens Lendários (Rosa de Ferro, Olho de Hannya, Pena do Tengu, Sino Rachado, Lágrima da Kitsune, Âncora do Kappa) viram **relíquias**.
  - Itens corrompidos viram **relíquias amaldiçoadas** (ganho + defeito que se cura com uma condição), oferecidas no Tesouro Amaldiçoado.
  - Remover a tela de Equipamento e a mochila; o Ferreiro Errante passa a **aprimorar relíquias** e **temperar a arma** (E.11.4).
  - Migração de save: itens guardados viram Ecos (WP-11).

### E.6.2 Relíquias com preço — **MANTER + raridade em 4 níveis** (WP-10)

- **O que fazer:** raridades **Comum, Rara, Lendária, Shura** (cores fixas, D-19). Espaços de relíquia: 4 (5 com nó do Altar). Toda relíquia: uma linha de ganho, uma de custo. Auditar a lista: relíquias que mexem em janela de parry ou tempo de anel são redesenhadas (ex.: Ampulheta Quebrada passa a "parry perfeito congela o atacante 20 f", sem tocar no anel).

### E.6.3 Clãs (bônus de 2 e 4 peças) — **CORTAR** (WP-10)

Sem itens, sem conjuntos. A função "bônus por apostar num tema" já é das marcas e fusões.

### E.6.4 Bênçãos (Vigor, Gume, Guarda, Vento, Calma) — **FUNDIR na Ascensão** (WP-10)

- **Por quê:** carta de atributo puro é a carta menos interessante possível (D-5). Ela ocupa o lugar de uma decisão real.
- **O que fazer:** a Ascensão por sala vencida continua dando vida, dano e postura automaticamente. As bênçãos saem das cartas. No lugar, a carta "de respiro" vira **Cura +40% ou +1 gole máximo** (decisão entre agora e depois).

## E.7 Inimigos

### E.7.1 Bestiário de 36 — **SIMPLIFICAR por matriz de respostas** (WP-13)

- **Hoje:** 36 tipos entram um por profundidade; lacaios; cada um com mecânica e técnica.
- **Por quê:** variedade vale quando exige respostas diferentes (guia 9.2.3, D-13). Com arte reaproveitada e duelistas parecidos (o relatório admite), 36 inimigos que se confundem visualmente valem menos que 24 inconfundíveis (D-24). E com a Jornada fechada em ~24 salas (E.11.1), um inimigo novo por sala nem é alcançável numa run.
- **O que fazer:**
  1. Gerar automaticamente (script de editor ou teste) a **Matriz de Respostas**: para cada inimigo, anéis que usa, resposta exigida, resposta de mestre, mecânica especial, silhueta (tamanho, cor dominante, arma), facção, profundidade de entrada, técnica ensinada. Salvar em `docs/06-Matriz-Inimigos.md`.
  2. Marcar como **duplicado** todo par com mesma resposta exigida e mecânica de mesma família.
  3. **Meta para a 1.0: 24 a 28 inimigos** com silhueta inconfundível em 64 px. Os demais ficam atrás de `Feature.ExtraEnemies` (pós-lançamento). As técnicas dos cortados migram para inimigos que ficam, se forem boas.
  4. **Candidatos a fusão** (a IA confirma com a matriz):

| Candidatos | Por quê | Proposta |
| --- | --- | --- |
| Nopperabō (imita o último caído) + Espelho do Shura (sua arma) | mesma família "imitador" | um só: **Nopperabō**, que imita o último inimigo ou, se não houver, você |
| Sombra + Ninja do Trovão + Kunoichi | três ninjas de ataque pelas costas/distância | manter Kunoichi (distância) e Ninja do Trovão (cópias); Sombra vira afixo de elite "Velado" |
| Berserker (fúria abaixo de 50%) | regra genérica | vira afixo de elite "Furioso" |
| Vampiro (Kyūketsuki) e o chefe Ō-Kyūketsuki | o comum antecipa o chefe sem novidade | manter o comum só como lacaio do chefe |
| Esqueleto e Gashadokuro Colossal | mesma família | Esqueleto fica, renomeado como "Ossada", entra no Abismo como lacaio do chefe |
| Lodo e Lodo pequeno | ok como lacaio | manter |

  5. **Ordem de entrada:** com a Jornada em atos, os inimigos entram **por ato** (3 a 5 novos por ato, um por sala, com o padrão ensina → testa → torce → conclui, guia 10.2), não por profundidade absoluta.

### E.7.2 Facções e ecologia — **MANTER + tornar visível** (WP-13)

- **Por quê:** comportamento emergente barato (D-14), mas hoje quase invisível.
- **O que fazer:** cada facção tem um ícone pequeno sobre a cabeça quando há mais de uma facção na sala (人 Humano, 妖 Yokai, 鬼 Oni, 死 Mortos). Quando duas facções brigam, um kanji 争 aparece entre elas. A Xamã cura com um fio de luz visível ligando-a ao alvo.

### E.7.3 Formações (5) — **MANTER**

Ótimas: são o "torcer" do kishōtenketsu (guia 10.2) para inimigos já conhecidos. Entram só a partir do Ato 2.

### E.7.4 Elites com afixos — **MANTER**, recalibrar a entrada para o Ato 2 (antes: profundidade 15).

### E.7.5 Moral e rendição — **MANTER**

Pequeno, temático, gera decisão de Karma visível. Garantir que "poupar" é contextual (Interagir) e que o kanji do Karma aparece na hora (E.13.2).

## E.8 IA adaptativa 2.0 — **SIMPLIFICAR para 5 leituras e mostrar só no 3** (WP-13)

- **Hoje:** nove leituras (ATRASO, PRESSÃO, DEFESA, PERSEGUIÇÃO, PERIGO, FLANCO, FERRAMENTA, TROCA, CURA), medidor de 0 a 3 no HUD; contra-leitura dobra a postura e zera a leitura; "O Shura Lembra" opcional.
- **Por quê:** a IA que lê o jogador é uma ótima ideia e a **contra-leitura é uma decisão interessante de verdade** (D-5): o jogador pode iscar a IA. Mas nove medidores no HUD violam R3 e D-4, e três leituras atacam o próprio design: **TROCA pune o gancho**, **FERRAMENTA** perde sentido com a Arte, **CURA** pune a decisão de curar, que já tem custo.
- **O que fazer:**
  - Manter: ATRASO, PRESSÃO, DEFESA, PERSEGUIÇÃO, PERIGO (as cinco originais). FLANCO pode entrar como sexta se a matriz mostrar inimigos suficientes que giram.
  - Cortar: TROCA, FERRAMENTA, CURA.
  - HUD: nada até uma leitura chegar a 3. Aí, um kanji sobre os inimigos afetados e uma linha de texto por 2 s ("Eles aprenderam: você apara cedo"). É o momento de ensinar a contra-leitura.
  - "O Shura Lembra": desligado por padrão; vira Juramento "Leitura Profunda" nível máximo.

## E.9 Rivais do Shuradō — **REDESENHAR como "Algozes" (risco jurídico)** (WP-14)

- **Hoje:** quem te mata ganha nome, leva um quinto dos Ecos, rouba algo da build, guarda cicatriz, reaparece num Covil (仇); hierarquia de até 15 Capitães (1–3 estrelas) e 5 Generais com guarda-costas; vingar Capitães enfurece o General; Muralha dos Rivais mostra a hierarquia; rivais emprestados no online.
- **Por quê:** o próprio guia (2.3) avisa que o sistema Nemesis é **patenteado nos EUA pela Warner Bros. (patente concedida em 2021)** e recomenda se inspirar no princípio, não na implementação. A implementação atual reproduz justamente os elementos mais característicos do Nemesis: hierarquia de capitães e chefes, promoção, guarda-costas, reação de um chefe à morte de seus subordinados. Num lançamento na Steam (mercado dos EUA incluído), isso é um risco real. *Não sou advogado; a decisão final depende de consulta jurídica (Parte J.1).*
- **O que fazer (padrão recomendado, atrás de `Feature.RivalHierarchy = false`):**
  - Mantém-se o **princípio** que dá a emoção: o inimigo que te matou volta, lembra de você e carrega o que levou.
  - **Algoz (仇):** o assassino ganha nome, leva parte dos Ecos e **uma** coisa (a técnica mais alta *ou* a tsuba), guarda uma cicatriz visual.
  - **Sem hierarquia, sem patentes, sem promoção, sem guarda-costas, sem relações entre Algozes.** No máximo 3 Algozes ativos; o mais antigo "se perde no Shuradō" quando surge um quarto.
  - Aparece **uma vez** num Covil do Algoz no mapa de uma run futura, com fala própria e a sua coisa roubada; vencê-lo devolve os Ecos +50% e a coisa roubada.
  - A Muralha dos Rivais vira uma aba "Algozes" na Biblioteca (lista, sem organograma).
  - Rivais emprestados de amigos: ADIAR (depende de presença assíncrona, E.15.6).
  - Trocar todo texto "Nêmesis", "Capitão", "General" por "Algoz", "Vingança".

## E.10 Chefes — **MANTER + POLIR a arte** (WP-12, WP-16)

- **Hoje:** Kagutsuchi, Jorōgumo, Ō-Kyūketsuki, Gashadokuro Colossal, Yuki-onna e o Ashura, cada um com 3 fases e várias barras de postura; Ponto de Ruptura pula uma fase; o Ashura decide o final.
- **Por quê:** chefes são o "conclui" de cada ato (guia 10.2), o material de marketing mais forte e o teste de domínio do jogador. Estão no lugar certo. O ponto fraco é a arte (ampliada e tingida).
- **O que fazer:** (1) cada Senhor do Bioma fecha o seu ato (E.11.1). (2) Ponto de Ruptura: manter, e deixá-lo **descobrível** com uma dica no pergaminho do chefe depois da primeira derrota para ele (morte que ensina, D-18). (3) Prioridade número 1 de arte para a 1.0 (WP-16): arte própria para os 6 chefes. (4) Ō-Kyūketsuki, fase 3 "sem anéis": manter o escuro, mas com os anéis ainda visíveis (R5 e D-19); o desafio passa a ser ver o inimigo, não o anel.

## E.11 Mundo e estrutura da run

### E.11.1 Run infinita — **REDESENHAR: Jornada de 4 atos + Roda Eterna** (WP-12)

- **Hoje:** biomas de 5 salas em sequência sem fim; Guardião a cada 5 salas; Senhores nas profundidades 4, 14, 19, 24, 29; Ashura na 49 e a cada 50; Kalpas a cada 25.
- **Por quê:** o guia (9.3) recomenda runs de 20 a 45 minutos que cabem numa sessão. Uma run sem fim não tem arco (tensão → clímax → alívio), adia o primeiro "zerei" para muito longe, dificulta balancear e torna o Ashura algo que a maioria nunca vê. Hades, Slay the Spire e Dead Cells têm uma run **com fim** e um modo de dificuldade crescente *depois* da primeira vitória. A distribuição atual dos Senhores (4, 14, 19, 24, 29) também não segue os biomas de 5 salas.
- **O que fazer:**

| Ato | Biomas possíveis | Salas no mapa | Fecha com | Hora do dia (fixa) |
| --- | --- | --- | --- | --- |
| I — A Chegada | Cinzas ou Bosque | 6 | Senhor do bioma | aurora → manhã |
| II — A Descida | 1 dos restantes (Musgo, Sangue, Gelo, Abismo) | 6 | Senhor do bioma | tarde |
| III — O Fundo | 1 dos restantes | 6 | Senhor do bioma | noite |
| IV — O Trono | Shuradō | 2 + Ashura | Ashura → final | eclipse |

  - **Duração-alvo:** 35 a 45 minutos numa vitória (medir, Parte I).
  - **Guardião** deixa de ser fixo a cada 5 salas: vira um **nó de elite opcional no mapa** (mais risco, item garantido), como as elites de Slay the Spire.
  - **Roda Eterna** (modo liberado após a primeira vitória, ou escolhido no final "a roda gira de novo"): continua depois do Ashura com os **Kalpas** (regras permanentes a cada ato), Yomi e afixos crescentes. Não precisa de novo item de menu: é a opção "continuar descendo" no fim da Jornada.
  - **Suspender run:** salvar e sair entre salas (o jogo é por semente: salvar semente, profundidade, build e estado). Returnal precisou acrescentar isso depois do lançamento por pedido dos jogadores; o Shura deve ter desde já.

### E.11.2 Biomas (10 + zona corrompida) — **SIMPLIFICAR para 6 biomas de ato + variantes** (WP-12)

- **Por quê:** D-24 (um bioma excelente vence três medíocres) e a Jornada só usa 4 por run.
- **O que fazer:**

| Bioma da 1.0 | Absorve | Regra própria (mantida ou ajustada) | Chefe |
| --- | --- | --- | --- |
| Cinzas | Castelo (variante de arena com shōji) | fogo dura o dobro; erupções marcadas | Kagutsuchi |
| Bosque | Bosque Selvagem + Bambuzal (variante) | árvores e bambus que caem no corte | Jorōgumo (move-se do Musgo) |
| Musgo | — | esquiva 30% mais longa; esporos escondem **o inimigo**, não o anel | — (sorteia o Jorōgumo se o Bosque não saiu) |
| Sangue | — | cura −50%, roubo de vida dobrado | Ō-Kyūketsuki |
| Gelo | — | sua postura regenera 40% mais devagar; gelo fino | Yuki-onna |
| Abismo | Rio Sanzu (variante de arena com água) | escuridão (anéis sempre visíveis), bordas | Gashadokuro |
| Shuradō | — | arena do Ashura | Ashura |
| Yomi | — | só na Roda Eterna | — |

  Zona corrompida: MANTER como modificador de sala (barato e variado).

### E.11.3 Mapa de rotas — **MANTER**

Provado em Slay the Spire. Ajustes: voto online de 25 s está bom; mostrar no mapa o **nó de elite** e o **Covil do Algoz** com ícones claros; garantir pelo menos uma rota com Santuário ou Mercador antes de cada Senhor (D-7).

### E.11.4 Tipos de sala — **MANTER com geração protegida** (WP-12)

- **O que fazer:** sorteio por **saco embaralhado** por ato (D-21), por exemplo, num ato de 6 salas: 3 combates, 1 evento, 1 entre {Santuário, Mercador}, 1 entre {Duelo, Desafio, Disputa, Tesouro Amaldiçoado}. Ferreiro Errante: aprimorar 1 relíquia (sobe a raridade) **ou** temperar a arma (+10% de dano na run) **ou** purificar uma maldição. Remover fundir, gravar, quebrar e afiar (dependiam dos itens).

### E.11.5 Eventos (8) — **MANTER**

Cada evento já é uma decisão com custo e consequência (D-5). Lâmina de Muramasa passa a ser uma das portas do Caminho do Asura (E.2.9). Duelo de Honra mantido.

### E.11.6 Arenas e destrutíveis — **MANTER**

Formas, estátuas, lanternas, armadilhas, fossos, shōji, bambus, óleo em chamas: é variedade que muda decisões de posicionamento (E.2.7).

### E.11.7 Dia de 24 horas em tempo real — **SIMPLIFICAR para hora por ato** (WP-12)

- **Hoje:** relógio de 1 minuto de jogo por segundo; período muda luz e elenco.
- **Por quê:** efeito mecânico invisível (o jogador não sabe que o elenco mudou por causa da hora), relógio no HUD (R3) e ruído de aleatoriedade. Como progressão visual, porém, é linda: cada ato numa hora diferente conta a descida.
- **O que fazer:** a hora é fixa por ato (tabela de E.11.1). Mantém a gradação de cor, os sons de hora (pássaros, sino, cigarras) e o efeito "à aurora os mortos nascem fracos" só no Ato I. Remover o relógio do HUD.

### E.11.8 Clima (6) — **SIMPLIFICAR para 4 com ícone e regra visível** (WP-12)

Manter Chuva (Fogo −50%, Raio +50%), Vento (projéteis fazem curva), Neblina (inimigos aparecem a 6 m, **anéis sempre visíveis**), Neve (pegadas revelam invisíveis). Tempestade: cortar (dano aleatório que prefere quem usa aço é invisível e injusto). Lua cheia: só com o Caminho do Asura. No máximo um clima por sala; banner de 2 s ao entrar com a regra em uma linha.

### E.11.9 Kalpas — **REBAIXAR para a Roda Eterna** (WP-12)

As 6 regras de Kalpa são excelentes "dificuldade por regras" (guia 8.3); elas vivem na Roda Eterna, uma por ato.

## E.12 Meta-progressão

### E.12.1 Templo em Ruínas (8 construções) — **SIMPLIFICAR para 6 lugares** (WP-11)

| Hoje | Depois |
| --- | --- |
| Altar das Almas | Altar (mantido) |
| Forja | **Arsenal**: armas, aspectos, tsuba, Arte |
| Dojo | Dojo (lições essenciais + Treino Livre, E.14) |
| Biblioteca, Cemitério da Linhagem, Muralha dos Rivais | **Biblioteca** com abas: Bestiário, Algozes, Linhagem, Pergaminhos |
| Jardim Zen | Jardim (Juramentos após a 1ª vitória, cosméticos) |
| Casa de Chá | Casa de Chá (mantida) |

Menos lugares para andar, mesma informação.

### E.12.2 Moedas (Ecos, Aço Celeste, Fragmentos de Alma, Pétalas + Mon) — **SIMPLIFICAR para Ecos, Pétalas e Mon** (WP-11)

- **Por quê:** R6. Cada moeda extra é uma regra a aprender e uma tela a mais. Aço Celeste vinha do ferreiro e dos itens (que saem); Fragmentos vinham de absorver técnica repetida (que agora sobe o nível ou vira Ecos).
- **O que fazer:** Ecos (tudo permanente), Pétalas (cosméticos, multiplicadas pelo Calor), Mon (só na run). Migração `MetaProgress` v3: Aço Celeste × 5 → Ecos; Fragmentos × 10 → Ecos (taxas iniciais, documentar).

### E.12.3 Altar em árvore — **SIMPLIFICAR: sem janela de parry, com escolhas excludentes** (WP-11)

- **Hoje:** Corpo (Vigor, Postura de Aço, Fôlego), Lâmina (Lâmina Temperada, Olhar Afiado), Mente (Meditação, Despertar, Visão do Destino); 12 pontos (+1 a cada 3 runs, até 22; +2 do Monge); fins de ramo Corpo de Diamante, Segunda Troca, Clarividência; redistribuir.
- **O que fazer:**
  - **Olhar Afiado** (janela de parry): substituir por **"Olhar Afiado: o anel mostra a seta da resposta de mestre nos primeiros 6 f"** (ajuda a ler, não muda o tempo).
  - **Fôlego** (some com o fôlego): substituir por **"Fôlego: +1 carga de esquiva"**.
  - **Corpo de Diamante** (ignorava o primeiro ferimento, que não existe mais): passa a ignorar o primeiro golpe recebido de cada sala.
  - Cada nó ganha **duas versões excludentes** (o Espelho da Noite de Hades): ex. Vigor = "+vida máxima" **ou** "+1 gole de cabaça". O teto de pontos fica; o que cresce é a escolha, não o poder.
  - Teto de poder total do Altar: medir para que um Altar cheio reduza o tempo de vitória em no máximo ~20% em relação a um Altar vazio (hipótese a validar com bots, Parte I.4).

### E.12.4 Juramentos (10) — **MANTER, liberar após a 1ª vitória** (WP-11)

- **Por quê:** é o Pacto de Punição de Hades e a Ascensão de Slay the Spire, ambos liberados depois da primeira vitória (D-17). Antes disso, o jogador ainda está aprendendo; dificuldade opcional confunde.
- **Ajustes:** Anel Silencioso no nível máximo **substitui o Modo Mestre**; Kalpa Precoce passa a valer para a Roda Eterna; Rival Eterno vira **Algoz Eterno**; Leitura Profunda absorve "O Shura Lembra"; acrescentar **Caminho do Asura** (ativa o medidor de Shura desde o início).

### E.12.5 Linhagem, idade, Senseis, herança — **SIMPLIFICAR: idade é narrativa, não atributo** (WP-11)

- **Hoje:** discípulo com idade e traços; passar da profundidade 10 envelhece 5 anos; faixas de idade mudam velocidade, fôlego e **janela de parry**; aos 70 vira Sensei (+6 vida, +2% dano, até 5); herdeiro entre 3 com 1–2 traços; herda a técnica mais alta; jisei no túmulo.
- **Por quê:** a linhagem é uma das partes mais bonitas e "contáveis" do jogo (o próprio relatório destaca). Mas idade mudando janela de parry e velocidade viola R5 e D-8: o jogador melhora e o personagem piora sem ele decidir. Senseis são poder permanente acumulado (D-17). Rogue Legacy mostrou que herdeiros com traços funcionam quando os traços são **escolhidos** entre opções e mudam o *estilo*, não quando punem em silêncio.
- **O que fazer:**
  - Idade: só visual (cabelo, postura, voz, frase do jisei) e narrativa (falas da Casa de Chá).
  - Traços: 1 por discípulo, escolhido entre 3 candidatos, sempre com efeito de **estilo** e uma linha visível (ex.: "Canhoto: os cortes começam do outro lado"; "Herdeiro Hattori: começa com a Arte Fumaça"). Nenhum traço mexe em tempo de anel ou janela.
  - Sensei: aos 70, em vez de +vida e +dano, **desbloqueia uma lição avançada no Dojo e um ornamento**.
  - Herança da técnica mais alta: manter (começa a run com ela no nível I). É variedade, não poder bruto.
  - Jisei no túmulo: manter (é material compartilhável).

### E.12.6 Fins de ramo do Altar: Segunda Troca e Clarividência — **MANTER, com uma ressalva**

- **Clarividência** (mapa mostra a sala inteira): manter; é informação, não poder (D-19).
- **Segunda Troca** (levar 3 armas): é uma mudança de regra legítima de fim de jogo, mas mexe no gancho (a Troca Perfeita passa a ter duas armas candidatas). Manter atrás de `Feature.ThirdWeapon = true` e observar no Playtest 2: se confundir a leitura do anel roxo, vira Juramento invertido ou é cortada.

### E.12.7 Liberação em camadas — **MANTER e reordenar** (WP-11)

A ideia é excelente (guia 10.1: uma ideia por vez). A ordem passa a seguir o novo núcleo:

| Momento | O que aparece |
| --- | --- |
| Dojo + 1ª run | anel ciano, parry, postura, finalização, anel vermelho e esquiva, cabaça |
| Fim do Ato I (1ª vez) | anel roxo e Troca Perfeita (lição curta do Dojo liberada) |
| 2ª run | Arte, técnicas por verbo, mapa com elites |
| 1º Senhor derrotado | Mikiri e respostas de mestre como lição, finta |
| 4ª run | Algozes, eventos completos, Casa de Chá com missões |
| 1ª morte no Ato III | Caminho do Asura (relíquia e evento passam a aparecer) |
| 1ª vitória | Juramentos, Roda Eterna, Aspecto III, Desafio Diário |

## E.13 Narrativa e rituais

### E.13.1 Casa de Chá (6 hóspedes) — **MANTER e priorizar a escrita reativa** (WP-15)

- **Por quê:** é o motor narrativo de Hades: falas que reagem à última morte fazem cada derrota avançar algo (D-18).
- **O que fazer:** priorizar falas que reagem a: quem te matou, em que ato, com que arma, se você venceu um chefe pela primeira vez, se usou o Caminho do Asura. Meta de volume para a 1.0: 30 a 50 falas por hóspede, com sistema de prioridade (fala nova e relevante antes de fala genérica). As missões: Zatō libera o Juramento Anel Silencioso; Tsubaki libera Duelos de Honra e o final secreto; Ferreiro sem Rosto, as tsubas lendárias; Zashiki-warashi, os pergaminhos; Monge, nós do Altar; Kohaku, Artes.

### E.13.2 Karma (−100 a +100) — **MANTER + tornar visível** (WP-07)

- **Por quê:** muda falas, eventos, relíquias oferecidas e o final, mas hoje cada ação de Karma é silenciosa (falha no teste da visibilidade).
- **O que fazer:** toda ação que move o Karma mostra, no samurai, por 1 s, 義 (Bushidō, dourado) ou 修 (Shura, vermelho) com um som curto. A tela de morte mostra o Karma atual numa linha.

### E.13.3 Rituais (Chiburi e Nōtō, Reverência, Oração) — **MANTER**

São o "alívio" depois da tensão (D-9) e pura fantasia (MDA). O Chiburi no tempo certo é um micro-desafio de timing fora da luta: ótimo. Sem mudanças.

### E.13.4 Pergaminhos de inimigo — **MANTER**

Metas de maestria por inimigo (como os desafios de Hitman). A recompensa "técnica já no nível II" é poder, mas pequeno e conquistado por domínio: compatível com o pilar 2.

### E.13.5 Jisei — **MANTER + botão de compartilhar**

Botão "Salvar imagem" na tela de morte (PNG em sumi-e com o haicai, o nome do discípulo e o Algoz). Material de comunidade de custo quase zero.

### E.13.6 Finais (4) — **MANTER**

Libertação, O Novo Ashura, Final secreto e "a roda gira de novo" (que agora leva à Roda Eterna). O Novo Ashura depende de presença assíncrona para aparecer na run de amigos (E.15.6): no padrão, ele aparece na **sua** Roda Eterna como chefe espelho.

## E.14 Modos — **SIMPLIFICAR para 4 no menu** (WP-12)

| Hoje | Veredito | Depois |
| --- | --- | --- |
| Jornada | MANTER | Jornada de 4 atos (+ Roda Eterna após a vitória) |
| Diário | MANTER | Desafio Diário (semente do dia; ranking local por enquanto) |
| Torre | FUNDIR | Some: a Jornada já tem objetivo fixo |
| Mestre | REBAIXAR | Juramento Anel Silencioso no máximo |
| Peregrinação | FUNDIR | Some: a Jornada já tem duração fixa |
| Dojo (21 lições) | SIMPLIFICAR | **8 lições essenciais** (mover e atacar; anel ciano e parry; postura e finalização; anel vermelho e esquiva; Mikiri; anel roxo e Troca Perfeita; Arte e cabaça; finta) + **Treino Livre** contra qualquer inimigo já visto, com indicador de "cedo/tarde em N quadros". O prólogo e o tour do HUD ficam, encurtados para o novo HUD; o "tempo congelado no instante do parry" (o melhor recurso do Dojo) é mantido em todas as lições de timing; as 13 lições restantes viram desafios opcionais |
| Duelo PvP | SIMPLIFICAR | **Duelo entre amigos**, sem ranking (E.16) |

## E.15 Online cooperativo

### E.15.1 Até 8 jogadores — **SIMPLIFICAR: suporte oficial até 4** (WP-19)

- **Por quê:** (1) Legibilidade: com 8 samurais, até 24 inimigos e efeitos, o anel do *seu* atacante compete com dezenas de estímulos (D-3, D-4). (2) Balanceamento: escalar vida, ondas e atacantes de 1 a 8 é uma curva 8 vezes maior para testar. (3) Mercado: os roguelites de ação cooperativos de referência ficam em 3 ou 4 jogadores (Risk of Rain 2 e Ravenswatch com 4, Windblown com 3). (4) Teste: 8 jogadores reais pela internet nunca foram testados.
- **O que fazer:** `Net.MaxPlayers = 4` como padrão; `Feature.PartyMode8` (5 a 8, rotulado "Festa — experimental") desligado por padrão, disponível em Opções avançadas da sala.

### E.15.2 Cliente do navegador — **ADIAR (congelar)** (WP-19)

- **Por quê:** o relatório já diz que ele dobra o trabalho de cada sistema e ficou para trás (sem duelo, sem presença, sem pátio). Com convidado nativo e Steam funcionando, o navegador não paga o custo.
- **O que fazer:** `Feature.BrowserClient = false`. Não remover os arquivos ainda (WP-20 decide). Remover a menção ao navegador do fluxo principal de "Jogar online".

### E.15.3 Arquitetura (anfitrião autoritativo, ~45 snapshots/s, previsão local, anel segurado pelo ping) — **MANTER**

Está correta para coop. Manter as entradas pelo cliente nativo, pela Steam e por IP (mesma rede ou Radmin). Acrescentar: teste de desempenho com 4 jogadores + 12 inimigos + efeitos (meta: 60 fps estáveis no anfitrião num PC de referência a definir pelo diretor).

### E.15.4 Ações conjuntas, Ameaça e papéis, Queda e resgate — **MANTER**

- Ressonância de postura e golpe de grupo (anel branco partido): manter.
- Kiai em grupo: passa a exigir 2 jogadores (era 3), via Arte Kiai.
- Ameaça e papéis por arma: manter (papéis emergentes sem classes fixas são elegantes).
- Queda e resgate: manter.

### E.15.5 Pacto de Sangue — **CORTAR** (WP-19)

Nicho (dupla fixa), depende do Shura (agora opcional) e de mais uma tela no lobby. `Feature.BloodPact = false`.

### E.15.6 Presença assíncrona sem servidor — **ADIAR** (WP-19)

O relatório admite que quase não alcança ninguém sem servidor. Manter o código, `Feature.AsyncPresence = false`, e reavaliar se um dia houver servidor (mensagens de tinta e manchas funcionam em Dark Souls por causa da população enorme).

### E.15.7 Pátio do Templo (lobby) — **MANTER + POLIR**

Lindo e barato de manter. Acrescentar: cada jogador com uma **cor própria** (faixa/lenço) que se repete no samurai durante a run (WP-16).

### E.15.8 Clareza no coop — **ACRESCENTAR opções** (WP-16, WP-19)

Opção "Efeitos dos aliados" (100% / 50% / 25% de opacidade); anéis só no tamanho cheio para inimigos que miram **você**; anéis de inimigos que miram aliados aparecem menores e semitransparentes.

## E.16 Duelo PvP com rollback — **SIMPLIFICAR: duelo entre amigos, sem ranking** (WP-19)

- **Hoje:** 1v1, melhor de 3, `DuelSim` em inteiros, rollback de até 10 quadros, Elo local (K = 32), temporadas mensais, títulos.
- **Por quê:** a engenharia está excelente e deve ficar. Mas ranking guardado no save de cada jogador é trivial de falsificar (o relatório admite), e jogos indie que dependem de população PvP sofrem quando a base é pequena (Absolver e vários outros perderam a fila de PvP pouco depois do lançamento). Torneio, Invasão e Guerra de Clãs estão fora do escopo da 1.0.
- **O que fazer:** manter o duelo entre amigos (convite e pátio). `Feature.PvpRanked = false`: esconder Elo, temporadas e títulos. Torneio, Invasão e Guerra de Clãs: ADIAR (não implementar).

## E.17 HUD, telas e controles

### E.17.1 HUD de batalha — **REDESENHAR dentro do Orçamento** (WP-15)

| Elemento hoje | Depois |
| --- | --- |
| Cartão do duelista: vida (gota), postura (tsuba), fôlego, Shura, esquivas (pena), boneco de ferimentos, 4 figurinhas de guarda | **Vida, postura e Fluxo** no cartão; esquivas como 2 pontos sob os pés; Shura só no Caminho do Asura; fôlego, ferimentos e guardas saem |
| Cinto: armas com carga, cabaças, bandagens, ferramenta e papéis, bolsa de Mon | **2 armas, goles de cabaça, cargas da Arte**; bandagens, papéis e Mon saem da luta (Mon aparece no mapa, no mercador e ao fim da sala) |
| Topo: sala com porta, hora, clima, Kalpa, regra do bioma; Fluxo e Ecos | **Banner de 2,5 s ao entrar na sala** (nome, clima, regra); depois só um ícone pequeno de clima. Ecos só fora da luta |
| Leituras da IA (●●○) para 9 leituras | Nada até uma leitura chegar a 3 (E.8) |
| Faixa do rival ou chefe com fases | Mantida |
| Registro recolhível | Recolhido por padrão |
| Selos de Ki no inimigo | Só com a técnica Selo Interno |

Teste de aceitação do HUD: capturas 1920×1080 com 3 inimigos; **nenhum elemento de HUD na região central de 60% da tela**, salvo o que está preso a personagens.

### E.17.2 Telas — **SIMPLIFICAR** (WP-15)

Saem: Equipamento (mochila), Forja de 100 combinações (vira Arsenal), Muralha dos Rivais e Cemitério como telas separadas (viram abas da Biblioteca). Entra: **Tela de morte "a morte ensina"** com replay dos últimos 3 s (o jogo já é determinístico: reproduzir a partir de um *buffer* de estados/entradas), o anel que matou destacado, o erro em quadros ("aparou 5 f cedo"), o Algoz, o jisei e o que fica.

### E.17.3 Controles — **REDESENHAR para 7 teclas de combate** (WP-05)

| Ação | Teclado + mouse | Controle |
| --- | --- | --- |
| Mover / mirar | WASD / mouse | analógico esq. / dir. |
| Atacar / finalizar (segurar = mira dirigida) | botão esquerdo | RB |
| Aparar (toque) / bloquear (segurar) | botão direito | LB |
| Esquivar | Espaço | A / ✕ |
| Trocar arma (Troca Perfeita) | Q ou botão do meio | Y / △ |
| Cabaça | F | B / ○ |
| Arte | R | RT / R2 |
| Interagir (poupar, reviver, eventos) | E | X / □ |
| Trava de mira | Z (opcional) | R3 |
| Pausa / mapa | Esc / Tab | Start / Select |

Saem: G (detonar), V (kiai), B (alma), H (bandagem), T (segunda ferramenta), 1–4 (guardas). Remapeamento continua completo; acrescentar opção "segurar ou alternar" para bloqueio e trava.

### E.17.4 Interface em IMGUI — **MANTER por ora** (WP-15)

Reescrever ~30 telas em UI Toolkit agora custaria semanas sem ganho para o jogador. Fazer: um helper de layout em `UiKit` que mede texto e quebra linha antes de desenhar (resolve a classe de bugs de texto sobreposto) e usá-lo em toda tela tocada. UI Toolkit: ADIAR para telas novas pós-1.0.

## E.18 Arte e áudio

### E.18.1 Direção de arte — **POLIR com shader unificador e silhuetas** (WP-16)

- **Hoje:** pacote Ninja Adventure + arte procedural; duelistas parecidos; chefes ampliados e tingidos; recortes frágeis de folhas.
- **Por quê:** o guia (12.2) recomenda um shader que unifique assets de origens diferentes e silhuetas fortes; a arte precisa ser **coerente, legível e com identidade**, não cara.
- **O que fazer:**
  1. **Pós-processamento "tinta"** opcional e ligado por padrão: contorno escuro de 1 px em personagens, mapeamento de paleta limitada por bioma, leve textura de papel washi. É o que dá unidade ao pixel art com a interface sumi-e.
  2. **Regras de cor fixas** (`docs/07-Guia-de-Estilo.md`, uma página): ciano = aparável; vermelho = perigo; roxo = espiritual/troca; dourado = domínio (selo, Fluxo, Mikiri); cores de jogador no coop nunca usam esses quatro tons.
  3. **Silhuetas:** cada inimigo da lista da 1.0 precisa ser reconhecível em 64 px em preto puro. Teste: exportar silhuetas lado a lado e conferir.
  4. **Duelistas jogáveis:** 8 aparências com chapéu/elmo e cor de faixa distintos.
  5. **Chefes:** arte própria (prioridade máxima de arte). Se for usar IA generativa ou freelancer, registrar licença e declarar na Steam (guia 12.3).
  6. Folhas de tiles: substituir recortes frágeis por coordenadas nomeadas e testadas (o bug da Muramasa).

### E.18.2 Áudio — **POLIR: som que informa** (WP-17)

- Cada tipo de anel com som próprio (tabela de E.2.4), **direcional** (pan estéreo pela posição do atacante).
- Respostas de mestre, Troca Perfeita e quebra de postura com assinaturas sonoras distintas.
- Trilha por ato e por chefe: ADIAR o conteúdo, mas preparar o sistema de camadas (já existe) para receber faixas por ato.

## E.19 Acessibilidade — **COMPLETAR** (WP-17)

O relatório lista o que falta. Especificação:

| Opção | Detalhe |
| --- | --- |
| Legendas de som | ícone + texto curto na borda da tela na direção do som ("taiko ←") para os sons de anel |
| Alto contraste | anéis com contorno preto e branco grosso; inimigos com contorno |
| Assistência de tempo | janela de parry +0 a +4 f e velocidade do jogo 70 a 100%, numa seção "Assistência" com aviso de que muda a experiência (padrão Celeste); **é a única exceção a R5** |
| Tremor e flashes | sliders de 0 a 100% (verificar se já existem e unificar) |
| Segurar ou alternar | bloqueio, trava, interagir |
| Tamanho de texto | separado do tamanho da interface |
| Daltonismo | já há padrões nos anéis; acrescentar prévia das 4 formas na tela de opções |

## E.20 Arquitetura e engenharia

| Tema | Veredito | O que fazer |
| --- | --- | --- |
| Simulação fixa 60 Hz, semente, determinismo | MANTER | Base de replay, Diário, suspender run e "a morte ensina" |
| Conteúdo em código (`EnemyDefs`, `WeaponDefs`…) | MANTER | Mas números de balanceamento migram para `Balance.json` carregado em `GameConfig` (WP-02), editáveis sem recompilar |
| `PlayerBuild.RecomputeModifiers` como ponto único | MANTER | Acrescentar uma asserção em teste: nenhum modificador altera `ParryWindowFrames` ou o tempo de anel fora da Assistência |
| `World`, `Game`, `GameUI` gigantes | ADIAR refatoração grande | Regra do escoteiro: ao tocar num parcial, extrair o serviço que você mexeu (ex.: `ArtSystem`, `TelemetryService`) |
| Feature flags | ACRESCENTAR | WP-00 |
| Telemetria | ACRESCENTAR | WP-01 |
| Localização | ACRESCENTAR | WP-18 |
| Save v3 com migração | ACRESCENTAR | WP-11 |
| Bots | MANTER + **bot competente** | WP-20: bot com taxa de acerto de parry configurável para medir curvas de dificuldade |

---

# PARTE F — O que funcionou e o que falhou em outros jogos

Esta parte justifica as decisões da Parte E com casos reais. Leia como contexto; não é lista de tarefas. Os casos vêm de conhecimento consolidado da área até meados de 2026 e de palestras e postmortems públicos; a busca web estava desativada nesta sessão, então trate os detalhes como referência de design, não como dado verificado.

## F.1 Combate de duelo e parry

| Jogo | O que fez | Resultado | Lição aplicada ao Shura |
| --- | --- | --- | --- |
| **Sekiro** (2019) | Um botão de deflect, postura para os dois lados, **sem barra de stamina do jogador**, Mikiri para estocadas, cabaça de cura, golpes perigosos com um kanji vermelho | Aclamado; o combate é estudado até hoje como referência de clareza | Cortar o Fôlego (E.2.2); uma resposta de mestre por golpe perigoso (E.1.5); um símbolo único para o perigo (anel vermelho) |
| **Nine Sols** (2024) | Parry estilo Sekiro em 2D, com um recurso (Qi) construído em volta do parry | Muito bem recebido por fãs de Sekiro | Os Selos funcionam quando o jogo é construído em volta deles; no Shura, que já tem postura, eles viram técnica (E.2.3) |
| **Windblown** (2024) | Trocar de arma no fim do combo dispara um golpe especial (Alterattack) | Mecânica destacada nas análises do acesso antecipado | Valida o mercado para a Troca Perfeita; o Shura vai além ao fazer a troca *aparar* (E.2.1) |
| **Ghost of Tsushima** (2020) | 4 posturas, cada uma forte contra um tipo de inimigo; impasse de saque | Impasse muito elogiado; posturas funcionais, mas com troca frequente que parte do público achou mecânica | Manter o impasse (E.2.8); posturas viram identidade das armas (E.2.5) |
| **For Honor** (2017) | Guarda direcional em 3 direções, combate profundo | Profundo, mas no lançamento sofreu com jogo passivo dominante e problemas de rede P2P | Punir passividade (D-12); rede de duelo exige rollback (o Shura já tem) |
| **Absolver** (2017) | Baralho de golpes montado pelo jogador, posturas, PvP | Elogiado pela ideia; a população online caiu rápido depois do lançamento | Complexidade de montagem afasta o público médio; PvP sem população não sobrevive (E.16) |
| **Thymesia** (2022) | Armas da praga roubadas dos inimigos | Recepção mista; crítica a pouca profundidade e variedade dessas armas | A absorção do Shura precisa exigir domínio (selo dourado) e mudar o estilo (técnicas por verbo, E.5.2) |
| **Sifu** (2022) | Morrer envelhece o personagem: mais dano, menos vida | Mecânica marcante; o jogo recebeu modos de dificuldade depois do lançamento para ampliar o público | Envelhecimento tem força narrativa; no Shura vira narrativa sem atributos (E.12.5); acessibilidade desde o início (E.19) |
| **Katana ZERO** (2019) | Um golpe mata; replay da fase ao vencer; recomeço instantâneo | Juice e clareza exemplares | A tela "a morte ensina" com replay (E.17.2) |
| **Hotline Miami** (2012) | Morte e recomeço em menos de 1 s | Fez a morte frequente ser divertida | O recomeço da Jornada depois da morte deve levar menos de 10 s até o Templo |
| **Furi** (2016) | Só lutas de chefe, cada uma ensina um padrão novo | Cult, muito elogiado pelo design de chefes | Chefes como "prova de domínio" e o Ponto de Ruptura como descoberta (E.10) |
| **Hi-Fi Rush** (2023) | Combate sincronizado com a música | Muito elogiado pela sensação | Música em camadas com o Fluxo já existe; anéis de chefe podem cair no compasso (adição opcional, G.9) |

## F.2 Estrutura de roguelite e builds

| Jogo | O que fez | Resultado | Lição aplicada ao Shura |
| --- | --- | --- | --- |
| **Hades** (2020) | Escolha 1 de 3; bênçãos presas a **verbos** (ataque, especial, lançamento, avanço); bênçãos duplas; **Aspectos** de arma; Lembranças pré-run; Espelho com versões excludentes; Pacto de Punição **depois da 1ª vitória**; história que avança a cada morte; **sem inventário** | Referência absoluta do gênero; atraiu até quem não gostava de roguelite | Técnicas por verbo (E.5.2), Aspectos (E.4.2), tsuba como Lembrança (E.4.3), Altar com escolhas excludentes (E.12.3), Juramentos após vitória (E.12.4), Casa de Chá reativa (E.13.1), corte do loot (E.6.1) |
| **Slay the Spire** (2019) | Mapa de rotas com elites opcionais; telemetria de cada escolha desde o protótipo (pick rate, win rate); Ascensão em níveis | Balanceamento guiado por dados é citado como modelo | Mapa mantido e Guardião vira elite opcional (E.11.1); telemetria (WP-01); Juramentos (E.12.4) |
| **Dead Cells** (2018) | Peças feitas à mão combinadas pelo sistema; dificuldade extra (Células de Chefe) após vitória | Grande sucesso de acesso antecipado | Arenas de peças + semente (já é assim); Roda Eterna após vitória (E.11.1) |
| **Hollow Knight** (2017) | Amuletos com custo em entalhes limitados | As restrições criaram builds memoráveis | Espaços limitados de técnica, relíquia, Arte e tsuba (D-16) |
| **Risk of Rain 2** (2020) | Itens empilháveis, coop até 4 | Coop de roguelite de muito sucesso | Coop oficial até 4 (E.15.1) |
| **Curse of the Dead Gods** (2021) | Corrupção que dá maldições escolhendo risco | Elogiado pelo risco e recompensa | Caminho do Asura opcional e relíquias amaldiçoadas (E.2.9, E.6.1) |
| **Returnal** (2021) | Itens com defeito que se cura; runs longas sem suspender no lançamento | O sistema de defeito funcionou; a falta de suspender gerou queixas e foi corrigida em atualização | Relíquias amaldiçoadas (E.6.1); suspender run desde já (E.11.1) |
| **Diablo III** (2012 → 2014) | Lançou com excesso de drops de atributos sem significado; a reforma "Loot 2.0" cortou volume e aumentou a relevância de cada item | A reforma é considerada uma das maiores recuperações do gênero | Corte do loot de atributos (E.6.1) |
| **Rogue Legacy** (2013) | Herdeiros com traços aleatórios, escolha entre 3 | Funcionou; traços que só atrapalham foram vistos como piada, os que mudam estilo como escolhas | Traços de linhagem só de estilo, sempre visíveis (E.12.5) |
| **Balatro** (2024) | Sinergias claras, números grandes e visíveis | Fenômeno de vendas | Cada sinergia (fusão, reação) mostra o número na tela quando acontece |
| **Vampire Survivors** (2022) | Um botão (mover), escolha de upgrades, feedback de cassino | Fenômeno; prova que simplicidade vende | O orçamento de complexidade (C.5) não é "empobrecer": é concentrar |
| **Magic: The Gathering** | "New World Order" (2011): complexidade baixa nas cartas comuns, alta nas raras | Melhorou a entrada de jogadores novos sem perder profundidade | Base simples, complexidade nas técnicas III, fusões e relíquias raras (D-15) |

## F.3 Mundo, IA, sistemas sociais

| Jogo | O que fez | Resultado | Lição aplicada ao Shura |
| --- | --- | --- | --- |
| **Shadow of Mordor / War** (2014/2017) | Nemesis: inimigos que lembram, hierarquia de capitães, promoções | Sistema celebrado; **patenteado nos EUA (2021)**; quase ninguém o replicou desde então | Redesenho como Algozes sem hierarquia (E.9) e consulta jurídica (J.1) |
| **Into the Breach** (2018) | Todo ataque inimigo é telegrafado e manipulável | Referência de "informação perfeita" | Telegrafia total (D-19) e golpes desviados que ferem outros inimigos (E.2.7) |
| **Dark Souls** | Mensagens e manchas de outros jogadores | Funciona por causa da população enorme | Presença assíncrona adiada até haver servidor (E.15.6) |
| **Darkest Dungeon** (2016) | Estresse com aflições | Divisivo: amado pelo tema, odiado por quem sentiu perda de controle | Por isso o Shura é opcional (E.2.9): perda de controle só quando escolhida |
| **Celeste** (2018) | Perdões invisíveis + Modo Assistência explícito | Referência de acessibilidade sem descaracterizar o jogo | Assistência de tempo como única exceção a R5 (E.19) |
| **Super Mario Bros.** / Nintendo | Ensinar sem texto; introduzir, desenvolver, torcer, concluir | Base do design de fases moderno | Entrada de inimigos por ato no padrão de 4 tempos (E.7.1) e Dojo de 8 lições (E.14) |

## F.4 O que falha com frequência em jogos parecidos com o Shura (e o Shura já tem)

1. **Muitos recursos simultâneos no combate** → jogador olha o HUD, não o inimigo. (Fôlego, Shura, selos, leituras.)
2. **Progressão que mexe no tempo de reação** → o jogador não sabe se errou ou se o jogo mudou. (Idade, kamae, Altar, relíquias, Fluxo acelerando a simulação.)
3. **Dois sistemas de recompensa paralelos** (cartas + loot) → a escolha de carta perde peso.
4. **Combinatória sem curadoria** (100 armas) → poucas combinações boas, muitas indiferentes, algumas quebradas.
5. **Run sem fim como modo principal** → sem arco dramático, sem "zerei", difícil de balancear.
6. **Online grande demais para o tamanho da equipe** → bugs de rede que ninguém consegue reproduzir; modos que ninguém joga.
7. **Validação por robôs** → nada quebra, mas ninguém sabe se é divertido.

---

# PARTE G — As poucas adições que valem a pena

Cada adição abaixo passou nos três testes (C.5) e **substitui** algo ou é infraestrutura que torna os cortes seguros.

| ID | Adição | Substitui / justifica | WP |
| --- | --- | --- | --- |
| G.1 | **Feature flags** para todo corte | Torna o redesenho reversível e permite comparar versões no playtest | 00 |
| G.2 | **Telemetria local** em JSONL + relatório | O jogo nunca foi medido com humanos; guia 6.4 e 8.5 | 01 |
| G.3 | **Painel de afinação** em tempo de execução + `Balance.json` | Guia 6.2 e 7.6: afinar jogando | 02 |
| G.4 | **Arte (技)**: um encaixe, uma tecla | Substitui ferramentas, papéis, Kiai, alma, detonar (4 teclas, 2 recursos) | 05 |
| G.5 | **Técnicas por verbo** (4 espaços) | Substitui acúmulo livre; builds legíveis | 09 |
| G.6 | **Aspectos de arma** (8 × 3) | Substitui forja de 100 combinações e duas armas redundantes | 08 |
| G.7 | **Tela "a morte ensina"** com replay de 3 s e erro em quadros | Morte justa e didática (D-18, D-19); usa o determinismo que já existe | 15 |
| G.8 | **Seta de ataque fora da tela** com a cor do anel | Guia 7.3: nunca ser atingido por algo fora da tela sem aviso | 06 |
| G.9 | (Opcional, depois do playtest) **Anéis de chefe no compasso** da trilha | Pilar 3; Hi-Fi Rush. Só se sobrar tempo | — |
| G.10 | **Treino Livre** no Dojo contra qualquer inimigo visto, com "cedo/tarde em N f" | O público de maestria quer praticar; substitui 13 lições | 12 |
| G.11 | **Suspender run** entre salas | Run de 40 min precisa de pausa real (Returnal) | 12 |
| G.12 | **Bot competente** com taxa de parry configurável | Permite medir curvas de dificuldade sem humano | 20 |
| G.13 | **Shader de tinta** unificador | Guia 12.2: une pixel art e interface sumi-e | 16 |
| G.14 | **Salvar imagem do jisei** | Material de comunidade a custo quase zero | 15 |

---

# PARTE H — Pacotes de trabalho

Ordem de execução e marcos de playtest:

```
WP-00 → WP-01 → WP-02                      (infraestrutura: flags, telemetria, afinação)
WP-03 → WP-04 → WP-05 → WP-06 → WP-07      (núcleo do combate)
        ▶ PLAYTEST 1 (núcleo) — diretor decide antes de seguir
WP-08 → WP-09 → WP-10                      (builds)
WP-11 → WP-12 → WP-13 → WP-14              (meta, run, inimigos, Algozes)
        ▶ PLAYTEST 2 (run completa)
WP-15 → WP-16 → WP-17 → WP-18 → WP-19      (telas, arte, áudio/acessibilidade, localização, online)
WP-20                                      (balanceamento por simulação + limpeza)
        ▶ PLAYTEST 3 (candidata a beta)
```

Convenção de flags: classe estática `Feature` em `Config/Feature.cs`, valores carregados de `features.json` (StreamingAssets) com padrão no código; flags visíveis numa aba "Desenvolvedor" do painel do WP-02; nome em PascalCase; o **padrão é sempre o novo design**.

---

## WP-00 — Feature flags e linha de base

**Objetivo:** tornar cada mudança reversível e registrar como o jogo está hoje.

1. Criar `Feature.cs` + `features.json` + leitura no boot (antes de `Game`).
2. Criar as flags (todas usadas nos WPs seguintes), cada uma com comentário dizendo o WP e o padrão:
   `Stamina=false`, `LocalWounds=false`, `KiSealsBase=false`, `KamaeSwitch=false`, `CursedRing=false`, `DoubleRingType=false`, `FlowTimeScale=false`, `ShuraMeterBase=false`, `LegacyToolsAndKeys=false`, `NamedLinks45=false`, `StatLoot=false`, `ItemClans=false`, `Blessings=false`, `ForgeHandles=false`, `ExtraWeapons=false` (Kanabō, Wakizashi como armas), `AgeStats=false`, `SenseiStats=false`, `ParryWindowModifiers=false`, `RealtimeDayClock=false`, `StormWeather=false`, `InfiniteRunDefault=false`, `ExtraEnemies=false`, `AiReadsExtended=false`, `RivalHierarchy=false`, `BrowserClient=false`, `PartyMode8=false`, `BloodPact=false`, `AsyncPresence=false`, `PvpRanked=false`, `LegacyModes=false` (Torre, Mestre, Peregrinação), `LegacyTemple=false` (8 construções), `LegacyCurrencies=false`, `ThirdWeapon=true` (Segunda Troca do Altar, em observação).
3. Rodar os 4 bots de referência (sementes 3, 11, 29, 41) na v0.15.0 e salvar em `docs/baseline/`: tempo por sala, profundidade alcançada, causa de morte, uso de teclas pelo bot.
4. Tag git `v0.15.0-baseline`.

**Aceite:** jogo idêntico à v0.15.0 com todas as flags em `true` (teste automático que liga tudo e roda um bot); 197 testes passando.

## WP-01 — Telemetria local

**Objetivo:** medir humanos (guia 6.4.7; Slay the Spire).

1. `TelemetryService` grava um arquivo `.jsonl` por run em `Application.persistentDataPath/telemetry/` (aviso ao jogador na primeira execução, opção de desligar).
2. Eventos (todos com `t` em quadros de simulação, `runId`, `seed`, `depth`, `act`, `room`):
   - `ring_resolved`: tipo de anel, inimigo, resposta usada (parry, block, dodge, swap, hit), **delta em quadros do fechamento** (negativo = cedo), sucesso.
   - `player_hit`: fonte, dano, tipo de anel, se o anel estava visível na tela.
   - `death`: fonte, sala, tempo de run, build resumida.
   - `reward_offered` e `reward_picked`: ids das 3 opções e a escolhida.
   - `route_choice`: portas oferecidas e escolhida.
   - `input_use`: contagem por ação a cada sala.
   - `room_end`: duração, dano recebido, curas, finalizações por tipo (rápida/dirigida e nível).
   - `run_end`: resultado, duração, Algoz criado.
3. Ferramenta `Tools/telemetry_report` (script Python ou C# de editor) que lê uma pasta de `.jsonl` e gera um relatório Markdown/CSV com: histograma de delta de parry por tipo de anel, morte por inimigo normalizada por exposição, pick rate e win rate por recompensa, duração das salas e das runs, uso de teclas.

**Aceite:** um bot de 20 salas gera um arquivo válido; o relatório roda e produz todas as seções; telemetria desligada não grava nada.

## WP-02 — Painel de afinação e `Balance.json`

**Objetivo:** afinar jogando (guia 7.6).

1. Mover para `Balance.json` (carregado por `GameConfig`) todos os números de sensação e balanceamento citados neste documento: janela de parry, anti-spam, buffers, anel (36 f, 4 f), hit-stop por evento, tremor, câmera lenta, ganho de Fluxo, cura da cabaça, postura por evento, vida/dano/postura por inimigo e escala por ato.
2. Painel em jogo (F1 em build de desenvolvimento): editar valores em tempo real, salvar e recarregar `Balance.json`, aba de flags, mostrar hitboxes, pular de sala, escolher inimigo.

**Aceite:** alterar a janela de parry no painel muda o jogo no próximo golpe; salvar e reabrir o jogo mantém o valor; o painel não existe em build release.

## WP-03 — Núcleo do combate: cortar e fundir

**Objetivo:** aplicar E.1 e E.2 no núcleo.

1. **Fôlego:** com `Stamina=false`, ataque e esquiva sem custo; remover exaustão e Brilho do Respiro; converter efeitos (tabela de E.2.2).
2. **Janela constante:** com `ParryWindowModifiers=false`, ignorar todo modificador de janela vindo de kamae, idade, Altar, relíquias, tsubas, itens e exaustão. Criar teste: "nenhuma combinação de build altera `ParryWindowFrames`" (varre todos os modificadores registrados).
3. **Fluxo sem acelerar a simulação:** com `FlowTimeScale=false`, ritmo fixo em 1,0; aceleração só nas recuperações do jogador (6% por nível) e nos efeitos.
4. **Ferimentos:** com `LocalWounds=false`, remover do jogador, bandagem e tecla H; manter só o desarme do Mikiri no inimigo.
5. **Kamae:** com `KamaeSwitch=false`, guarda fixa por arma (tabela de E.2.5); remover teclas 1–4 e direcional de guarda.
6. **Selos:** com `KiSealsBase=false`, sem selos na base; criar a técnica "Selo Interno" (detonação automática ao 5º selo) no inimigo indicado pela matriz (padrão: Onmyōji).
7. **Mikiri:** fundir contra-investida e Pisão numa resposta só (E.1.5); Descida e Arremesso mantidos; todas contam para o selo dourado.
8. Atualizar os testes afetados.

**Aceite:** bots sem exceção; testes passando; captura do HUD mostrando que fôlego e ferimentos sumiram; teste de janela constante verde.

## WP-04 — Anéis e Troca Perfeita como terceira resposta

1. Quatro tipos de anel (tabela de E.2.4) com forma, cor e um id de som (o som em si entra no WP-17; aqui, um placeholder distinto por tipo).
2. Roxo = Troca Perfeita; aparar no roxo atravessa a lâmina; esquiva evita sem ganho. Remover a exceção do Talismã.
3. `CursedRing=false` (verde), `DoubleRingType=false` (duplo vira dois anéis).
4. Branco partido só quando há 2+ jogadores.
5. Elos: com `NamedLinks45=false`, 10 comportamentos (um por arma que entra); os 45 nomes viram texto.
6. Distribuição de anéis por ato (ciano e vermelho no Ato I; roxo com o primeiro yokai espiritual; finta a partir do Ato II) aplicada nas definições dos inimigos.
7. Remover a leitura TROCA da IA (adiantado do WP-13 porque pune o gancho).

**Aceite:** capturas lado a lado dos 4 anéis em tamanho real e em modo daltônico; teste de que nenhum inimigo do Ato I usa roxo ou finta.

## WP-05 — Arte (技) e o novo esquema de 7 teclas

1. `ArtSystem`: 1 encaixe, 2 cargas, recarga de 1 carga por finalização; escolha no Arsenal; troca por carta.
2. Artes da 1.0: Kiai, Shuriken, Fumaça, Sino, Corrente, Ruptura, Alma (E.2.10). Petardo e óleo: atrás de `LegacyToolsAndKeys`.
3. Remover papéis espirituais (convertidos em cargas de Arte).
4. Novo mapeamento (E.17.3) em teclado e controle; remapeamento atualizado; ícones Xbox/PlayStation.
5. Kiai em grupo com 2 jogadores.
6. Dojo: atualizar as lições que ensinavam teclas removidas.

**Aceite:** telemetria `input_use` de uma run de bot mostra só as 7 ações + movimento; remapeamento salva e carrega; nenhuma string de UI cita G, V, B, H, T ou 1–4.

## WP-06 — Sensação: hierarquia de impacto, finalização, Iai, Mushin, ataques fora da tela

1. **Tabela única de impacto** (valores iniciais em `Balance.json`; ajustar no painel):

| Evento | Hit-stop | Tremor | Câmera lenta | Extra |
| --- | --- | --- | --- | --- |
| Golpe comum acerta | 2 f | leve, direcional | — | flash 1 f no alvo |
| Bloqueio | 3 f | leve | — | faísca curta |
| Parry perfeito | 6 f | médio | — | faíscas na cor da arma, "tim" |
| Troca Perfeita | 8 f | médio | — | rastro de tinta 4 f |
| Resposta de mestre (Mikiri, Descida, Arremesso) | 8 f | médio | — | kanji dourado curto |
| Quebra de postura (comum) | 10 f | forte | **não** (era 0,25 s) | kanji 破 |
| Finalização rápida | 6 f | médio | — | — |
| Finalização dirigida | 12 f | forte | durante a mira | linha dourada |
| Último inimigo da sala | 14 f | forte | 0,9 s | — |
| Quebra de postura de chefe / Ponto de Ruptura | 16 f | máximo | 0,6 s | — |
| Tomar dano | 4 f | forte, aleatório | — | borda da tela manchada |

2. **Finalização:** toque = rápida; segurar = dirigida; dirigida por padrão em elites, Guardiões, último inimigo e chefes; recompensa do perfeito ajustada (E.1.7).
3. **Iai:** punição muda para "o líder ataca primeiro"; frequência por saco embaralhado (máx. 1 a cada 4 salas de combate).
4. **Mushin:** HUD some, anéis, posturas e cargas de esquiva continuam.
5. **Seta fora da tela:** quando um inimigo fora da tela inicia um anel contra você, seta na borda com a cor e a forma do anel.
6. **Feedback de anti-spam** do parry (lâmina cinza + "tic").
7. Feedback de posicionamento (背, 壁, 倒).

**Aceite:** gravação de 60 s de combate revisada quadro a quadro: o feedback chega no mesmo quadro do impacto; nenhum evento comum usa câmera lenta.

## WP-07 — Shura opcional e Karma visível

1. `ShuraMeterBase=false`; o medidor nasce com Máscara Hannya, Lâmina de Muramasa ou Juramento Caminho do Asura.
2. Lua cheia só sorteia com o Caminho ativo.
3. Kanji de Karma (義/修) no samurai a cada ação de Karma, com som; linha de Karma na tela de morte.

**Aceite:** run sem Caminho não mostra o medidor; run com Máscara mostra; Asura Interior continua funcionando.

### ▶ PLAYTEST 1 — Núcleo (ver Parte I.5). O diretor decide antes do WP-08.

## WP-08 — Armas, Aspectos, tsuba, maestria

1. 8 armas; Kanabō e Wakizashi + Kunai viram Aspectos (`ExtraWeapons=false`).
2. 24 Aspectos (tabela de E.4.2), cada um com uma frase de regra; reaproveitar golpes de troca e finalizações das empunhaduras.
3. `ForgeHandles=false`: a Forja vira Arsenal (arma, aspecto, tsuba, Arte).
4. Tsuba: 1 por run; revisar as 15; nenhuma mexe em janela ou tempo de anel.
5. Maestria: I rastro, II Aspecto II, III segunda finalização, IV Aspecto III, V Ougi; remover bônus permanentes de postura e golpe de troca.
6. Teste de distinção: cada aspecto tem pelo menos um campo de regra diferente do Aspecto I da mesma arma.

**Aceite:** tela do Arsenal com 8 armas e aspectos bloqueados/desbloqueados; bots usando cada aspecto sem exceção.

## WP-09 — Técnicas por verbo, marcas unificadas, fusões

1. `TechniqueDef.Slot` ∈ {Aparar, Esquivar, Trocar, Finalizar, Passiva}; classificar as técnicas pelo gancho principal (gerar tabela em `docs/08-Tecnicas.md` para revisão do diretor).
2. Carta de técnica para espaço ocupado oferece substituir (a antiga vira Ecos).
3. Marcas: 8 (4 elementais, 4 de estilo; 氷 Gelo no lugar de 風 Vento); elementos passam a vir só das marcas elementais.
4. 6 reações (E.5.4); Sombra e Vento deixam de ser elementos; Ressonância sai.
5. Alma vira Arte (oferta ao chegar ao nível III).
6. Revisar as 8 fusões para caberem numa frase e não tocarem em tempo.

**Aceite:** cartão de pausa mostra os 4 espaços; teste de que nenhuma técnica tem `Slot` vazio; teste das 6 reações.

## WP-10 — Fim do loot de atributos; relíquias; bênçãos; RNG protegido

1. `StatLoot=false`, `ItemClans=false`, `Blessings=false`.
2. Afixos únicos dos Lendários → relíquias; corrompidos → relíquias amaldiçoadas.
3. Raridade de relíquia: Comum, Rara, Lendária, Shura; 4 espaços (5 com Altar).
4. Inimigos comuns deixam só Mon; Guardião/elite deixa escolha de relíquia.
5. Carta de respiro: Cura +40% **ou** +1 gole máximo.
6. **Proteções de aleatoriedade** (D-21): saco embaralhado para tipo de recompensa por ato; *pity timer* de técnica nova (no máx. 3 salas sem oferecer técnica de inimigo absorvível); *streak breaker* para eventos repetidos.
7. Remover tela de Equipamento e mochila (UI do WP-15 pode vir depois; aqui, só esconder).

**Aceite:** run inteira sem nenhum item de atributo; telemetria `reward_offered` mostra a distribuição esperada em 100 runs simuladas.

## WP-11 — Meta: moedas, save v3, Altar, Juramentos, Linhagem, Templo

1. `MetaProgress` v3; `Sanitize` migra Aço Celeste (×5), Fragmentos (×10) e itens guardados para Ecos; teste de migração com um save v2 real do diretor.
2. Templo com 6 lugares (E.12.1); Biblioteca com abas.
3. Altar: Olhar Afiado, Fôlego e Corpo de Diamante redesenhados (E.12.3); cada nó com duas versões excludentes.
4. Juramentos liberados após a 1ª vitória; Anel Silencioso no máximo = antigo Modo Mestre; Algoz Eterno; Caminho do Asura.
5. Linhagem: `AgeStats=false`, `SenseiStats=false`; traço único de estilo, escolhido entre 3; Sensei desbloqueia lição e ornamento.

**Aceite:** save v2 abre sem perda de Ecos; Altar cheio não altera a janela de parry (teste); Juramentos bloqueados antes da vitória.

## WP-12 — Estrutura da run: Jornada em 4 atos, Roda Eterna, modos, suspender

1. `InfiniteRunDefault=false`: Jornada com a tabela de E.11.1; Senhores fecham os atos; Guardião vira nó de elite opcional.
2. Biomas da 1.0 e variantes (E.11.2); `RealtimeDayClock=false` (hora por ato); clima 4 (`StormWeather=false`).
3. Saco embaralhado de tipos de sala por ato; garantia de Santuário ou Mercador antes de cada Senhor.
4. Roda Eterna após a vitória, com Kalpas e Yomi.
5. Suspender run entre salas (salvar e continuar).
6. Modos: Jornada, Desafio Diário, Dojo (8 lições + Treino Livre), Duelo entre amigos; `LegacyModes=false`.
7. Entrada de inimigos novos por ato (3 a 5 por ato).

**Aceite:** 50 runs de bot competente (WP-20 pode adiantar um protótipo) chegam ao Ato IV com duração média por sala registrada; todas as sementes geram rotas válidas; suspender e continuar reproduz a mesma sala.

## WP-13 — Inimigos: matriz, cortes, facções visíveis, IA de 5 leituras

1. Gerar `docs/06-Matriz-Inimigos.md` automaticamente.
2. Aplicar fusões (E.7.1); `ExtraEnemies=false` para os que saem; migrar técnicas boas.
3. Ícones de facção, kanji 争, fio de luz da Xamã.
4. `AiReadsExtended=false` (FLANCO, FERRAMENTA, CURA fora; TROCA já saiu no WP-04); leituras só aparecem no 3.
5. Afixos de elite e formações a partir do Ato II.

**Aceite:** matriz sem dois inimigos ativos com mesma resposta e mesma família de mecânica (ou justificativa escrita); silhuetas exportadas em 64 px.

## WP-14 — Algozes (substitui Rivais)

1. `RivalHierarchy=false`: sem Capitães, Generais, estrelas, guarda-costas, promoção ou reação entre rivais.
2. Algoz: nome, parte dos Ecos, **uma** coisa roubada, cicatriz visual; máx. 3 ativos; aparece uma vez num Covil do Algoz.
3. Renomear textos e a aba da Biblioteca.
4. Rivais emprestados de amigos: desligado (depende de `AsyncPresence`).

**Aceite:** nenhuma string ou classe pública com "Nemesis", "Captain", "General" no código ativo; teste do ciclo morte → Algoz → Covil → vingança.

### ▶ PLAYTEST 2 — Run completa (Parte I.5).

## WP-15 — HUD, telas, "a morte ensina", narrativa reativa

1. HUD conforme E.17.1; regra dos 60% centrais.
2. Helper de layout em `UiKit` (medir e quebrar texto) e aplicação nas telas tocadas.
3. Tela "a morte ensina": replay de 3 s a partir de um buffer circular de estados/entradas da simulação; anel fatal destacado; erro em quadros; Algoz; jisei; botão "Salvar imagem".
4. Casa de Chá: sistema de prioridade de falas reativas (causa da morte, ato, arma, primeira vitória contra chefe, Caminho do Asura); placeholders marcados `[ESCREVER]` onde faltar texto, para o diretor completar.

**Aceite:** capturas 1920×1080 e 1280×720 sem texto sobreposto; replay da morte idêntico em 3 execuções com a mesma semente.

## WP-16 — Arte e legibilidade

1. Pós-processamento "tinta" (contorno, paleta por bioma, textura de papel), com opção de desligar.
2. `docs/07-Guia-de-Estilo.md` (uma página): paleta, regras de cor de ameaça e de jogador.
3. Cores de jogador no coop; opacidade de efeitos dos aliados.
4. Silhuetas distintas para os inimigos da 1.0 (ajustes de escala, cor e acessório no código de arte).
5. Folhas de tiles com coordenadas nomeadas e teste que valida cada recorte.
6. Lista de arte própria necessária para os 6 chefes (entregar ao diretor como pedido de produção).

**Aceite:** captura com e sem shader; teste dos recortes verde.

## WP-17 — Áudio e acessibilidade

1. Som por tipo de anel, direcional; assinaturas de resposta de mestre, Troca Perfeita e quebra de postura.
2. Opções de E.19 (legendas de som, alto contraste, Assistência de tempo, tremor e flashes, segurar ou alternar, tamanho de texto, prévia dos anéis).
3. Garantir que a Assistência de tempo é a **única** coisa que altera janela e velocidade (teste).

**Aceite:** um jogador consegue jogar o Ato I só com áudio + legenda de som desligando a exibição dos anéis (teste manual do diretor); teste da Assistência verde.

## WP-18 — Localização

1. Extrair `Strings` para tabelas (`Localization/pt-BR.json`, `en.json`) com chaves estáveis.
2. Seletor de idioma nas opções; fallback para pt-BR.
3. Inglês: tradução inicial das telas principais (marcar o resto como pendente).

**Aceite:** nenhum literal de texto visível em `GameUI` (verificação por script); troca de idioma em tempo real.

## WP-19 — Online enxuto

1. `Net.MaxPlayers=4`; `PartyMode8` em Opções avançadas.
2. `BrowserClient=false`; remover do fluxo principal.
3. `BloodPact=false`, `AsyncPresence=false`, `PvpRanked=false`.
4. Coordenador com um token de ataque por jogador (máx. 3).
5. Anéis de inimigos que miram aliados menores e semitransparentes.
6. Teste de desempenho: 4 jogadores (anfitrião + 3 convidados-robô) + 12 inimigos; registrar fps e banda.

**Aceite:** sessão de 4 robôs por 20 salas sem dessincronia nem exceção; relatório de desempenho.

## WP-20 — Balanceamento por simulação e limpeza

1. **Bot competente:** parâmetros `parryAccuracy` (0,5 / 0,7 / 0,9), `reactionDelayFrames`, `usesMasterResponses`. 200 runs por perfil.
2. Gerar curvas: taxa de vitória por perfil, sala de morte, tempo por sala; comparar com os alvos da Parte I.3 e ajustar `Balance.json`.
3. **Limpeza:** para cada flag cujo corte o diretor confirmou nos playtests, remover o código antigo, a flag e os testes associados. Flags não confirmadas ficam.
4. Atualizar `docs/02-GDD.md` e `05-Expansao.md` para refletir o novo design.

**Aceite:** perfil 0,7 vence entre 10% e 30% das Jornadas sem Altar; perfil 0,9 vence mais de 60%; nenhum inimigo com letalidade por exposição acima de 2× a média do seu ato; build v0.20.0.

### ▶ PLAYTEST 3 — Candidata a beta.

---

# PARTE I — Telemetria, balanceamento e protocolo de playtest

## I.1 Perguntas que a telemetria precisa responder

| Pergunta | Métrica | Sinal de problema |
| --- | --- | --- |
| O anel está bem calibrado? | Histograma de delta (quadros) das respostas por tipo de anel | > 60% dos erros do mesmo lado (todos cedo ou todos tarde) em vários jogadores = anel ou som mal sincronizados |
| Algum inimigo é injusto? | Mortes por inimigo ÷ salas em que apareceu | Letalidade > 2× a média do ato |
| As mortes são "culpa do jogador"? | `player_hit` com anel visível × sem anel visível | Qualquer dano relevante sem anel visível é bug de telegrafia |
| Existe estratégia dominante? | Proporção de bloqueio, esquiva e parry nas respostas a ciano | Bloqueio > 35% em jogadores com 5+ runs |
| As recompensas são escolhas reais? | Pick rate e presença em vitórias por técnica, relíquia, aspecto | Pick < 5% (ignorada) ou presença em > 60% das vitórias (obrigatória) |
| A Troca Perfeita está sendo usada? | Trocas perfeitas por sala e taxa de sucesso no anel roxo | < 1 por sala depois do Ato I = gancho invisível ou difícil demais |
| O ritmo está bom? | Duração de sala, de ato e de run | Fora das faixas de I.3 |
| O que ninguém usa? | `input_use` por ação | Ação com < 2% de uso depois de 3 runs = candidata a corte |
| Onde as pessoas desistem? | Funil: dojo → 1ª sala → 1º Senhor → Ato II → Ashura | Queda > 40% num único degrau |

## I.2 Funil de onboarding (guia 10.4)

Marcos a registrar com tempo desde o primeiro boot: primeira lição concluída, primeiro parry perfeito, primeira Troca Perfeita, primeira técnica absorvida, primeira morte, primeiro Senhor derrotado, primeira vitória.

Hipóteses iniciais (validar no Playtest 1): primeiro parry perfeito em < 2 min; primeira Troca Perfeita em < 10 min; primeira morte entre 5 e 15 min; primeiro Senhor derrotado na 2ª ou 3ª run.

## I.3 Tempos-alvo e planilha (guia 8.5)

Valores iniciais. **São hipóteses** para a planilha; o playtest e o bot competente ajustam.

| Encontro | Duração-alvo | Parries perfeitos para quebrar a postura | Observação |
| --- | --- | --- | --- |
| Inimigo comum | 8–15 s | 3–4 | sem parry, morre por dano em ~2× o tempo |
| Elite | 30–45 s | 6–8 | |
| Guardião / nó de elite | 45–90 s | 8–10 | |
| Senhor do Bioma (por fase) | 40–80 s | 10–14 por barra | 3 fases = 2–4 min |
| Ashura | 4–6 min | — | |
| Sala de combate (2–3 ondas) | 60–120 s | | |
| Ato | 9–13 min | | |
| Jornada vitoriosa | 35–45 min | | |
| Morte típica no Ato I (iniciante) | 6–12 min | | morte cedo deve recomeçar rápido |

**Método de planilha** (`docs/09-Balanceamento.xlsx` ou CSV, gerado e lido pela ferramenta do WP-01):

- `DPS_jogador(ato) = dano_médio_por_golpe × golpes_por_segundo × (1 + 0,12 × Fluxo_médio) × multiplicadores_médios_de_build(ato)`
- `Vida_inimigo(tipo, ato) = DPS_jogador(ato) × tempo_alvo(tipo) × fração_do_dano_vindo_de_golpes` (o resto vem da finalização após quebra)
- `Postura_inimigo(tipo, ato) = postura_por_parry × parries_alvo(tipo)`
- Verificar **breakpoints** (D-20): para cada arma, quantos golpes matam um inimigo comum de cada ato; evitar que um ajuste de 1 ponto mude esse número sem querer.
- Escala entre atos: **no máximo +25% de números por ato**; o resto da dificuldade vem de regras (formações, afixos, fintas, roxo, Kalpas).

## I.4 Bot competente (WP-20)

Perfis: `parryAccuracy` 0,5 (iniciante), 0,7 (médio), 0,9 (veterano); `reactionDelayFrames` 12 / 8 / 5; `usesMasterResponses` não / às vezes / sim. Rodar 200 Jornadas por perfil, com e sem Altar cheio.

Metas de calibração (hipóteses):

| Perfil | Vitória sem Altar | Vitória com Altar cheio |
| --- | --- | --- |
| 0,5 | < 5% | 5–15% |
| 0,7 | 10–30% | 25–45% |
| 0,9 | > 60% | > 75% |

Se o Altar cheio dobrar a taxa de vitória de qualquer perfil, a meta-progressão está dando poder demais (D-17).

## I.5 Protocolo de playtest humano (guia 6.4)

**Quem:** 5 pessoas por rodada que nunca jogaram o Shura, com perfil do gênero (já terminaram pelo menos um entre Sekiro, Hades, Dead Cells, Hollow Knight, Nine Sols). Nada de amigos que jogam por educação; pelo menos 2 desconhecidos por rodada.

**Como:**

1. Build com telemetria ligada e gravação de tela + voz (OBS).
2. Nenhuma explicação. Só: "Pense em voz alta enquanto joga."
3. 60 minutos de jogo; o observador anota em silêncio (tabela abaixo).
4. 10 minutos de entrevista com perguntas abertas.

**Ficha do observador (uma linha por ocorrência):**

| Minuto | O que aconteceu | Tipo (hesitou / frustrou / sorriu / perguntou "e agora?" / ignorou sistema) | Sistema envolvido |
| --- | --- | --- | --- |

**Perguntas da entrevista:**

1. Como você explicaria esse jogo para um amigo em uma frase?
2. Qual foi o melhor momento? E o pior?
3. Por que você morreu da última vez?
4. Teve algo na tela que você não entendeu?
5. Teve alguma coisa que você sabia que existia mas não usou? Por quê?
6. Você jogaria mais uma run agora? (sim/não, sem justificar)

**O que cada rodada decide:**

| Rodada | Depois de | O diretor decide |
| --- | --- | --- |
| Playtest 1 | WP-07 | Fôlego e kamae podem ser removidos de vez? O anel roxo/Troca Perfeita é entendido? A finalização dupla (toque/segurar) funciona? A velocidade fixa é melhor que a variável (testar as duas com a flag)? |
| Playtest 2 | WP-14 | A Jornada de 4 atos tem a duração certa? Técnicas por verbo são legíveis? Alguém sente falta do loot de atributos? Quais inimigos confundem? |
| Playtest 3 | WP-20 | Está pronto para beta? Lista final de cortes a limpar. |

**Regra de interpretação (guia 6.4.6):** jogadores apontam bem onde dói e mal o remédio. Registre o problema; a solução sai deste documento ou de uma nova análise.

---

# PARTE J — Decisões reservadas ao diretor, riscos e glossário

## J.1 Decisões que a IA **não** toma sozinha

A IA implementa o **padrão recomendado** atrás de flag e reporta. O diretor confirma ou reverte.

| # | Decisão | Padrão recomendado | Por quê |
| --- | --- | --- | --- |
| 1 | Sistema de rivais e a patente do Nemesis | Algozes sem hierarquia (E.9) **e consulta a um advogado** antes de publicar na Steam | Risco jurídico nos EUA; não sou advogado |
| 2 | Cortar o Fôlego de vez | Cortar | Sekiro; R3, R5 |
| 3 | Fim do loot de atributos | Cortar | Duas economias de recompensa; R7 |
| 4 | Run com fim (Jornada) como modo principal | Sim, infinita vira Roda Eterna | Arco dramático, guia 9.3 |
| 5 | Coop oficial até 4 | Sim, 5–8 experimental | Legibilidade, teste, mercado |
| 6 | Congelar o cliente do navegador | Sim | Custo dobrado, já defasado |
| 7 | PvP sem ranking | Sim | Ranking local falsificável |
| 8 | Kanabō e Wakizashi como aspectos | Sim | Sobreposição com Montante e Lâminas Gêmeas |
| 9 | Lista final de inimigos (24–28) | Pela matriz + Playtest 2 | Silhuetas e respostas distintas |
| 10 | AppID próprio na Steam | Necessário antes de qualquer teste público | AppID 480 é só de teste |
| 11 | Velocidade de simulação fixa | Sim (comparar no Playtest 1) | R5 |
| 12 | Volume de texto da Casa de Chá | O diretor escreve ou aprova cada fala | Voz e tom do jogo |
| 13 | Arte dos chefes (freelancer, IA, própria) | Decisão de orçamento; registrar licenças | Guia 12.3 |

## J.2 Riscos do próprio redesenho

| Risco | Mitigação |
| --- | --- |
| Cortar demais e o jogo ficar raso | Tudo atrás de flags; profundidade migra para builds (técnicas III, fusões, aspectos, relíquias), não some; Playtest 1 compara |
| Quebrar saves e testes | `MetaProgress` v3 com migração testada; WP-00 garante a linha de base |
| A IA "aproveitar" e reescrever sistemas que estão bons | Regra A.2.8 e vereditos MANTER explícitos |
| Perder o que dá identidade (Shura, linhagem, rituais) | Nada temático foi cortado: Shura vira caminho escolhido, linhagem vira narrativa, rituais ficam |
| O diretor sentir falta de algo cortado | Lista de flags no painel; ligar e jogar de novo é imediato |
| Balanceamento por robô enganar | Bot competente só calibra; a decisão final vem do playtest humano |

## J.3 Glossário dos termos novos

| Termo | Significado |
| --- | --- |
| **Arte (技)** | O encaixe único de habilidade ativa (Kiai, Shuriken, Fumaça, Sino, Corrente, Ruptura, Alma), 2 cargas, recarga por finalização |
| **Aspecto** | Variação de uma arma com uma regra diferente; 3 por arma, liberados pela maestria |
| **Algoz (仇)** | O inimigo que te matou e volta uma vez, com algo seu; substitui Rivais |
| **Mikiri** | Resposta de mestre à estocada (funde contra-investida e Pisão) |
| **Técnica de Verbo** | Técnica que ocupa um dos 4 espaços: Aparar, Esquivar, Trocar, Finalizar |
| **Jornada** | Run de 4 atos com fim no Ashura |
| **Roda Eterna** | Continuação infinita depois da vitória, com Kalpas e Yomi |
| **Caminho do Asura** | Ativação opcional do medidor de Shura |
| **Orçamento de Complexidade** | Os limites R1–R10 da Parte C.5 |
| **Assistência de tempo** | Opção de acessibilidade, a única que altera janela e velocidade |
| **Lembrança** | A tsuba escolhida antes da run |

---

# PARTE K — Fontes

**Base do projeto:** *Shura — Relatório Completo do Jogo v0.15.0* (14 nov 2026); *Guia Completo: Como Criar um Bom Jogo que Vende na Steam* (14 nov 2026), Partes 1 a 12 e 14 (a 13, de marketing, ficou fora por pedido do diretor) e o Apêndice A.

**Teoria de design citada:**

- Hunicke, LeBlanc e Zubek, *MDA: A Formal Approach to Game Design and Game Research* (2004).
- Raph Koster, *A Theory of Fun for Game Design* (2004).
- Jesse Schell, *The Art of Game Design: A Book of Lenses* (3ª ed., 2019).
- Steve Swink, *Game Feel* (2008).
- Tynan Sylvester, *Designing Games* (2013) — elegância.
- Celia Hodent, *The Gamer's Brain* (2017) — atenção, memória de trabalho, carga cognitiva.
- Nelson Cowan, "The magical number 4 in short-term memory" (2001); George Miller, "The magical number seven, plus or minus two" (1956).
- Mihaly Csikszentmihalyi, *Flow* (1990); modelo de 8 canais (Massimini, Csikszentmihalyi e Carli, 1987).
- Deci e Ryan (Teoria da Autodeterminação); Rigby e Ryan, *Glued to Games* (2011).
- Daniel Cook, "The Chemistry of Game Design" (2007) — átomos de habilidade.
- Soren Johnson, "Water Finds a Crack" (2010) — jogadores otimizam a diversão para fora do jogo.
- Mark Rosewater, "New World Order" (Magic: The Gathering, 2011).
- Jakob Nielsen, "Why You Only Need to Test with 5 Users" (2000).
- Lei de Hick-Hyman (tempo de decisão).

**Palestras e postmortems:** Sid Meier, *Interesting Decisions* (GDC 2012); Martin Jonasson e Petri Purho, *Juice it or lose it* (2012); Jan Willem Nijman, *The Art of Screenshake* (2013); Greg Kasavin sobre a narrativa de Hades (GDC 2021); postmortem de design de Into the Breach (GDC 2019); dados de balanceamento de Slay the Spire (Game Developer, 2018); Game Maker's Toolkit (Mark Brown) sobre game feel e design de fases da Nintendo.

**Jogos analisados:** Sekiro, Nine Sols, Windblown, Ghost of Tsushima, For Honor, Absolver, Thymesia, Sifu, Katana ZERO, Hotline Miami, Furi, Hi-Fi Rush, Hades, Slay the Spire, Dead Cells, Hollow Knight, Risk of Rain 2, Ravenswatch, Curse of the Dead Gods, Returnal, Diablo III, Rogue Legacy, Balatro, Vampire Survivors, Magic: The Gathering, Shadow of Mordor/War, Into the Breach, Dark Souls, Darkest Dungeon, Celeste, Super Mario Bros.

**Nota de método:** a busca na web estava desativada nesta organização durante a análise. Fatos sobre jogos e literatura vêm de conhecimento consolidado até meados de 2026 e do próprio guia; números de balanceamento são hipóteses de partida, não dados medidos. A questão da patente deve ser confirmada com um advogado.

---

*Fim do documento. Comece pelo WP-00.*
