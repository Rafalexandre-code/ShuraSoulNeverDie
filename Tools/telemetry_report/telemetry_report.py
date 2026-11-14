"""
Relatório de telemetria do Shura (WP-01).

Uso:
    python telemetry_report.py <pasta com .jsonl> [pasta de saída]

Lê todos os run-*.jsonl da pasta e gera, na pasta de saída (padrão: <pasta>/relatorio):
    relatorio.md        o relatório em Markdown
    parry_delta.csv     histograma do atraso das respostas por tipo de anel
    letalidade.csv      mortes por inimigo normalizadas pela exposição
    recompensas.csv     pick rate e win rate por recompensa
    salas.csv           duração de cada sala
    teclas.csv          uso de cada ação

Perguntas da Parte I.1 do plano: anel bem calibrado? inimigo injusto? morte culpa do
jogador? estratégia dominante? recompensas são escolhas reais? o que ninguém usa?
"""
import csv
import glob
import json
import os
import statistics
import sys
from collections import Counter, defaultdict


def load(folder):
    runs = defaultdict(list)
    for path in sorted(glob.glob(os.path.join(folder, '*.jsonl'))):
        with open(path, encoding='utf-8') as f:
            for n, line in enumerate(f, 1):
                line = line.strip()
                if not line:
                    continue
                try:
                    e = json.loads(line)
                except json.JSONDecodeError as err:
                    print(f'aviso: {os.path.basename(path)}:{n} inválido ({err})', file=sys.stderr)
                    continue
                runs[e.get('runId', path)].append(e)
    return runs


