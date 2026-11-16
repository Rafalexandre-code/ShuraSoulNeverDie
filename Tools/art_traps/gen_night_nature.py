"""Natureza noturna do Bosque Selvagem: recolore tiles_nature.png (Ninja Adventure, CC0) para
a noite azul de lua — folhas verde-azuladas, troncos de ardósia, flores rosadas acesas, neve
em azul-luar. Uso: python gen_night_nature.py <tiles_nature.png> <tiles_nature_night.png>"""
import colorsys
import sys

from PIL import Image

im = Image.open(sys.argv[1]).convert('RGBA')
px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = px[x, y]
        if a == 0:
            continue
        h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        deg = h * 360
        if (deg >= 330 or deg < 30) and 0.08 <= s < 0.5 and v > 0.75:   # flores (rosa e pêssego): rosa aceso
            h2, s2, v2 = 345 / 360 if deg < 30 and deg > 10 else h, min(1, s * 1.2 + 0.1), min(1, v * 1.02)
        elif s < 0.16 and v > 0.7:                         # neve / branco: luar
            h2, s2, v2 = 0.58, 0.16, v * 0.88
        elif s < 0.22:                                   # pedra / cinza: ardósia azulada
            h2, s2, v2 = 0.61, max(s, 0.14), v * 0.72
        elif 45 <= deg < 170:                            # folhas: verde-azulado da noite
            h2 = (172 + (deg - 45) * 0.28) / 360
            s2 = min(1, s * 0.78 + 0.05)
            v2 = v * (0.78 if v > 0.75 else 0.62) + 0.03
        elif deg >= 320 or deg < 8:                      # flores rosadas: acesas
            h2, s2, v2 = h, min(1, s * 1.05), min(1, v * 1.05)
        elif 8 <= deg < 45:                              # troncos e terra: violeta escuro
            h2, s2, v2 = 0.72, s * 0.32, v * 0.52
        else:                                            # azuis e roxos: um pouco mais fundos
            h2, s2, v2 = h, s, v * 0.85
        rr, gg, bb = colorsys.hsv_to_rgb(h2, max(0, min(1, s2)), max(0, min(1, v2)))
        px[x, y] = (int(rr * 255), int(gg * 255), int(bb * 255), a)

# A cerejeira (a árvore 2x2 na coluna 14 da primeira linha): no original é uma copa redonda sem
# tronco, que na noite virou um "cogumelo" rosa-choque. Vira uma sakura ao luar: pétalas claras
# rosa-lilás com sombra ameixa, pétalas soltas brilhando e um tronco escuro que sai de baixo da copa.
src = Image.open(sys.argv[1]).convert('RGBA').load()
SAKURA = {
    (255, 203, 169): (250, 208, 228),   # luz da copa
    (239, 149, 151): (230, 150, 198),   # meio-tom
    (207, 115, 109): (172, 96, 156),    # sombra
    (20, 27, 27): (42, 22, 50),         # contorno
}
X0, Y0 = 14 * 16, 0


def h(x, y):
    return ((x * 73856093) ^ (y * 19349663)) % 1000 / 1000.0


for y in range(32):
    for x in range(32):
        r, g, b, a = src[X0 + x, Y0 + y]
        if a == 0 or (r, g, b) not in SAKURA:
            continue
        c = SAKURA[(r, g, b)]
        if (r, g, b) != (20, 27, 27):
            # Volume: a lua vem do alto à esquerda; a barriga e o lado direito da copa escurecem.
            shade = max(0.0, (y - 9) / 14.0) * 0.55 + max(0.0, (x - 20) / 12.0) * 0.35 - max(0.0, (12 - y) / 12.0) * 0.12
            k = h(x // 2, y // 2)
            if shade + 0.18 * k > 0.62:
                c = (150, 80, 140)
            elif shade + 0.18 * k > 0.36:
                c = (206, 122, 182)
            elif shade + 0.18 * k > 0.16:
                c = (236, 166, 208)
            else:
                c = (250, 210, 230)
            if shade < 0.3 and h(x, y) > 0.9:
                c = (255, 244, 250)                  # pétala pegando a lua
        px[X0 + x, Y0 + y] = c + (a,)

# O tronco: colunas 14 a 17, da barriga da copa (linha 19) até o chão (linha 28), com raízes e
# dois galhos que entram na copa.
BARK, BARK_DARK, LINE = (82, 58, 86), (56, 38, 62), (30, 18, 36)
for y in range(19, 29):
    for x in range(13, 19):
        if x in (13, 18):
            px[X0 + x, Y0 + y] = LINE + (255,)
        else:
            px[X0 + x, Y0 + y] = (BARK if x < 16 else BARK_DARK) + (255,)
for x, y in ((12, 18), (11, 17), (12, 17), (19, 18), (20, 17), (19, 17), (13, 18), (18, 18)):
    px[X0 + x, Y0 + y] = (BARK_DARK if x > 16 else BARK) + (255,)
for x, y in ((11, 18), (10, 17), (20, 18), (21, 17)):
    px[X0 + x, Y0 + y] = LINE + (255,)
# A copa cai um pouco por cima do tronco (folhagem pendurada).
for x in (14, 16, 17):
    px[X0 + x, Y0 + 19] = (150, 80, 140, 255)
for x, y in ((12, 28), (19, 28), (12, 27), (19, 27)):
    px[X0 + x, Y0 + y] = LINE + (255,)
for x in range(13, 19):
    px[X0 + x, Y0 + 29] = LINE + (255,)
# Uma fresta de luz no tronco e duas pétalas caídas no chão.
px[X0 + 14, Y0 + 25] = (110, 84, 116, 255)
px[X0 + 14, Y0 + 26] = (110, 84, 116, 255)
px[X0 + 8, Y0 + 28] = (250, 208, 228, 255)
px[X0 + 23, Y0 + 29] = (250, 208, 228, 255)
im.save(sys.argv[2])
print('ok', im.size)
