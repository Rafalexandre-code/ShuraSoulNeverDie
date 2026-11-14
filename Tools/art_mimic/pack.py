"""Remonta o mímico do joraxus (Chest Mimic, itch.io) numa folha só, recolorida em roxo.
Linhas: 0 descanso(4) · 1 lamber(8) · 2 mastigar(4) · 3 ataque(9) · 4 golpe(6) · 5 piscar(4)."""
import sys, os
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
CW, CH, RIGHT = 36, 30, 31          # célula; coluna da borda direita do corpo
MAP = {
    (69, 42, 27): (34, 16, 52),      # contorno
    (97, 59, 29): (88, 44, 140),     # madeira média
    (117, 88, 61): (138, 86, 200),   # madeira clara
    (98, 70, 48): (104, 62, 164),    # veio
    (86, 53, 29): (70, 34, 118),     # madeira escura
    (153, 24, 23): (160, 22, 72),    # boca / língua
    (202, 51, 50): (222, 58, 120),
    (126, 24, 24): (112, 16, 62),
    (185, 30, 29): (196, 34, 98),
    (232, 138, 54): (196, 255, 92),  # a fechadura vira o olho: íris verde-ácido
    (255, 240, 137): (26, 6, 34),    # ... com pupila em fenda
}
SHUT = {(232, 138, 54): (70, 34, 118), (255, 240, 137): (34, 16, 52)}
def frames(name):
    im = Image.open(os.path.join(src, name + '.png')).convert('RGBA')
    w, h = im.size; px = im.load()
    cols = [any(px[x, y][3] for y in range(h)) for x in range(w)]
    segs, s = [], None
    for x, c in enumerate(cols + [False]):
        if c and s is None: s = x
        if not c and s is not None: segs.append((s, x - 1)); s = None
    return im, segs
def recolor(im, table):
    im = im.copy(); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            if p[3] and p[:3] in table: px[x, y] = table[p[:3]] + (255,)
    return im
rows = [('mimic rest', MAP), ('mimic lick', MAP), ('mimic chew', MAP), ('mimic attack', MAP), ('mimic hit', MAP), ('mimic rest', None)]
sheet = Image.new('RGBA', (CW * 9, CH * len(rows)), (0, 0, 0, 0))
for r, (name, table) in enumerate(rows):
    im, segs = frames(name)
    for i, (a, b) in enumerate(segs):
        f = im.crop((a, 0, b + 1, im.height))
        if table is None:
            f = recolor(recolor(f, SHUT), MAP)   # piscar: o olho fechado
        else:
            f = recolor(f, table)
        x = RIGHT + 1 - f.width
        assert x >= 0, (name, i)
        sheet.alpha_composite(f, (i * CW + x, r * CH + CH - f.height))
sheet.save(out)
print(sheet.size)
