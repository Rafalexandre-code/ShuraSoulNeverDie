# Relatório de telemetria do Shura

2 runs, 362 eventos, pasta `C:/Users/rafae/AppData/Local/Temp/claude/3907ad0de26f862d/d27df8ee-fd02-48ce-ac2f-46854fce7fbe/scratchpad/tele1`.

## Runs

| Resultado | Runs |
| --- | --- |
| morte | 2 |

Duração das runs: média 302 s, mediana 302 s, mín 283 s, máx 321 s. Algozes criados: 2.

## Anéis: resposta e atraso (quadros em relação ao fechamento; negativo = cedo)

### Anel ciano: 118 golpes, 80 respondidos (68%)

Respostas: parry 66, swap 38, hit 14

Erros com atraso medido: 24; cedo 19 (79%), tarde 5. Sinal de problema: mais de 60% do mesmo lado.

```
 ≤-20 | ████ 9
  -15 | █ 1
  -12 | █ 1
  -11 | █ 1
  -10 | █ 1
   -8 | █ 2
   -7 | █ 1
   -6 | █ 2
   -2 | █ 2
   -1 | ██ 4
   +0 | █ 2
   +1 | ███ 7
   +2 | ██████████████████████████████ 66
   +3 | █ 3
   +4 | █ 2
```

### Anel maldito: 2 golpes, 2 respondidos (100%)

Respostas: parry 1, swap 1

```
   +2 | ██████████████████████████████ 2
```

### Anel roxo: 5 golpes, 3 respondidos (60%)

Respostas: swap 5

Erros com atraso medido: 2; cedo 0 (0%), tarde 2. Sinal de problema: mais de 60% do mesmo lado.

```
   +2 | ██████████████████████████████ 5
```

### Anel vermelho: 33 golpes, 31 respondidos (94%)

Respostas: dodge 31, hit 2

```
   +1 | █ 1
   +2 | ██████████████████████████████ 30
```

Bloqueio nas respostas a anéis ciano: 0% (sinal de problema: acima de 35%).

## Letalidade por inimigo (mortes ÷ salas em que ele atacou)

| Inimigo | Salas | Mortes | Letalidade |
| --- | --- | --- | --- |
| o_kyuketsuki | 1 | 1 | 1.00 **(> 2× a média)** |
| jorogumo | 2 | 1 | 0.50 **(> 2× a média)** |
| ronin | 4 | 0 | 0.00 |
| lanceiro | 3 | 0 | 0.00 |
| lodo | 3 | 0 | 0.00 |
| kenkaku | 3 | 0 | 0.00 |
| kunoichi | 3 | 0 | 0.00 |
| arqueiro | 2 | 0 | 0.00 |
| kappa | 5 | 0 | 0.00 |
| kagutsuchi | 2 | 0 | 0.00 |
| carrasco | 2 | 0 | 0.00 |
| curandeira | 3 | 0 | 0.00 |
| ciclope | 4 | 0 | 0.00 |
| vampiro | 2 | 0 | 0.00 |
| olho | 4 | 0 | 0.00 |
| berserker | 3 | 0 | 0.00 |
| monge | 3 | 0 | 0.00 |
| raijin | 3 | 0 | 0.00 |
| kamaitachi | 2 | 0 | 0.00 |
| onryo | 3 | 0 | 0.00 |
| tengu | 3 | 0 | 0.00 |
| rokurokubi | 4 | 0 | 0.00 |
| lanterna | 3 | 0 | 0.00 |
| bombardeiro | 1 | 0 | 0.00 |
| sombra | 3 | 0 | 0.00 |
| esqueleto | 3 | 0 | 0.00 |
| aranha_menor | 1 | 0 | 0.00 |
| nopperabo | 1 | 0 | 0.00 |
| ittan_momen | 1 | 0 | 0.00 |
| omukade | 1 | 0 | 0.00 |

## Golpes sofridos com o anel fora da tela

41 golpes sofridos; visibilidade conhecida em 0; com o atacante fora da tela: 0. Qualquer dano relevante sem anel visível é bug de telegrafia.

## Recompensas (pick rate = escolhida ÷ oferecida)

