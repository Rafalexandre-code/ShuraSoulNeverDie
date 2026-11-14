# Linha de base v0.15.0 (WP-00)

Os 4 robôs de referência (sementes 3, 11, 29, 41, alvo profundidade 20), headless, flags no padrão, com o código da v0.15.0 (as flags ainda não mudam nada no WP-00). Gerado de `bots.ps1 -tag base`; dados por sala em `v0.15.0-salas.csv`.

**Atenção:** os robôs não são determinísticos entre execuções: o relógio do dia avança pelo tempo real (a hora de cada sala, e com ela clima e elenco, depende da velocidade da máquina). Compare médias, não runs individuais. O WP-12 (hora fixa por ato) remove essa fonte.

## Resultado por semente

| Semente | Profundidade | Causa da morte | Parries | Esquivas | Contras | Finalizações | Técnicas | Tempo de simulação (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3 | 20 | o_kyuketsuki:garras_rubras_1 sala=Guardian | 54 | 19 | 3 | 18 | 7 | 313.3 |
| 11 | 14 | jorogumo:abraco_da_aranha sala=Guardian | 31 | 12 | 3 | 28 | 13 | 259.6 |
| 29 | 9 | asura_interior:asura_0 sala=Guardian | 15 | 3 | 4 | 10 | 5 | 146.7 |
| 41 | 14 | jorogumo:garras_de_seda_1 sala=Guardian | 55 | 9 | 5 | 22 | 12 | 288.2 |

## Tempo de simulação por tipo de sala

| Tipo | Salas | Média (s) | Mediana (s) | Mín–máx (s) |
| --- | --- | --- | --- | --- |
| Combat | 27 | 16.6 | 13.1 | 7.4–42.8 |
| Guardian | 11 | 25.9 | 27.1 | 6.8–38.3 |
| Duel | 5 | 6.8 | 5.3 | 4.8–11.6 |
| Challenge | 3 | 31.2 | 30.5 | 22.7–40.4 |
| Dispute | 2 | 30.6 | 30.6 | 21.8–39.4 |
| Shrine | 2 | 6.0 | 6.0 | 4.4–7.5 |
| Merchant | 2 | 6.5 | 6.5 | 4.3–8.7 |
| Smith | 1 | 3.8 | 3.8 | 3.8–3.8 |
| Event | 1 | 4.0 | 4.0 | 4.0–4.0 |

## Teclas usadas pelo robô (todas as salas, 4 sementes)

| Ação | Apertos |
| --- | --- |
| Attack | 940 |
| Gourd | 567 |
| Parry | 151 |
| Swap | 143 |
| Dodge | 136 |
| Tool | 95 |
| Detonate | 18 |

O robô de testes não usa todas as teclas que um humano usaria (por exemplo, ele aperta Aparar e Esquivar de forma reativa e quase nunca usa ferramentas); a telemetria do WP-01 mede humanos.