def histogram(values, lo=-20, hi=20):
    """Barras de texto por quadro (valores fora da faixa vão para as pontas)."""
    c = Counter(max(lo, min(hi, v)) for v in values)
    peak = max(c.values()) if c else 1
    lines = []
    for d in range(lo, hi + 1):
        if c.get(d, 0) == 0:
            continue
        label = f'≤{lo}' if d == lo else f'≥{hi}' if d == hi else f'{d:+d}'
        lines.append(f'{label:>5} | {"█" * max(1, round(30 * c[d] / peak))} {c[d]}')
    return '\n'.join(lines)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    folder = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(folder, 'relatorio')
    os.makedirs(out, exist_ok=True)
    runs = load(folder)
    events = [e for evs in runs.values() for e in evs]
    by = defaultdict(list)
    for e in events:
        by[e.get('type')].append(e)

    md = ['# Relatório de telemetria do Shura', '',
          f'{len(runs)} runs, {len(events)} eventos, pasta `{folder}`.', '']

    # ── Resultado das runs ──────────────────────────────────────────────────────────
    ends = by['run_end']
    results = Counter(e.get('result') for e in ends)
    md += ['## Runs', '', '| Resultado | Runs |', '| --- | --- |']
    md += [f'| {k} | {v} |' for k, v in results.most_common()]
    durs = [e['duration_s'] for e in ends if 'duration_s' in e]
    if durs:
        md += ['', f'Duração das runs: média {statistics.mean(durs):.0f} s, mediana {statistics.median(durs):.0f} s, '
                   f'mín {min(durs):.0f} s, máx {max(durs):.0f} s. Algozes criados: {sum(1 for e in ends if e.get("algoz"))}.']
    md.append('')

    # ── Anéis: atraso por tipo ──────────────────────────────────────────────────────
    rings = by['ring_resolved']
    md += ['## Anéis: resposta e atraso (quadros em relação ao fechamento; negativo = cedo)', '']
    with open(os.path.join(out, 'parry_delta.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['anel', 'resposta', 'delta', 'quantidade'])
        for ring in sorted({r.get('ring') for r in rings}):
            rs = [r for r in rings if r.get('ring') == ring]
            resp = Counter(r.get('response') for r in rs)
            ok = sum(1 for r in rs if r.get('success'))
            md += [f'### Anel {ring}: {len(rs)} golpes, {ok} respondidos ({100 * ok / max(1, len(rs)):.0f}%)', '',
                   'Respostas: ' + ', '.join(f'{k} {v}' for k, v in resp.most_common()), '']
            deltas = [r['delta'] for r in rs if r.get('delta') is not None]
            errs = [r['delta'] for r in rs if r.get('delta') is not None and not r.get('success')]
            if errs:
                early = sum(1 for d in errs if d < 0)
                md.append(f'Erros com atraso medido: {len(errs)}; cedo {early} ({100 * early / len(errs):.0f}%), '
                          f'tarde {len(errs) - early}. Sinal de problema: mais de 60% do mesmo lado.')
                md.append('')
            if deltas:
                md += ['```', histogram(deltas), '```', '']
            for (rp, d), n in sorted(Counter((r.get('response'), r['delta']) for r in rs if r.get('delta') is not None).items()):
                w.writerow([ring, rp, d, n])
    # Bloqueio dominante?
    cyan = [r for r in rings if r.get('ring') == 'ciano' and r.get('response') in ('parry', 'block', 'dodge', 'swap')]
    if cyan:
        blocks = sum(1 for r in cyan if r.get('response') == 'block')
        md += [f'Bloqueio nas respostas a anéis ciano: {100 * blocks / len(cyan):.0f}% (sinal de problema: acima de 35%).', '']

    # ── Letalidade por inimigo ──────────────────────────────────────────────────────
    exposure = defaultdict(set)
    for r in rings:
        if r.get('enemy'):
            exposure[r['enemy']].add((r.get('runId'), r.get('room')))
    deaths = Counter()
    for d in by['death']:
        src = (d.get('source') or '').split(':')[0]
        deaths[src or '?'] += 1
    md += ['## Letalidade por inimigo (mortes ÷ salas em que ele atacou)', '', '| Inimigo | Salas | Mortes | Letalidade |', '| --- | --- | --- | --- |']
    rows = []
    for enemy, rooms in exposure.items():
        rows.append((enemy, len(rooms), deaths.get(enemy, 0), deaths.get(enemy, 0) / len(rooms)))
    rows.sort(key=lambda r: -r[3])
    mean = statistics.mean([r[3] for r in rows]) if rows else 0
    for enemy, rooms, dead, rate in rows:
        flag = ' **(> 2× a média)**' if mean > 0 and rate > 2 * mean else ''
        md.append(f'| {enemy} | {rooms} | {dead} | {rate:.2f}{flag} |')
    with open(os.path.join(out, 'letalidade.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['inimigo', 'salas', 'mortes', 'letalidade'])
        w.writerows(rows)
    other = [k for k in deaths if k not in exposure]
    if other:
        md.append('')
        md.append('Mortes sem anel registrado (armadilhas, perigos): ' + ', '.join(f'{k} {deaths[k]}' for k in other))
    md.append('')

    # ── Morte culpa do jogador? ─────────────────────────────────────────────────────
    hits = by['player_hit']
    if hits:
        known = [h for h in hits if h.get('ring_visible') is not None]
        hidden = [h for h in known if h.get('ring_visible') is False]
        md += ['## Golpes sofridos com o anel fora da tela', '',
               f'{len(hits)} golpes sofridos; visibilidade conhecida em {len(known)}; com o atacante fora da tela: {len(hidden)}. '
               'Qualquer dano relevante sem anel visível é bug de telegrafia.', '']

    # ── Recompensas ─────────────────────────────────────────────────────────────────
    offered = Counter()
    picked = Counter()
    for e in by['reward_offered']:
        for o in e.get('options', []):
            offered[o] += 1
    victory_runs = {e.get('runId') for e in ends if e.get('result') == 'vitoria'}
    runs_with = defaultdict(set)
    for e in by['reward_picked']:
        picked[e.get('picked')] += 1
        runs_with[e.get('picked')].add(e.get('runId'))
    md += ['## Recompensas (pick rate = escolhida ÷ oferecida)', '', '| Recompensa | Oferecida | Escolhida | Pick rate | Win rate |', '| --- | --- | --- | --- | --- |']
    rrows = []
    for rid in offered:
        rate = picked[rid] / offered[rid]
        wins = len(runs_with[rid] & victory_runs) / len(runs_with[rid]) if runs_with[rid] else 0
        rrows.append((rid, offered[rid], picked[rid], rate, wins))
    rrows.sort(key=lambda r: -r[3])
    for rid, o, p, rate, wins in rrows:
        md.append(f'| {rid} | {o} | {p} | {100 * rate:.0f}% | {100 * wins:.0f}% |')
    with open(os.path.join(out, 'recompensas.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['recompensa', 'oferecida', 'escolhida', 'pick_rate', 'win_rate'])
        w.writerows(rrows)
    ignored = [r[0] for r in rrows if r[1] >= 5 and r[3] < 0.05]
    if ignored:
        md += ['', 'Ignoradas (pick < 5% em 5+ ofertas): ' + ', '.join(ignored)]
    md.append('')

    # ── Salas e ritmo ───────────────────────────────────────────────────────────────
    rooms = by['room_end']
    kinds = defaultdict(list)
    for r in rooms:
        kinds[r.get('kind')].append(r.get('duration_s', 0))
    md += ['## Duração das salas', '', '| Tipo | Salas | Média (s) | Mediana (s) | Dano recebido médio | Curas médias |', '| --- | --- | --- | --- | --- | --- |']
    for k, v in sorted(kinds.items(), key=lambda kv: -len(kv[1])):
        rs = [r for r in rooms if r.get('kind') == k]
        md.append(f'| {k} | {len(v)} | {statistics.mean(v):.1f} | {statistics.median(v):.1f} | '
                  f'{statistics.mean([r.get("damage_taken", 0) for r in rs]):.1f} | {statistics.mean([r.get("heals", 0) for r in rs]):.2f} |')
    fin = Counter()
    for r in rooms:
        for k, v in (r.get('finishers') or {}).items():
            fin[k] += v
    if fin:
        md += ['', 'Finalizações: ' + ', '.join(f'{k} {v}' for k, v in fin.most_common())]
    with open(os.path.join(out, 'salas.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['run', 'sala', 'profundidade', 'tipo', 'bioma', 'duracao_s', 'dano', 'curas'])
        for r in rooms:
            w.writerow([r.get('runId'), r.get('room'), r.get('depth'), r.get('kind'), r.get('biome'), r.get('duration_s'), r.get('damage_taken'), r.get('heals')])
    md.append('')

    # ── Rota ────────────────────────────────────────────────────────────────────────
    routes = by['route_choice']
    if routes:
        chosen = Counter((e.get('chosen') or '').split('/')[0] for e in routes)
        md += ['## Portas escolhidas', '', ', '.join(f'{k} {v}' for k, v in chosen.most_common()), '']

    # ── Teclas ──────────────────────────────────────────────────────────────────────
    keys = Counter()
    for e in by['input_use']:
        for k, v in (e.get('counts') or {}).items():
            keys[k] += v
    total = sum(keys.values()) or 1
    md += ['## Uso de cada ação', '', '| Ação | Apertos | % |', '| --- | --- | --- |']
    md += [f'| {k} | {v} | {100 * v / total:.1f}% |' for k, v in keys.most_common()]
    rare = [k for k, v in keys.items() if v / total < 0.02]
    if rare:
        md += ['', 'Ações com menos de 2% de uso (candidatas a corte depois de 3 runs humanas): ' + ', '.join(rare)]
    with open(os.path.join(out, 'teclas.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['acao', 'apertos'])
        w.writerows(keys.most_common())
    md.append('')

    with open(os.path.join(out, 'relatorio.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(md) + '\n')
    print(f'ok: {len(runs)} runs → {os.path.join(out, "relatorio.md")}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