| Recompensa | Oferecida | Escolhida | Pick rate | Win rate |
| --- | --- | --- | --- | --- |
| tec_lamina_paciente | 2 | 2 | 100% | 0% |
| tec_correnteza_do_kappa | 1 | 1 | 100% | 0% |
| tec_emboscada | 2 | 2 | 100% | 0% |
| tec_golpe_do_carrasco | 1 | 1 | 100% | 0% |
| tec_furia_do_barbaro | 1 | 1 | 100% | 0% |
| rel_lamina_faminta | 1 | 1 | 100% | 0% |
| tec_passo_fantasma | 1 | 1 | 100% | 0% |
| rel_ampulheta_quebrada | 1 | 1 | 100% | 0% |
| ben_gume | 1 | 1 | 100% | 0% |
| tec_emboscada#2.0 | 1 | 1 | 100% | 0% |
| tec_bencao_da_xama | 1 | 1 | 100% | 0% |
| rel_voto_de_silencio | 3 | 2 | 67% | 0% |
| tec_estocada_espelhada | 3 | 2 | 67% | 0% |
| tec_saque_iai | 3 | 2 | 67% | 0% |
| tec_golpe_do_oni | 2 | 1 | 50% | 0% |
| tec_sede_de_sangue | 2 | 1 | 50% | 0% |
| tec_piscar_do_vigia | 2 | 1 | 50% | 0% |
| rel_passos_leves | 2 | 1 | 50% | 0% |
| rel_corrente_do_carrasco | 3 | 1 | 33% | 0% |
| rel_guarda_de_ferro | 3 | 1 | 33% | 0% |
| rel_moeda_de_seis_mon | 3 | 1 | 33% | 0% |
| rel_ritmo_implacavel | 4 | 1 | 25% | 0% |
| rel_olhos_de_falcao | 5 | 1 | 20% | 0% |
| rel_fluxo_ardente | 1 | 0 | 0% | 0% |
| item_2 | 2 | 0 | 0% | 0% |
| item_7 | 1 | 0 | 0% | 0% |
| rel_sandalias_de_palha | 3 | 0 | 0% | 0% |
| ben_vento | 2 | 0 | 0% | 0% |
| ben_vigor | 2 | 0 | 0% | 0% |
| item_14 | 1 | 0 | 0% | 0% |
| rel_olho_do_dragao | 1 | 0 | 0% | 0% |
| ben_guarda | 3 | 0 | 0% | 0% |
| ben_calma | 2 | 0 | 0% | 0% |
| fus_lua_sangrenta | 1 | 0 | 0% | 0% |
| rel_juramento_de_sangue | 3 | 0 | 0% | 0% |
| item_4 | 1 | 0 | 0% | 0% |
| item_6 | 1 | 0 | 0% | 0% |
| item_10 | 1 | 0 | 0% | 0% |
| rel_pergaminho_em_branco | 2 | 0 | 0% | 0% |
| item_15 | 1 | 0 | 0% | 0% |
| item_17 | 1 | 0 | 0% | 0% |
| item_30 | 1 | 0 | 0% | 0% |
| rel_mascara_hannya | 1 | 0 | 0% | 0% |
| item_34 | 1 | 0 | 0% | 0% |

## Duração das salas

| Tipo | Salas | Média (s) | Mediana (s) | Dano recebido médio | Curas médias |
| --- | --- | --- | --- | --- | --- |
| Combat | 16 | 18.0 | 12.5 | 13.9 | 0.25 |
| Guardian | 9 | 19.2 | 19.9 | 57.0 | 0.56 |
| Duel | 5 | 6.5 | 7.3 | 0.0 | 0.00 |
| Dispute | 2 | 34.2 | 34.2 | 33.2 | 0.50 |
| Challenge | 2 | 19.0 | 19.0 | 69.3 | 1.50 |
| Event | 1 | 4.8 | 4.8 | 0.0 | 0.00 |

Finalizações: dirigida_perfeito 51

## Portas escolhidas

Combat 13, Guardian 7, Event 3, Duel 3, Shrine 2, Dispute 2, Smith 1, Merchant 1, Challenge 1

## Uso de cada ação

| Ação | Apertos | % |
| --- | --- | --- |
| Attack | 563 | 49.0% |
| Gourd | 252 | 21.9% |
| Parry | 95 | 8.3% |
| Swap | 84 | 7.3% |
| Dodge | 82 | 7.1% |
| Tool | 69 | 6.0% |
| Detonate | 5 | 0.4% |

Ações com menos de 2% de uso (candidatas a corte depois de 3 runs humanas): Detonate

