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
- **Parry perfeito:** primeiros 7 f após apertar. Causa dano de postura no atacante, zero
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

## 3. Sistemas
- **Técnicas Absorvidas:** cada inimigo tem um golpe-assinatura e uma *Resposta Perfeita*.
  Executar a resposta marca o inimigo com o selo dourado; finalizá-lo absorve a técnica,
  que aparece garantida na próxima escolha de recompensa.
- **Fluxo (0–3):** pontos por ações perfeitas (parry perfeito +1, esquiva perfeita +1,
  contra-investida +2, finalizador +2). Níveis em 3/7/12 pontos. Levar dano derruba
  um nível. Efeitos: N1 +15% dano de postura; N2 esquiva recarrega 30% mais rápido;
  N3 finalizadores curam 10. Cada nível acrescenta uma camada musical.
- **Recompensas:** após cada sala, escolha 1 de 3 (técnicas pendentes primeiro, depois
  relíquias). Todas têm custo. Limpar uma sala cura 20 de vida.
- **Coordenador de ataque:** só 1 inimigo ataca por vez, com intervalo mínimo de 20 f.

## 4. Entidades
| Entidade | Vida | Postura | Forma | Golpes | Assinatura → Resposta Perfeita |
|---|---|---|---|---|---|
| Jogador | 100 | 100 | Círculo claro + cunha | Combo 3 (10 dano / 7 postura) | — |
| Ronin Errante | 60 | 60 | Quadrado | Corte Duplo; Corte Atrasado | Corte Atrasado → parry perfeito |
| Lanceiro | 55 | 50 | Triângulo | Cutucada; Estocada perigosa | Estocada → contra-investida |
| Monge do Sino | 110 | 100 | Hexágono grande | Palmas (3 golpes); Varredura perigosa | Varredura → esquiva perfeita |
| Chefe (marco 2) | — | — | — | 2 fases | — |

## 5. Progressão
Protótipo: 4 salas com dificuldade crescente (1 inimigo → trios). Jogo completo: 3 biomas
× ~6 salas + chefe. Ritmo: sala de ensino → mistura → pico → descanso (recompensa).

## 6. Vitória, derrota, reinício
Vitória: limpar a última sala (no MVP: derrotar o chefe). Derrota: vida 0 → tela "A morte
ensina" com estatísticas → nova run com nova semente em 1 botão.

## 7. Conteúdo do MVP
1 bioma, 4–5 tipos de inimigo, 1 chefe com 2 fases, ~20 recompensas, 1 arma.
Protótipo atual: 3 inimigos, 4 salas, 3 técnicas, 7 relíquias.

## 8. Interface
Telas: Título → Menu (Jogar, Configurações, Créditos, Sair) → Run (HUD) ⇄ Pausa
(Continuar, Configurações, Recomeçar, Menu) → Recompensa → … → Morte/Vitória → Menu.
HUD: vida, postura, cargas de esquiva, Fluxo, técnicas, sala atual.
UI do protótipo é provisória (IMGUI); UI final na Fase 6.

## 9. Onboarding
Sala 1 = um único Ronin, com dicas de controle no chão. Sala 2 introduz o Lanceiro
(estocada) já sabendo aparar. Sala 3 introduz a Varredura isolada. Sala 4 combina.

## 10. Direção de arte
Pixel art livre unificada por paleta única (shader no polimento). Telegraphs feitos por
código. Paleta:
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
SFX: golpe, acerto, bloqueio, parry perfeito, quebra de postura, finalizador, esquiva,
dano recebido, aviso de perigo, subida de Fluxo, UI. Música em camadas: drone
(exploração) → tambores (combate) → cordas (Fluxo 2) → coro (Fluxo 3 / chefe).
No protótipo tudo é sintetizado por código; a estrutura aceita arquivos reais depois.

## 12. Acessibilidade
Perigo nunca só por cor (forma + som + marca no chão). Intensidade de tremida 0–100%.
Opção de reduzir flashes. Controles remapeáveis (persistência pronta; tela de remapear
na Fase 6). Textos centralizados para tradução.

## 13. Balanceamento inicial
Ver tabela da seção 4 e `GameConfig.cs`. Hit-stop: acerto 4 f, bloqueio 3 f, parry
perfeito 7 f, contra-investida 10 f, finalizador 12 f, dano recebido 5 f.

## 14. Escopo (MoSCoW)
- **Must:** combate completo, 4–5 inimigos, 1 chefe, Técnicas Absorvidas, Fluxo,
  recompensas, telas obrigatórias, controle, remapeamento.
- **Should:** meta-progressão horizontal, corredores entre arenas, música real em camadas.
- **Could:** Rival/Nêmesis, segunda arma, biomas 2 e 3.
- **Won't (agora):** metroidvania, várias armas, multiplayer.
