# SHURA — Roadmap (Fase 4)

Ordenado por risco: o mais incerto primeiro. P/M/G = complexidade.

## M1 — Protótipo do core loop (G) ← ATUAL
Objetivo: provar que aparar, quebrar postura e absorver técnicas é divertido.
- Projeto Unity, timestep fixo, entrada remapeável, câmera, arena
- Jogador: movimento com easing, combo, parry/bloqueio direcional, esquiva, finalizador
- 3 inimigos (Ronin, Lanceiro, Monge), coordenador de ataque, telegraphs
- Técnicas Absorvidas (3), relíquias (7), Fluxo, 4 salas, morte/vitória
- Hit-stop, tremida, partículas, SFX e música sintetizados, HUD e menus provisórios
- Overlay de debug e atalhos de desenvolvimento; testes de lógica

**Pronto quando:** compila sem erros; testes passam; uma run completa é jogável do
título à vitória ou morte, com teclado e com controle; você aprova a *sensação* do parry.

**M1.1 — após o primeiro playtest (feito):** indicador de tempo (anel + som), janela de
parry 7 → 9 f, Altar das Almas com Ecos e 8 melhorias com teto, armas animadas
(antecipação legível), braseiros, brasas, vinheta, transições, números de dano.

**M1.2 — confronto de espadas (feito):** 5 inimigos novos (Kenkaku, Carrasco, Arqueiro,
Sombra, Escudeiro) com 5 técnicas; agarrão, flechas refletíveis, teletransporte; salas
sorteadas (run de 6); choque de lâminas com quique das armas; som metálico de espada;
ritmo global mais lento que acelera com o Fluxo; Fluxo dá dano e impacto; sequência
perfeita; câmera lenta no último golpe.

**M1.3 — run infinita (feito):** salas procedurais infinitas por profundidade, biomas,
arenas de tamanhos variados, Guardiões a cada 5 salas, escala de dificuldade, Ascensão
automática, Bênçãos repetíveis, IA que lê o jogador (5 leituras) e as anuncia.

**M1.4 — arsenal, sangue e equipamento (feito):** 6 armas com combos, golpes de troca e
finalizações próprias; Troca Perfeita; sangue e manchas persistentes (com opção); itens com
10 raridades, 4 espaços, 6 elementos e atributos aleatórios; tela de Arsenal e de
Equipamento; painel de técnicas removido da tela de jogo.

## M2 — Vertical slice (G)
Chefe com 2 fases; pixel art livre escolhida + shader de paleta; migração URP 2D; corredores
curtos; UI final das telas obrigatórias; tela de remapeamento.
**Pronto quando:** sala + chefe com qualidade de produto final.

## M3 — Produção de conteúdo (G)
+2 inimigos, ~20 recompensas, meta-progressão horizontal, salas do bioma 1.
**Pronto quando:** run de 12–15 min com variedade real de builds.

## M4 — Polimento e game feel (M)
Checklist de game feel completo, música real em camadas, acessibilidade.

## M5 — QA e balanceamento (M)
Plano de testes, telemetria local de mortes, ajuste das tabelas, playtests.

## M6 — Lançamento (M)
Build Steam (Steamworks), página, trailer, README, créditos, CHANGELOG.
