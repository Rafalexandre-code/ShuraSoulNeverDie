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

## Fase 2 — Estrutura da run e mundo (§6) — ⏳

| § | Item | Estado |
|---|---|---|
| 6.1 | Mapa de rotas | ⏳ |
| 6.2 | Tipos de sala (duelo, desafio, disputa, santuário, mercador, ferreiro, evento, tesouro maldito, covil do rival) | ⏳ |
| 6.3 | Biomas com regras próprias + 4 biomas novos | ⏳ |
| 6.4 | Hora do dia e clima | ⏳ |
| 6.5 | Arenas destrutíveis | ⏳ |
| 6.6 | Kalpas | ⏳ |
| 6.7 | Eventos narrativos | ⏳ |

## Fase 3 — Armas, técnicas e builds (§4) — ⏳

| § | Item | Estado |
|---|---|---|
| 4.1 | Forja modular | ⏳ |
| 4.2 | Quatro armas novas (Tessen, Kanabō, Shakujō, Wakizashi + Kunai) | ⏳ |
| 4.3 | Maestria de arma | ⏳ |
| 4.4 | Técnicas com níveis, ramos, marcas e 8 fusões | ⏳ |
| 4.5 | Transformação em alma | ⏳ |
| 4.6 | Itens com identidade (afixos únicos, sets de clã, corrompidos, forja de itens) | ⏳ |
| 4.7 | Reações elementais | ⏳ |
| 4.8 | 8 relíquias novas | ⏳ |
| 4.9 | Editor de kata | ⏳ (provável adiamento: exige UI de edição de combos) |
| 4.10 | Arquétipos de build | ⏳ (decorre de 4.1–4.8) |

## Fase 4 — Progressão, meta e narrativa (§7, §8) — ⏳

| § | Item | Estado |
|---|---|---|
| 7.1 | Templo em Ruínas (hub) | ⏳ |
| 7.2 | Moedas (Mon, Aço Celeste, Fragmentos, Pétalas) | ⏳ |
| 7.3 | Altar em árvore | ⏳ |
| 7.4 | Juramentos | ⏳ (os botões de regra já existem: regen de fôlego, ganho de Shura, velocidade de adaptação, afixos extras) |
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

## Apresentação e acessibilidade (§10) — ⏳

10.1 tabela de impacto · 10.2 câmera e finalizações em tinta · 10.3 som que informa · 10.4 música adaptativa (🟡 música abafada em Mushin/Iai já feita) · 10.5 interface diegética · 10.6 luz 2D (provável adiamento: o projeto usa Built-in RP; luz 2D exige URP) · 10.7 acessibilidade (🟡 anéis com padrão próprio por tipo) · 10.8 replay e foto.
